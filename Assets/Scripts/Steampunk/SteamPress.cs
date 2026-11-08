using UnityEngine;

// A steam hammer / stamping press: the head waits up high, lowers a little and hisses as a warning,
// slams down (hurting anything under it), holds, then rises slowly. World time throughout.
// Put the Hazard (trigger) on the moving head.
public class SteamPress : MonoBehaviour
{
    public Transform head;
    public Hazard hazard;
    public SteamEmitter slamPuff;

    // How far the head drops from its raised (placed) position
    public float dropDistance = 1.6f;
    public float warnDrop = 0.25f;

    [Header("Cycle, in seconds of world time")]
    public float upTime = 1.6f;
    public float warnTime = 0.6f;
    public float slamTime = 0.12f;
    public float holdTime = 0.5f;
    public float riseTime = 0.9f;
    [Range(0f, 1f)] public float phaseOffset = 0f;

    Vector3 raised;
    float timer;
    bool slammed;

    float Cycle => upTime + warnTime + slamTime + holdTime + riseTime;

    void Awake()
    {
        if (head != null)
            raised = head.localPosition;
        timer = phaseOffset * Cycle;
    }

    void Update()
    {
        if (head == null)
            return;
        timer = (timer + WorldTime.Delta) % Cycle;

        float t = timer, drop;
        if (t < upTime) { drop = 0f; slammed = false; }
        else if ((t -= upTime) < warnTime) drop = warnDrop * Mathf.SmoothStep(0f, 1f, t / warnTime) + Mathf.Sin(t * 60f) * 0.015f;
        else if ((t -= warnTime) < slamTime) drop = Mathf.Lerp(warnDrop, dropDistance, t / slamTime);
        else if ((t -= slamTime) < holdTime)
        {
            drop = dropDistance;
            if (!slammed)
            {
                slammed = true;
                if (slamPuff != null) slamPuff.Burst();
            }
        }
        else drop = Mathf.Lerp(dropDistance, 0f, Mathf.SmoothStep(0f, 1f, (t - holdTime) / riseTime));

        head.localPosition = raised + Vector3.down * drop;
        if (hazard != null)
            hazard.armed = drop > dropDistance * 0.55f;
    }
}
