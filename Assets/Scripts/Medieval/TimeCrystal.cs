using UnityEngine;

// The Time Crystal: the Medieval Era's goal. Walking into it collects it and tells MedievalLevel.
// Spins and bobs in world time. Keep the art on the "Visual" child.
[RequireComponent(typeof(Collider))]
public class TimeCrystal : MonoBehaviour
{
    public Transform visual;
    public float spinDegreesPerSecond = 90f;
    public float bobHeight = 0.2f;
    public float bobSpeed = 2f;

    Vector3 visualBase;
    float phase;

    void Awake()
    {
        if (visual != null)
            visualBase = visual.localPosition;
    }

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
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

        if (MedievalLevel.Instance != null)
            MedievalLevel.Instance.CollectCrystal();
        Destroy(gameObject);
    }
}
