using UnityEngine;
using UnityEngine.InputSystem;

// Adds a jump to the player. Sits next to PlayerMovement, which already keeps the
// Rigidbody's vertical velocity, so all this script has to do is set it.
// Uses the project-wide "Player/Jump" action (Space / gamepad south button).
// Runs on normal time like the rest of the player: only the world slows down.
[RequireComponent(typeof(Rigidbody))]
public class MedievalJump : MonoBehaviour
{
    [Tooltip("Peak height of a jump, in world units.")]
    public float jumpHeight = 1.6f;

    [Tooltip("Extra gravity while falling, so jumps feel snappy instead of floaty. 1 = normal gravity.")]
    public float fallGravityMultiplier = 2f;

    [Tooltip("Extra gravity when Jump is released early, for short hops. 1 = no short hops.")]
    public float lowJumpGravityMultiplier = 2.5f;

    [Tooltip("Seconds after walking off a ledge where a jump still counts.")]
    public float coyoteTime = 0.1f;

    [Tooltip("Seconds a Jump press is remembered before landing, so early presses aren't lost.")]
    public float jumpBuffer = 0.12f;

    [Tooltip("What counts as ground. Leave as Everything unless you add layers.")]
    public LayerMask groundLayers = ~0;

    // True while standing on something. Read by GroundShadow and anything else that cares
    public bool IsGrounded { get; private set; }

    Rigidbody body;
    CapsuleCollider capsule;
    InputAction jump;

    float lastGroundedTime = -1f;
    float lastJumpPressedTime = -1f;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        jump = InputSystem.actions.FindAction("Player/Jump");
    }

    void Update()
    {
        // Read the press in Update so it is never missed between physics steps
        if (jump != null && jump.WasPressedThisFrame())
            lastJumpPressedTime = Time.time;
    }

    void FixedUpdate()
    {
        IsGrounded = CheckGrounded();
        if (IsGrounded)
            lastGroundedTime = Time.time;

        bool buffered = Time.time - lastJumpPressedTime <= jumpBuffer;
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;

        if (buffered && canJump)
        {
            // v = sqrt(2gh) gives exactly jumpHeight at the top of the arc
            Vector3 v = body.linearVelocity;
            v.y = Mathf.Sqrt(2f * -Physics.gravity.y * jumpHeight);
            body.linearVelocity = v;

            // Consume the press and the ground, so one press = one jump
            lastJumpPressedTime = -1f;
            lastGroundedTime = -1f;
            IsGrounded = false;
            return;
        }

        // Better-feeling arc: fall faster, and cut the jump short when the button is released
        float vy = body.linearVelocity.y;
        if (vy < 0f)
            body.AddForce(Physics.gravity * (fallGravityMultiplier - 1f), ForceMode.Acceleration);
        else if (vy > 0f && (jump == null || !jump.IsPressed()))
            body.AddForce(Physics.gravity * (lowJumpGravityMultiplier - 1f), ForceMode.Acceleration);
    }

    bool CheckGrounded()
    {
        // Cast a slightly thinner sphere down from inside the capsule's bottom.
        // A cast that starts inside our own collider ignores it, so no self-hits.
        float radius = capsule != null ? capsule.radius * 0.9f : 0.25f;
        Vector3 center = capsule != null ? transform.TransformPoint(capsule.center) : transform.position + Vector3.up * 0.5f;
        float halfHeight = capsule != null ? capsule.height * 0.5f : 0.5f;
        Vector3 origin = center + Vector3.down * (halfHeight - radius - 0.02f);

        // Only count ground when not already flying upwards from a jump
        if (body.linearVelocity.y > 0.1f)
            return false;

        return Physics.SphereCast(origin, radius, Vector3.down, out _, 0.12f, groundLayers, QueryTriggerInteraction.Ignore);
    }
}
