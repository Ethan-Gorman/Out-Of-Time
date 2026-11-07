using UnityEngine;

// Global time for everything except the player.
// Scale is 1 when the player moves at full speed and near 0 when they stand still.
// Enemies, bullets and effects should use WorldTime.Delta instead of Time.deltaTime.
// Don't use Time.timeScale for this: it would slow the player down too.
public static class WorldTime
{
    // 0 = frozen, 1 = normal speed. Set every frame by TimeController
    public static float Scale = 1f;

    // Extra multiplier on how fast the Clock drains (up to 2 at full sprint). Doesn't speed up the world.
    // Set every frame by TimeController
    public static float ClockCost = 1f;

    // 0..1: how far into a sprint the player is. Eases with the player's speed. Set by TimeController
    public static float Sprint;

    // Use in Update: how much world time passed this frame
    public static float Delta => Time.deltaTime * Scale;

    // Use in FixedUpdate: how much world time passed this physics step
    public static float FixedDelta => Time.fixedDeltaTime * Scale;

    // Statics survive between play sessions when domain reload is off, so reset on play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Scale = 1f;
        ClockCost = 1f;
        Sprint = 0f;
    }
}
