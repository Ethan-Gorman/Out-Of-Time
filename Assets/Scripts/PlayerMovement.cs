using UnityEngine;
using UnityEngine.InputSystem;

// Top-down movement on the XZ plane using the project-wide "Player/Move" action.
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public SpriteRenderer sprite;

    Rigidbody body;
    InputAction move;
    Vector2 input;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        move = InputSystem.actions.FindAction("Player/Move");
    }

    void Update()
    {
        input = move != null ? move.ReadValue<Vector2>() : Vector2.zero;

        if (sprite != null && Mathf.Abs(input.x) > 0.01f)
            sprite.flipX = input.x < 0f;
    }

    void FixedUpdate()
    {
        Vector3 velocity = new Vector3(input.x, 0f, input.y) * speed;
        velocity.y = body.linearVelocity.y;
        body.linearVelocity = velocity;
    }
}
