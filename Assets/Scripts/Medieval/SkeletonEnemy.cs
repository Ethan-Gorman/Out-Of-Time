using UnityEngine;

// A skeleton that patrols between two points and chases the player when they come close.
// Touching it hurts; jumping on its head defeats it.
// Moves in world time with a kinematic Rigidbody, like EnemyChase.
// Keep the art (SpriteRenderer, Animator, Billboard) on the "Sprite" child.
[RequireComponent(typeof(Rigidbody))]
public class SkeletonEnemy : MonoBehaviour
{
    [Header("Patrol (world positions, set on placement)")]
    public Vector3 patrolA;
    public Vector3 patrolB;
    public float patrolSpeed = 1.6f;

    [Header("Chase")]
    public float chaseSpeed = 3.2f;
    public float aggroRadius = 6f;
    public float leashRadius = 9f;

    [Header("Combat")]
    public float contactTimeCost = -1f;
    public float stompHeight = 0.7f;
    public float stompBounce = 4.5f;
    public float pauseAfterHit = 1f;

    [Header("Art")]
    public SpriteRenderer sprite;
    public Animator animator;

    public bool IsDead { get; private set; }

    Rigidbody body;
    Transform player;
    Vector3 home;
    bool towardsB = true;

    // Counts down in world time after a hit; the skeleton doesn't move while it's above 0
    float pauseLeft;

    // Whether the skeleton moved this physics step. Its walk animation freezes when it doesn't
    bool moving;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;

        var p = GameObject.FindWithTag("Player");
        if (p != null)
            player = p.transform;

        if (patrolA == Vector3.zero && patrolB == Vector3.zero)
        {
            patrolA = transform.position + Vector3.left * 3f;
            patrolB = transform.position + Vector3.right * 3f;
        }
        home = (patrolA + patrolB) * 0.5f;
    }

    void Update()
    {
        // Walk animation runs in world time, and stops while the skeleton stands still (e.g. after a hit)
        if (animator != null)
            animator.speed = moving ? WorldTime.Scale : 0f;
    }

    void FixedUpdate()
    {
        moving = false;
        if (IsDead)
            return;

        // Standing still after landing a hit. World time, so it lasts longer while the world is slowed
        if (pauseLeft > 0f)
        {
            pauseLeft -= WorldTime.FixedDelta;
            return;
        }

        Vector3 pos = body.position;
        Vector3 target;
        float speed;

        bool playerInLeash = player != null && Flat(player.position - home).magnitude < leashRadius;
        bool playerClose = player != null && Flat(player.position - pos).magnitude < aggroRadius;

        if (playerInLeash && playerClose)
        {
            target = player.position;
            speed = chaseSpeed;
        }
        else
        {
            target = towardsB ? patrolB : patrolA;
            speed = patrolSpeed;
            if (Flat(target - pos).sqrMagnitude < 0.04f)
                towardsB = !towardsB;
        }

        Vector3 to = Flat(target - pos);
        if (to.sqrMagnitude < 0.01f)
            return;

        Vector3 step = Vector3.ClampMagnitude(to.normalized * speed * WorldTime.FixedDelta, to.magnitude);
        body.MovePosition(pos + step);
        moving = true;

        if (sprite != null && Mathf.Abs(to.x) > 0.01f)
            sprite.flipX = to.x < 0f;
    }

    // The Hitbox child's trigger reports here (trigger messages go to the Rigidbody's object).
    // Enter as well as Stay: Stay stops firing once both bodies are still and asleep
    void OnTriggerEnter(Collider other) => Touch(other);
    void OnTriggerStay(Collider other) => Touch(other);

    void Touch(Collider other)
    {
        if (IsDead || other.attachedRigidbody == null)
            return;

        var player = other.attachedRigidbody.GetComponent<PlayerDamage>();
        if (player == null || player.IsOut)
            return;

        Rigidbody playerBody = other.attachedRigidbody;
        bool above = playerBody.position.y > body.position.y + stompHeight;
        bool falling = playerBody.linearVelocity.y <= 0.5f;

        if (above && falling)
        {
            Vector3 v = playerBody.linearVelocity;
            v.y = stompBounce;
            playerBody.linearVelocity = v;
            Die();
        }
        else
        {
            // Only pause when the hit actually landed (not while the player is invulnerable)
            if (player.Hit(body.position, contactTimeCost))
                pauseLeft = pauseAfterHit;
        }
    }

    public void Die()
    {
        if (IsDead)
            return;
        IsDead = true;

        if (animator != null)
        {
            animator.SetBool("Dead", true);
            animator.speed = 1f;
        }
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;
        enabled = false;
        Destroy(gameObject, 2f);
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(patrolA, patrolB);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f);
        Gizmos.DrawWireSphere(Application.isPlaying ? home : (patrolA + patrolB) * 0.5f, leashRadius);
    }
}
