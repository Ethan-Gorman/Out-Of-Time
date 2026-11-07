using UnityEngine;
using UnityEngine.InputSystem;

// Adds a jump to the player. Sits next to PlayerMovement, which already keeps the
// Rigidbody's vertical velocity, so all this script has to do is set it.
// Uses the project-wide "Player/Jump" action (Space / gamepad south button).
// Runs on normal time like the rest of the player: only the world slows down.
//
// Every jump is the same height: how long Jump is held doesn't matter.
// The jump follows Height Curve: height (0..1 of Jump Height) over the jump's time (0..1 of Jump Duration).
// The default takes off fast, slows to a stop at the top, then speeds up on the way down (gentler than gravity).
// It drives the Rigidbody's vertical velocity rather than its position, so walls, ceilings and
// ledges still collide normally. Landing on something higher ends the jump early; walking off a
// ledge mid-jump carries on falling with gravity once the curve runs out.
[RequireComponent(typeof(Rigidbody))]
public class MedievalJump : MonoBehaviour
{
    [Tooltip("Peak height of a jump, in world units.")]
    public float jumpHeight = 1.6f;

    [Tooltip("Seconds from take-off back down to the starting height.")]
    public float jumpDuration = 0.7f;

    [Tooltip("Height over time, both 0..1. Edit the shape in the Inspector: the peak's position sets how much of the jump is rising.")]
    public AnimationCurve heightCurve = DefaultCurve();

    [Tooltip("Downward acceleration while falling past the end of the jump curve (jumping or walking off something " +
             "taller), in units/s². 0 = keep the curve's own acceleration, so the fall carries on speeding up smoothly")]
    public float fallAcceleration = 0f;

    [Tooltip("Fastest the player can fall, in units/s")]
    public float maxFallSpeed = 20f;

    [Tooltip("Seconds after walking off a ledge where a jump still counts.")]
    public float coyoteTime = 0.1f;

    [Tooltip("Seconds a Jump press is remembered before landing, so early presses aren't lost.")]
    public float jumpBuffer = 0.12f;

    [Tooltip("What counts as ground. Leave as Everything unless you add layers.")]
    public LayerMask groundLayers = ~0;

    // True while standing on something. Read by GroundShadow and anything else that cares
    public bool IsGrounded { get; private set; }

    // True while the curve is driving the jump
    public bool IsJumping { get; private set; }

    Rigidbody body;
    CapsuleCollider capsule;
    PlayerMovement movement;
    InputAction jump;

    float lastGroundedTime = -1f;
    float lastJumpPressedTime = -1f;

    // Current jump: progress through the curve (0..1), where it started, and its height
    float progress;
    float startY;
    float height;
    float apex;

    // Vertical speed this script set last step, to notice when something else changes it (e.g. a stomp bounce)
    float lastSetSpeed;

    // Fast take-off that slows to a stop at the peak (45% of the way through), then keeps speeding up all the way down.
    // A real gravity arc would land at slope -3.6 here; -3.0 (about 83% of it) is gentler but still accelerates to the ground
    static AnimationCurve DefaultCurve() => new AnimationCurve(
        new Keyframe(0f, 0f, 3.2f, 3.2f),
        new Keyframe(0.45f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -3f, -3f));

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        movement = GetComponent<PlayerMovement>();
        jump = InputSystem.actions.FindAction("Player/Jump");
        apex = FindApex();
    }

    void OnDisable()
    {
        // Never leave the player without gravity (e.g. disabled when time runs out mid-jump)
        EndJump();
    }

    void Update()
    {
        // Read the press in Update so it is never missed between physics steps
        if (jump != null && jump.WasPressedThisFrame())
            lastJumpPressedTime = Time.time;
    }

    void FixedUpdate()
    {
        if (IsJumping)
        {
            StepJump();
            return;
        }

        IsGrounded = CheckGrounded();
        if (IsGrounded)
            lastGroundedTime = Time.time;

        bool buffered = Time.time - lastJumpPressedTime <= jumpBuffer;
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        if (buffered && canJump)
        {
            StartJump();
            return;
        }

        // Falling without a jump (off a ledge, or after the curve ended in mid-air): keep speeding up
        // at the fall rate instead of plain gravity, up to the speed limit
        if (!IsGrounded && body.linearVelocity.y < 0f)
        {
            float wanted = FallAcceleration();
            body.AddForce(Vector3.down * (wanted + Physics.gravity.y), ForceMode.Acceleration);
            if (body.linearVelocity.y < -maxFallSpeed)
                SetVerticalSpeed(-maxFallSpeed);
        }
    }

    // How fast the fall speeds up: the setting, or the jump curve's own average falling acceleration
    // (its landing speed divided by how long it falls), so falling past the curve feels the same
    float FallAcceleration()
    {
        if (fallAcceleration > 0f)
            return fallAcceleration;

        const float e = 0.01f;
        float landingSpeed = jumpHeight * (heightCurve.Evaluate(1f - e) - heightCurve.Evaluate(1f)) / (e * jumpDuration);
        float fallTime = Mathf.Max(0.05f, (1f - apex) * jumpDuration);
        float accel = landingSpeed / fallTime;
        return accel > 0.5f ? accel : -Physics.gravity.y;
    }

    void StartJump()
    {
        // Consume the press and the ground, so one press = one jump
        lastJumpPressedTime = -1f;
        lastGroundedTime = -1f;
        IsGrounded = false;

        IsJumping = true;
        progress = 0f;
        startY = body.position.y;
        height = jumpHeight;
        apex = FindApex();

        // The curve sets the vertical motion on its own
        body.useGravity = false;
        StepJump();
    }

    void StepJump()
    {
        // Knocked back by a hit: let physics take over
        if (movement != null && !movement.enabled)
        {
            EndJump();
            return;
        }

        // Something else launched us upwards (e.g. bouncing off a stomped skeleton): let that win
        if (progress > 0f && body.linearVelocity.y > lastSetSpeed + 1f)
        {
            EndJump();
            return;
        }

        float dt = Time.fixedDeltaTime;
        bool rising = progress < apex;

        // Hit a ceiling while rising: stop the jump and fall
        float expectedY = startY + height * heightCurve.Evaluate(progress);
        if (rising && progress > 0f && body.position.y < expectedY - 0.05f)
        {
            EndJump();
            return;
        }

        // Landed (on the starting ground or something higher) after the peak
        if (!rising && CheckGrounded())
        {
            IsGrounded = true;
            lastGroundedTime = Time.time;
            EndJump();
            return;
        }

        float next = progress + dt / Mathf.Max(0.05f, jumpDuration);
        if (next >= 1f)
        {
            // Curve finished in mid-air (jumped off a ledge): keep its final speed; FixedUpdate carries on accelerating
            float endSpeed = height * (heightCurve.Evaluate(1f) - heightCurve.Evaluate(1f - 0.01f)) / (0.01f * jumpDuration);
            SetVerticalSpeed(endSpeed);
            EndJump();
            return;
        }

        // Velocity that moves us to the curve's next height in exactly one physics step
        float targetY = startY + height * heightCurve.Evaluate(next);
        SetVerticalSpeed((targetY - body.position.y) / dt);
        progress = next;
    }

    void EndJump()
    {
        IsJumping = false;
        if (body != null)
            body.useGravity = true;
    }

    void SetVerticalSpeed(float vy)
    {
        Vector3 v = body.linearVelocity;
        v.y = vy;
        body.linearVelocity = v;
        lastSetSpeed = vy;
    }

    // Where the curve peaks (0..1), so we know when the jump switches from rising to falling
    float FindApex()
    {
        float best = 0f, bestValue = float.MinValue;
        for (int i = 0; i <= 100; i++)
        {
            float t = i / 100f;
            float value = heightCurve.Evaluate(t);
            if (value > bestValue)
            {
                bestValue = value;
                best = t;
            }
        }
        return best;
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
