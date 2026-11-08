using UnityEngine;

// Spins this object around a local axis while Running: gears, flywheels, rollers, drive shafts.
// Eases up to speed and back down, in world time, so machinery slows when the player stands still.
// Leave Running off for a stopped machine and call SetRunning(true) to start it.
public class GearSpinner : MonoBehaviour
{
    public bool running = true;

    // Degrees per second at full speed. Negative turns the other way
    public float degreesPerSecond = 45f;

    public Vector3 axis = Vector3.forward;

    // Seconds (world time) to reach full speed from a stop
    public float spinUpTime = 1.2f;

    // Optional: copies another spinner's speed (times Ratio) instead of its own, for meshing gears
    public GearSpinner follow;
    public float ratio = -1f;

    // 0..1 of full speed right now
    public float Speed01 { get; private set; }

    // Degrees turned this frame (world time), for chains and rods that ride on the gear
    public float DeltaDegrees { get; private set; }

    public float CurrentDegreesPerSecond => follow != null ? follow.CurrentDegreesPerSecond * ratio : degreesPerSecond * Speed01;

    void Awake()
    {
        Speed01 = running ? 1f : 0f;
    }

    public void SetRunning(bool on) => running = on;

    void Update()
    {
        float dt = WorldTime.Delta;
        if (follow == null)
        {
            float rate = spinUpTime > 0f ? dt / spinUpTime : 1f;
            Speed01 = Mathf.MoveTowards(Speed01, running ? 1f : 0f, rate);
        }
        DeltaDegrees = CurrentDegreesPerSecond * dt;
        if (DeltaDegrees != 0f)
            transform.Rotate(axis, DeltaDegrees, Space.Self);
    }
}
