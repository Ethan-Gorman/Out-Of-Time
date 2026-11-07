using UnityEngine;

// A clock you collect to refill the golden time bar.
// Time is health (see Clock), so this is the health pickup.
// Bobs and spins in world time, so it slows down with the rest of the world.
public class ClockPickup : MonoBehaviour
{
    [Tooltip("Seconds put back on the clock.")]
    public float refillSeconds = 15f;

    [Tooltip("Ignore refillSeconds and fill the bar to the top.")]
    public bool refillToFull = false;

    [Tooltip("Never fill above the clock's starting time, so the bar can't overflow.")]
    public bool capAtStartTime = true;

    [Header("Idle animation")]
    public float bobHeight = 0.15f;
    public float bobSpeed = 3f;

    [Tooltip("The child holding the sprite. It bobs while this object (and its trigger) stays put.")]
    public Transform visual;

    // Optional sound when collected. Played at the pickup's spot, so it survives Destroy
    public AudioClip collectSound;

    float phase;
    Vector3 visualBase;

    void Awake()
    {
        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);
        if (visual != null)
            visualBase = visual.localPosition;

        // Offset each clock so a row of them doesn't bob in sync
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        if (visual == null)
            return;

        phase += WorldTime.Delta * bobSpeed;
        visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(phase) * bobHeight);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        var clock = Clock.Instance;
        if (clock == null || clock.IsOut)
            return;

        float max = clock.startSeconds;
        float amount = refillToFull ? max - clock.Remaining : refillSeconds;
        if (capAtStartTime)
            amount = Mathf.Min(amount, max - clock.Remaining);

        if (amount > 0f)
            clock.AddTime(amount);

        if (collectSound != null)
            AudioSource.PlayClipAtPoint(collectSound, transform.position);

        Destroy(gameObject);
    }
}
