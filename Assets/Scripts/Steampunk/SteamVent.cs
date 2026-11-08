using UnityEngine;

// A floor grate or pipe that vents steam on a cycle: quiet, a warning hiss (thin wisps, glowing valve),
// then a scalding burst that hurts the player. World time, so standing still lets the player read the rhythm.
// Put the Hazard (trigger) on a child over the vent; the steam is a SteamEmitter.
public class SteamVent : MonoBehaviour
{
    public Hazard hazard;
    public SteamEmitter steam;
    public Renderer warningLight;
    public Light glow;

    [Header("Cycle, in seconds of world time")]
    public float quietTime = 2f;
    public float warnTime = 0.8f;
    public float burstTime = 1.2f;
    [Range(0f, 1f)] public float phaseOffset = 0f;

    [Header("Steam amounts (particles per second)")]
    public float warnRate = 10f;
    public float burstRate = 70f;
    public float burstSpeed = 5.5f;
    public float warnSpeed = 1.2f;

    public Color warningColor = new Color(1f, 0.45f, 0.15f);

    float timer;
    MaterialPropertyBlock block;

    float Cycle => quietTime + warnTime + burstTime;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        timer = phaseOffset * Cycle;
        Apply();
    }

    void Update()
    {
        timer = (timer + WorldTime.Delta) % Cycle;
        Apply();
    }

    void Apply()
    {
        bool warning = timer >= quietTime && timer < quietTime + warnTime;
        bool bursting = timer >= quietTime + warnTime;

        if (hazard != null)
            hazard.armed = bursting;

        if (steam != null)
        {
            steam.speed = bursting ? burstSpeed : warnSpeed;
            steam.rate = bursting ? burstRate : warning ? warnRate : 0f;
        }

        float heat = bursting ? 1f : warning ? 0.5f + 0.5f * Mathf.Sin(timer * 25f) : 0f;
        if (glow != null)
            glow.intensity = heat * 2.2f;
        if (warningLight != null)
        {
            warningLight.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Color.Lerp(new Color(0.25f, 0.2f, 0.18f), warningColor, heat));
            warningLight.SetPropertyBlock(block);
        }
    }
}
