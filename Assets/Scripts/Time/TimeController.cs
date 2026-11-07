using UnityEngine;

// Drives WorldTime.Scale from how fast the player is moving.
// Sits on the Player next to PlayerMovement.
[RequireComponent(typeof(PlayerMovement))]
public class TimeController : MonoBehaviour
{
    [Range(0f, 1f)] public float minScale = 0.05f;

    public float sprintTimeCost = 2f;

    PlayerMovement player;

    void Awake()
    {
        player = GetComponent<PlayerMovement>();
    }

    void OnDisable()
    {
        // Don't leave the world frozen if the player is removed or the scene reloads
        WorldTime.Scale = 1f;
        WorldTime.ClockCost = 1f;
        WorldTime.Sprint = 0f;
    }

    void Update()
    {
        // Follows the player's actual speed (0..1). PlayerMovement already eases that over
        // accelerationTime, so slow-mo ramps in and out at the same rate with no extra smoothing
        WorldTime.Scale = Mathf.Max(minScale, player.SpeedFraction);

        // Sprinting costs extra clock time (but doesn't speed up enemies), building up with the sprint speed
        WorldTime.Sprint = player.SprintAmount;
        WorldTime.ClockCost = Mathf.Lerp(1f, sprintTimeCost, WorldTime.Sprint);
    }
}
