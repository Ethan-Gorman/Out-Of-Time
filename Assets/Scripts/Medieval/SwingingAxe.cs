using UnityEngine;

// Swings like a pendulum around this object's position (the pivot at the top).
// Put the blade, with a Hazard, on a child hanging below. Runs in world time.
public class SwingingAxe : MonoBehaviour
{
    public float maxAngle = 65f;

    public float swingsPerSecond = 0.45f;

    [Range(0f, 1f)] public float phaseOffset = 0f;

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
