using UnityEngine;

// A story item the player walks into (the Core Hall keycard). Tells FutureLevel and disappears.
// Bobs and turns in world time so it catches the eye. Keep the art on the "Visual" child.
[RequireComponent(typeof(Collider))]
public class FutureItem : MonoBehaviour
{
    public FutureLevel.Item item;
    public Transform visual;
    public float spinDegreesPerSecond = 70f;
    public float bobHeight = 0.12f;
    public float bobSpeed = 2.2f;

    Vector3 visualBase;
    float phase;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        if (visual != null)
            visualBase = visual.localPosition;
        // InteractOutline only looks for its own list of types, so ask for the gold outline here
        InteractOutline.Add(this);
    }

    void Update()
    {
        if (visual == null)
            return;
        phase += WorldTime.Delta * bobSpeed;
        visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(phase) * bobHeight);
        visual.Rotate(0f, spinDegreesPerSecond * WorldTime.Delta, 0f, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody == null || !other.attachedRigidbody.CompareTag("Player"))
            return;
        if (FutureLevel.Instance != null)
            FutureLevel.Instance.Collect(item);
        Destroy(gameObject);
    }
}
