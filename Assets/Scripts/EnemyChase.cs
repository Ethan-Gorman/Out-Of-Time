using UnityEngine;

// Walks towards the player in world time, so it slows down when the player stands still.
// Uses a kinematic Rigidbody: normal physics would ignore WorldTime.
[RequireComponent(typeof(Rigidbody))]
public class EnemyChase : MonoBehaviour
{
    // What to chase. Found automatically by the "Player" tag if left empty
    public Transform target;

    // Movement speed in units per second of world time
    public float speed = 3f;

    // The billboard sprite on the child "Sprite" object, flipped to face the player
    public SpriteRenderer sprite;

    // The run animation on the sprite child, slowed down with world time
    public Animator animator;

    Rigidbody body;

    void Awake()
    {
        body = GetComponent<Rigidbody>();

        // Kinematic = moved only by our code, never pushed around by physics
        body.isKinematic = true;

        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                target = player.transform;
        }
    }

    void Update()
    {
        // Slow the run animation with the rest of the world
        if (animator != null)
            animator.speed = WorldTime.Scale;
    }

    void FixedUpdate()
    {
        if (target == null)
            return;

        // Direction to the player along the ground only, ignoring height
        Vector3 toTarget = target.position - body.position;
        toTarget.y = 0f;

        // Close enough: stop, so it doesn't jitter on top of the player
        if (toTarget.sqrMagnitude < 0.01f)
            return;

        // FixedDelta (not Time.fixedDeltaTime) is what makes the enemy slow down with world time
        Vector3 step = toTarget.normalized * speed * WorldTime.FixedDelta;

        // MovePosition moves a kinematic body smoothly and still collides with the player
        body.MovePosition(body.position + step);

        if (sprite != null)
            sprite.flipX = toTarget.x < 0f;
    }
}
