using UnityEngine;

// The factory's Clockwork Guard: a brass security automaton. Patrols between two points, chases the player
// when they come close, and when it gets within reach it winds up (shakes, glows) and does a spinning attack
// with its blade arms. After the spin it stops to wind its spring again. Touching it hurts; jumping on its
// head defeats it, the same as the Medieval skeleton. Everything runs in world time with a kinematic Rigidbody.
// Keep the art (SpriteRenderer, Animator, Billboard) on the "Sprite" child, the stomp/contact trigger on "Hitbox".
[RequireComponent(typeof(Rigidbody))]
public class ClockworkGuard : MonoBehaviour
{
    enum Mode { Move, Windup, Spin, Recover }

    [Header("Patrol (world positions, set on placement)")]
    public Vector3 patrolA;
    public Vector3 patrolB;
    public float patrolSpeed = 1.4f;

    [Header("Chase")]
    public float chaseSpeed = 2.6f;
    public float aggroRadius = 5.5f;
    public float leashRadius = 8f;

    [Header("Spin attack (world-time seconds)")]
    public float attackRange = 1.7f;
    public float spinRadius = 1.45f;
    public float windupTime = 0.7f;
    public float spinTime = 0.9f;
    public float recoverTime = 1.3f;
    public float spinMoveSpeed = 1.2f;
    public Transform blades;
    public float bladeDegreesPerSecond = 900f;

    [Header("Combat")]
    public float contactTimeCost = -1f;
    public float stompHeight = 0.7f;
    public float stompBounce = 4.5f;
    public float pauseAfterHit = 1f;

    [Header("Art")]
    public SpriteRenderer sprite;
    public Animator animator;
    public Color windupColor = new Color(1f, 0.55f, 0.3f);
    public SteamEmitter deathSparks;

    public bool IsDead { get; private set; }

    Rigidbody body;
    Transform player;
    PlayerDamage playerDamage;
    Vector3 home;
    bool towardsB = true;
    Mode mode = Mode.Move;
    float modeTimer;
    float pauseLeft;
    bool moving;
    Color baseColor = Color.white;
    Vector3 spriteBase;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;

        var p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerDamage = p.GetComponent<PlayerDamage>();
        }

        if (patrolA == Vector3.zero && patrolB == Vector3.zero)
        {
            patrolA = transform.position + Vector3.left * 3f;
            patrolB = transform.position + Vector3.right * 3f;
        }
        home = (patrolA + patrolB) * 0.5f;
        if (sprite != null)
        {
            baseColor = sprite.color;
            spriteBase = sprite.transform.localPosition;
        }
        if (blades != null)
            blades.gameObject.SetActive(false);
    }

    void Update()
    {
        if (IsDead)
            return;
        if (animator != null)
            animator.speed = moving ? WorldTime.Scale : 0f;

        float dt = WorldTime.Delta;
        if (sprite != null)
        {
            // Wind-up: rattle and heat up so the attack is readable before it lands
            bool winding = mode == Mode.Windup;
            float heat = winding ? Mathf.Clamp01(modeTimer / windupTime) : 0f;
            sprite.color = Color.Lerp(baseColor, windupColor, heat);
            sprite.transform.localPosition = spriteBase + (winding ? new Vector3(Mathf.Sin(Time.time * 70f) * 0.04f * WorldTime.Scale, 0f, 0f) : Vector3.zero);
            if (mode == Mode.Spin)
                sprite.flipX = Mathf.Repeat(modeTimer * 8f, 1f) > 0.5f;
        }
        if (blades != null && blades.gameObject.activeSelf)
            blades.Rotate(0f, bladeDegreesPerSecond * dt, 0f, Space.World);
    }

    void FixedUpdate()
    {
        moving = false;
        if (IsDead)
            return;
        float dt = WorldTime.FixedDelta;

        if (pauseLeft > 0f)
        {
            pauseLeft -= dt;
            return;
        }

        Vector3 pos = body.position;
        Vector3 toPlayer = player != null ? Flat(player.position - pos) : Vector3.zero;
        bool playerInLeash = player != null && Flat(player.position - home).magnitude < leashRadius;
        bool playerClose = player != null && toPlayer.magnitude < aggroRadius && Mathf.Abs(player.position.y - pos.y) < 1.5f;

        modeTimer += dt;
        switch (mode)
        {
            case Mode.Move:
                if (playerInLeash && playerClose && toPlayer.magnitude < attackRange)
                {
                    SetMode(Mode.Windup);
                    return;
                }
                if (playerInLeash && playerClose)
                    Step(pos, player.position, chaseSpeed, dt);
                else
                {
                    Vector3 target = towardsB ? patrolB : patrolA;
                    if (Flat(target - pos).sqrMagnitude < 0.04f)
                        towardsB = !towardsB;
                    Step(pos, target, patrolSpeed, dt);
                }
                break;

            case Mode.Windup:
                if (modeTimer >= windupTime)
                {
                    SetMode(Mode.Spin);
                    if (blades != null) blades.gameObject.SetActive(true);
                }
                break;

            case Mode.Spin:
                // Drifts after the player while spinning, slower than walking
                if (player != null)
                    Step(pos, player.position, spinMoveSpeed, dt);
                if (playerDamage != null && player != null && Flat(player.position - body.position).magnitude < spinRadius
                    && Mathf.Abs(player.position.y - body.position.y) < 1.2f)
                    playerDamage.Hit(body.position, contactTimeCost);
                if (modeTimer >= spinTime)
                {
                    SetMode(Mode.Recover);
                    if (blades != null) blades.gameObject.SetActive(false);
                }
                break;

            case Mode.Recover:
                if (modeTimer >= recoverTime)
                    SetMode(Mode.Move);
                break;
        }
    }

    void SetMode(Mode m)
    {
        mode = m;
        modeTimer = 0f;
    }

    void Step(Vector3 pos, Vector3 target, float speed, float dt)
    {
        Vector3 to = Flat(target - pos);
        if (to.sqrMagnitude < 0.01f)
            return;
        body.MovePosition(pos + Vector3.ClampMagnitude(to.normalized * speed * dt, to.magnitude));
        moving = true;
        if (sprite != null && mode != Mode.Spin && Mathf.Abs(to.x) > 0.01f)
            sprite.flipX = to.x < 0f;
    }

    // The Hitbox child's trigger reports here (trigger messages go to the Rigidbody's object)
    void OnTriggerEnter(Collider other) => Touch(other);
    void OnTriggerStay(Collider other) => Touch(other);

    void Touch(Collider other)
    {
        if (IsDead || other.attachedRigidbody == null)
            return;
        var damage = other.attachedRigidbody.GetComponent<PlayerDamage>();
        if (damage == null || damage.IsOut)
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
        else if (damage.Hit(body.position, contactTimeCost))
            pauseLeft = pauseAfterHit;
    }

    public void Die()
    {
        if (IsDead)
            return;
        IsDead = true;
        if (blades != null) blades.gameObject.SetActive(false);
        if (sprite != null) sprite.color = baseColor * 0.6f;
        if (deathSparks != null) deathSparks.Burst();
        if (animator != null)
        {
            animator.SetBool("Dead", true);
            animator.speed = 1f;
        }
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;
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
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, spinRadius);
    }
}
