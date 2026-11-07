using UnityEngine;

// Spikes that hide, poke up a little as a warning, then shoot up and hurt the player.
// Runs in world time, so standing still slows the cycle and lets the player time a run.
public class SpikeTrap : MonoBehaviour
{
    [Tooltip("The moving part. Its starting position is the 'raised' position.")]
    public Transform spikes;

    [Tooltip("Armed only while the spikes are up.")]
    public Hazard hazard;

    public float hiddenDepth = 0.7f;
    public float warnDepth = 0.5f;

    [Header("Cycle, in seconds of world time")]
    public float downTime = 1.4f;
    public float warnTime = 0.5f;
    public float upTime = 1.0f;

    [Tooltip("Units per second of world time the spikes move.")]
    public float moveSpeed = 10f;

    [Range(0f, 1f)] public float phaseOffset = 0f;

    Vector3 raisedPosition;
    float timer;
    float depth;

    float Cycle => downTime + warnTime + upTime;

    void Awake()
    {
        if (spikes == null)
            return;
        raisedPosition = spikes.localPosition;
        timer = phaseOffset * Cycle;
        depth = hiddenDepth;
        Apply();
    }

    void Update()
    {
        if (spikes == null)
            return;

        timer = (timer + WorldTime.Delta) % Cycle;

        float target = timer < downTime ? hiddenDepth
                     : timer < downTime + warnTime ? warnDepth
                     : 0f;
        depth = Mathf.MoveTowards(depth, target, moveSpeed * WorldTime.Delta);
        Apply();
    }

    void Apply()
    {
        spikes.localPosition = raisedPosition + Vector3.down * depth;
        if (hazard != null)
            hazard.armed = depth < hiddenDepth * 0.4f;
    }
}
