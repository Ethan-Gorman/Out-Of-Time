using UnityEngine;
using UnityEngine.Events;

// A skeleton that patrols between two points and chases the player when they come close.
// Touching it hurts; jumping on its head defeats it and puts a few seconds back on the clock.
// Moves in world time with a kinematic Rigidbody, like EnemyChase.
// Keep the art (SpriteRenderer, Animator, Billboard) on the "Sprite" child.
// Has a solid capsule so the player can't walk through it; the Hitbox trigger sits just outside it.
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

    [Header("Reward")]
    // Seconds back on the clock for stomping it. 0 gives nothing
    public float killTimeReward = 5f;
    // Where the "+5s" popup appears, above the sprite (or the skeleton when it has none)
    public float rewardPopupHeight = 1.2f;
    public UnityEvent onKilled;

    [Header("Body")]
    public float bodyRadius = 0.36f;

    [Header("Art")]
    public SpriteRenderer sprite;
    public Animator animator;

    public bool IsDead { get; private set; }

    Rigidbody body;
    Transform player;
    Rigidbody playerRigidbody;
    float playerRadius = 0.3f;
    float solidRadius;
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
        {
            player = p.transform;
            playerRigidbody = p.GetComponent<Rigidbody>();
            if (p.TryGetComponent(out CapsuleCollider playerCapsule))
                playerRadius = playerCapsule.radius;
        }
        EnsureSolid();

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
        if (sprite != null && Mathf.Abs(to.x) > 0.01f)
            sprite.flipX = to.x < 0f;

        step = StopAtPlayer(pos, step);
        if (step.sqrMagnitude < 0.000001f)
            return;
        body.MovePosition(pos + step);
        moving = true;
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

    // A solid capsule on the skeleton itself (the prefab's, or a new one), wide enough to match the art.
    // The Hitbox trigger is kept a bit wider than it, so the player still touches the trigger (contact hit,
    // stomp) before the solid body stops them. Kinematic bodies don't collide with each other, so skeletons
    // never get stuck on one another
    void EnsureSolid()
    {
        CapsuleCollider solid = null;
        foreach (var c in GetComponents<CapsuleCollider>())
            if (!c.isTrigger) { solid = c; break; }

        if (solid == null)
        {
            solid = gameObject.AddComponent<CapsuleCollider>();
            solid.height = 1f;
            solid.center = new Vector3(0f, 0.5f, 0f);
        }
        solid.radius = Mathf.Max(solid.radius, bodyRadius);
        solidRadius = solid.radius;

        foreach (var c in GetComponentsInChildren<CapsuleCollider>())
            if (c.isTrigger)
                c.radius = Mathf.Max(c.radius, solidRadius + 0.1f);
    }

    // A kinematic body shoves the player out of its way, which can squash them into a wall.
    // So the skeleton walks up to the player and stops when the two bodies touch, instead of walking into them.
    // Sideways movement is kept so it can still slide round them. Doesn't block while the player is above (stomping)
    Vector3 StopAtPlayer(Vector3 pos, Vector3 step)
    {
        if (player == null)
            return step;

        // The physics position, not the interpolated transform, so the gap matches what the colliders see
        Vector3 playerPos = playerRigidbody != null ? playerRigidbody.position : player.position;
        if (Mathf.Abs(playerPos.y - pos.y) > 0.9f)
            return step;

        Vector3 toPlayer = Flat(playerPos - pos);
        float distance = toPlayer.magnitude;
        if (distance < 0.001f)
            return step;

        Vector3 dir = toPlayer / distance;
        float towards = Vector3.Dot(step, dir);
        if (towards <= 0f)
            return step;

        float room = Mathf.Max(0f, distance - (solidRadius + playerRadius));
        return step - dir * towards + dir * Mathf.Min(towards, room);
    }

    public void Die()
    {
        if (IsDead)
            return;
        IsDead = true;

        // The IsDead check above means this pays out once. Clock.AddTime shows the HUD "+5s" and plays the gain sound;
        // skipped once the clock has stopped (out of time, or the level is finished)
        var clock = Clock.Instance;
        if (killTimeReward > 0f && clock != null && !clock.IsOut && !clock.IsFrozen)
        {
            clock.AddTime(killTimeReward);
            Vector3 at = sprite != null ? sprite.transform.position : transform.position;
            FloatText.TimeGain(at + Vector3.up * rewardPopupHeight, killTimeReward);
        }
        onKilled?.Invoke();

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
