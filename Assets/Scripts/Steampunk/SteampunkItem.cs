using UnityEngine;

// A story item the player walks into: the spare iron chain or the oil can. Tells SteampunkLevel and disappears.
// Bobs and turns in world time so it catches the eye. Keep the art on the "Visual" child.
[RequireComponent(typeof(Collider))]
public class SteampunkItem : MonoBehaviour
{
    public SteampunkLevel.Item item;
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
        if (SteampunkLevel.Instance != null)
            SteampunkLevel.Instance.Collect(item);
        Destroy(gameObject);
    }
}
