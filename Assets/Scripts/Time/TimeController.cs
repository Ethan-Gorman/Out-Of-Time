using UnityEngine;

// Drives WorldTime.Scale from how hard the player is moving.
// Sits on the Player next to PlayerMovement.
[RequireComponent(typeof(PlayerMovement))]
public class TimeController : MonoBehaviour
{
    [Tooltip("World speed while standing still. Above 0 so the world never fully stops.")]
    [Range(0f, 1f)] public float minScale = 0.02f;

    [Tooltip("How quickly time speeds up and slows down. Higher = snappier.")]
    public float responsiveness = 10f;

    PlayerMovement player;

    void Awake()
    {
        player = GetComponent<PlayerMovement>();
    }

    void OnDisable()
    {
        // Don't leave the world frozen if the player is removed or the scene reloads
        WorldTime.Scale = 1f;
    }

    void Update()
    {
        // Stick magnitude is 0..1, WASD is 0 or 1 (diagonals clamped to 1)
        float target = Mathf.Max(minScale, Mathf.Clamp01(player.MoveInput.magnitude));

        // Ease towards the target instead of snapping, so slow-mo ramps in and out.
        // The Exp form makes the easing feel the same at any frame rate.
        // Uses real Time.deltaTime: world time must not slow down its own controller
        WorldTime.Scale = Mathf.Lerp(WorldTime.Scale, target, 1f - Mathf.Exp(-responsiveness * Time.deltaTime));
    }
}
