using UnityEngine;
using UnityEngine.InputSystem;

// Top-down movement on the XZ plane using the project-wide "Player/Move" action.
// The world is 3D, so "up" on the stick moves along Z (away from the camera), not Y.
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    // Movement speed in units per second
    public float speed = 5f;

    // Acceleration and friction in one: seconds to go from standing to normal top speed.
    // The same rate is used for braking, turning, and easing into and out of a sprint.
    // World time follows the player's speed, so this is also how long slow-mo takes to kick in
    public float accelerationTime = 0.1f;

    [Header("Sprint (hold Left Shift / left stick press)")]
    // Top speed multiplier while sprinting. The clock cost lives on TimeController
    public float sprintSpeedMultiplier = 1.5f;

    // The billboard sprite on the child "Sprite" object, flipped to face the move direction
    public SpriteRenderer sprite;

    [SerializeField, Tooltip("Optional sprite sheet whose unflipped artwork faces left.")]
    Texture2D leftFacingSpriteSheet;
    // More left-facing sheets, for looks whose clips use several (e.g. Caveman's idle, walk and death sheets).
    // Every sheet the player uses must face the same way after flipping, or the sprite mirrors on start/stop
    [SerializeField] Texture2D[] moreLeftFacingSpriteSheets;
    bool facingLeft;

    // The Animator on the same "Sprite" child, using AcPlayer (Stand / Run / Dead)
    public Animator animator;

    [Range(0f, 1f)] public float slowestAnimationSpeed = 0.15f;

    Rigidbody body;
    InputAction move;
    InputAction sprint;

    // Latest stick/WASD value, read in Update and applied in FixedUpdate
    Vector2 input;

    // Current ground velocity, easing towards the input direction at the acceleration rate
    Vector3 planarVelocity;

    // How fast the player actually moved over the last physics step, along the ground.
    // Walking into a wall gives ~0 here even though planarVelocity says full speed
    float measuredSpeed;
    Vector3 lastPosition;

    public Vector2 MoveInput => input;

    // Sprint held while actually trying to move: sets the sprint top speed as the target
    public bool IsSprinting { get; private set; }

    // 0 = not moving, 1 = normal top speed (or faster). Based on actual movement, not input,
    // so pushing against a wall counts as standing still. Read by TimeController to decide how fast the world runs
    public float SpeedFraction => speed > 0f ? Mathf.Clamp01(measuredSpeed / speed) : 0f;

    // 0 = at or below normal top speed, 1 = full sprint speed. Also from actual movement,
    // so the extra clock cost, drum boost and animation speed ease in and out with the player
    public float SprintAmount
    {
        get
        {
            float extra = speed * (sprintSpeedMultiplier - 1f);
            return extra > 0f ? Mathf.Clamp01((measuredSpeed - speed) / extra) : 0f;
        }
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();

        // Project-wide actions live in Assets/InputSystem_Actions.inputactions
        // (WASD, arrow keys and gamepad left stick are all bound to Player/Move)
        move = InputSystem.actions.FindAction("Player/Move");
        sprint = InputSystem.actions.FindAction("Player/Sprint");

        // No friction on the player's colliders: with friction, pushing into a wall grips it,
        // so you can't slide along it diagonally or jump while touching it.
        // Movement sets velocity directly, so the player still stops instantly on the ground
        var slippery = new PhysicsMaterial("Player (no friction)")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };
        foreach (var col in GetComponents<Collider>())
            if (!col.isTrigger)
                col.sharedMaterial = slippery;

        Application.onBeforeRender += FaceWhileDisabled;
    }

    // Re-enabled after a knockback: carry on from the actual velocity instead of the old one
    void OnEnable()
    {
        if (body == null)
            return;
        planarVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        lastPosition = body.position;
    }

    void Update()
    {
        // Read input every frame so no key presses are missed between physics steps
        input = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
        IsSprinting = sprint != null && sprint.IsPressed() && input.sqrMagnitude > 0.01f;

        // Keep the last horizontal facing when moving up/down or idle.
        if (Mathf.Abs(input.x) > 0.01f)
            facingLeft = input.x < 0f;

        // AcPlayer switches Stand -> Run when Speed > 0.01, and back when it drops below.
        // Uses actual speed so the run animation lasts until the player has slowed to a stop
        if (animator != null)
        {
            animator.SetFloat("Speed", SpeedFraction);

            // Legs speed up as the sprint builds, and the whole animation slows down with the world
            // (movement itself stays full speed: only how the player looks slows)
            float worldTime = Mathf.Max(WorldTime.Scale, slowestAnimationSpeed);
            animator.speed = Mathf.Lerp(1f, sprintSpeedMultiplier, SprintAmount) * worldTime;
        }
    }

    void LateUpdate()
    {
        ApplyFacing();
    }

    // Clock and knockback disable this script, which stops LateUpdate, but the animator keeps
    // swapping sprites (e.g. to a death sheet), so keep the facing right just before rendering
    void FaceWhileDisabled()
    {
        if (!enabled)
            ApplyFacing();
    }

    void OnDestroy()
    {
        Application.onBeforeRender -= FaceWhileDisabled;
    }

    void ApplyFacing()
    {
        if (sprite == null)
            return;
        sprite.flipX = facingLeft ^ ArtworkFacesLeft(sprite.sprite);
    }

    bool ArtworkFacesLeft(Sprite current)
    {
        if (current == null)
            return false;
        Texture2D sheet = current.texture;
        if (leftFacingSpriteSheet != null && sheet == leftFacingSpriteSheet)
            return true;
        if (moreLeftFacingSpriteSheets != null)
            foreach (var other in moreLeftFacingSpriteSheets)
                if (other != null && sheet == other)
                    return true;
        return false;
    }

    // Plays the death animation. Called by Clock when time runs out
    public void PlayDeath()
    {
        if (animator == null)
            return;
        // World time is frozen when time runs out, so play the death at normal speed
        animator.speed = 1f;
        animator.SetTrigger("Dead");
    }

    void FixedUpdate()
    {
        // Measure how far we really moved since the last step, after collisions were resolved
        Vector3 moved = body.position - lastPosition;
        moved.y = 0f;
        measuredSpeed = moved.magnitude / Time.fixedDeltaTime;
        lastPosition = body.position;

        // Map 2D input onto the ground plane: input.x -> world X, input.y -> world Z
        float topSpeed = IsSprinting ? speed * sprintSpeedMultiplier : speed;
        Vector3 target = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f) * topSpeed;

        // Ease towards the target at one constant rate (acceleration = friction),
        // so 0 -> normal top speed takes exactly accelerationTime, and sprint builds at the same rate
        float step = accelerationTime > 0f ? speed / accelerationTime * Time.fixedDeltaTime : float.MaxValue;
        planarVelocity = Vector3.MoveTowards(planarVelocity, target, step);

        // Keep the current vertical velocity so gravity still works
        body.linearVelocity = new Vector3(planarVelocity.x, body.linearVelocity.y, planarVelocity.z);
    }
}
