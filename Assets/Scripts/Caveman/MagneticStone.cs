using UnityEngine;

public class MagneticStone : MonoBehaviour
{
    public Transform visual;
    public float floatHeight = 0.12f;
    public float floatSpeed = 1.5f;

    Vector3 visualBase;
    bool collected;
    float phase;

    void Awake()
    {
        if (visual != null)
            visualBase = visual.localPosition;
    }

    void Update()
    {
        if (visual == null || collected)
            return;

        phase += WorldTime.Delta * floatSpeed;
        float offset = Mathf.Sin(phase) * floatHeight;
        visual.localPosition = visualBase + Vector3.up * offset;
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected || other.attachedRigidbody == null || other.attachedRigidbody.GetComponent<MedievalInteractor>() == null)
            return;

        var level = CavemanLevel.Instance;
        if (level == null)
            return;

        collected = true;
        Vector3 at = visual != null ? visual.position : transform.position;
        CavemanFx.Play(CavemanSfx.Sound.Crystal);
        CavemanFx.Shake(0.2f);
        CavemanFx.Debris(at, CavemanFx.MaterialsOf(this), 8, 0.07f, 2f);
        level.CollectMagneticStone();
        CavemanFx.Text(at + Vector3.up * 0.8f, $"Magnet {level.MagneticStones}/{level.magneticStonesNeeded}", new Color(0.6f, 0.9f, 1f));
        Destroy(gameObject);
    }
}