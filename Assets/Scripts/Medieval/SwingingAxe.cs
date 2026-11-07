using UnityEngine;

// Swings like a pendulum around this object's position (the pivot at the top).
// Put the blade, with a Hazard, on a child hanging below. Runs in world time.
public class SwingingAxe : MonoBehaviour
{
    [Tooltip("Furthest swing either side, in degrees.")]
    public float maxAngle = 65f;

    [Tooltip("Full swings per second of world time.")]
    public float swingsPerSecond = 0.45f;

    [Tooltip("Where in the swing it starts, 0-1. Offset neighbouring axes so there's a gap to run through.")]
    [Range(0f, 1f)] public float phaseOffset = 0f;

    [Tooltip("Local axis it swings around.")]
    public Vector3 axis = Vector3.right;

    Quaternion baseRotation;
    float phase;

    void Awake()
    {
        baseRotation = transform.localRotation;
        phase = phaseOffset;
        Apply();
    }

    void Update()
    {
        phase += WorldTime.Delta * swingsPerSecond;
        Apply();
    }

    void Apply()
    {
        float angle = Mathf.Sin(phase * Mathf.PI * 2f) * maxAngle;
        transform.localRotation = baseRotation * Quaternion.AngleAxis(angle, axis);
    }
}
