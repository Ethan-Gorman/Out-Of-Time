using UnityEngine;
using UnityEngine.InputSystem;

// Top-down movement on the XZ plane using the project-wide "Player/Move" action.
// The world is 3D, so "up" on the stick moves along Z (away from the camera), not Y.
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    // Movement speed in units per second
    public float speed = 5f;

    // The billboard sprite on the child "Sprite" object, flipped to face the move direction
    public SpriteRenderer sprite;

    // The Animator on the same "Sprite" child, using AcPlayer (Stand / Run / Dead).
    // Runs on normal time: only the world slows down, never the player
    public Animator animator;

    Rigidbody body;
    InputAction move;

    // Latest stick/WASD value, read in Update and applied in FixedUpdate
    Vector2 input;

    // Read by TimeController to decide how fast the world runs
    public Vector2 MoveInput => input;

    void Awake()
    {
        body = GetComponent<Rigidbody>();

        // Project-wide actions live in Assets/InputSystem_Actions.inputactions
        // (WASD, arrow keys and gamepad left stick are all bound to Player/Move)
        move = InputSystem.actions.FindAction("Player/Move");
    }

    void Update()
    {
        // Read input every frame so no key presses are missed between physics steps
        input = move != null ? move.ReadValue<Vector2>() : Vector2.zero;

        // Only flip when moving sideways, so the sprite keeps its last facing when moving up/down or idle
        if (sprite != null && Mathf.Abs(input.x) > 0.01f)
            sprite.flipX = input.x < 0f;

        // AcPlayer switches Stand -> Run when Speed > 0.01, and back when it drops below
        if (animator != null)
            animator.SetFloat("Speed", input.magnitude);
    }

    // Plays the death animation. Called by Clock when time runs out
    public void PlayDeath()
    {
        if (animator != null)
            animator.SetTrigger("Dead");
    }

    void FixedUpdate()
    {
        // Map 2D input onto the ground plane: input.x -> world X, input.y -> world Z
        Vector3 velocity = new Vector3(input.x, 0f, input.y) * speed;

        // Keep the current vertical velocity so gravity still works
        velocity.y = body.linearVelocity.y;

        // Setting velocity directly gives instant, snappy movement with no acceleration or drift
        body.linearVelocity = velocity;
    }
}
