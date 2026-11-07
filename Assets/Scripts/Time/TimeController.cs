using UnityEngine;

// Drives WorldTime.Scale from how fast the player is moving.
// Sits on the Player next to PlayerMovement.
[RequireComponent(typeof(PlayerMovement))]
public class TimeController : MonoBehaviour
{
    [Tooltip("World speed while standing still. Above 0 so the world never fully stops.")]
    [Range(0f, 1f)] public float minScale = 0.05f;

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
        // Follows the player's actual speed (0..1). PlayerMovement already eases that over
        // accelerationTime, so slow-mo ramps in and out over the same 0.4s with no extra smoothing
        WorldTime.Scale = Mathf.Max(minScale, player.SpeedFraction);
    }
}
