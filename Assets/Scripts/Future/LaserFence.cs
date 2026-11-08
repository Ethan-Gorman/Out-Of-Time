using UnityEngine;

// A laser fence across a walkway, on a cycle like the steam vents: quiet, a warning (beams flicker, lenses go amber),
// then solid beams that hurt the player. World time, so it only moves while the player does: stand still to read
// the rhythm, move to make it happen. PowerOff() shuts it down for good (Stabiliser B does).
// Put the Hazard (trigger, taller than a jump) on a child; Beams is the object holding the beam art.
public class LaserFence : MonoBehaviour
{
    public Hazard hazard;
    public GameObject beams;
    public Renderer[] lenses;
    public Light glow;

    [Header("Cycle, in seconds of world time")]
    public float quietTime = 1.6f;
    public float warnTime = 0.6f;
    public float onTime = 1.2f;
    [Range(0f, 1f)] public float phaseOffset = 0f;

    public bool powered = true;

    public Color offColor = new Color(0.2f, 0.08f, 0.1f);
    public Color warnColor = new Color(1f, 0.7f, 0.25f);
    public Color onColor = new Color(1f, 0.23f, 0.29f);
    public float glowIntensity = 2.2f;

    float timer;
    MaterialPropertyBlock block;

    float Cycle => quietTime + warnTime + onTime;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        timer = phaseOffset * Cycle;
        Apply();
    }

    void Update()
    {
        if (powered)
            timer = (timer + WorldTime.Delta) % Cycle;
        Apply();
    }

    public void PowerOff()
    {
        powered = false;
        Apply();
    }

    void Apply()
    {
        bool warning = powered && timer >= quietTime && timer < quietTime + warnTime;
        bool on = powered && timer >= quietTime + warnTime;

        if (hazard != null)
            hazard.armed = on;
        // Warning: the beams blink on every other 0.08 s of world time
        if (beams != null)
            beams.SetActive(on || (warning && Mathf.Repeat(timer, 0.16f) < 0.08f));
        if (glow != null)
            glow.intensity = on ? glowIntensity : warning ? glowIntensity * 0.35f : 0f;

        Color c = on ? onColor : warning ? warnColor : offColor;
        if (lenses != null)
            foreach (var lens in lenses)
            {
                if (lens == null)
                    continue;
                lens.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                block.SetColor("_EmissionColor", powered ? c * 1.5f : Color.black);
                lens.SetPropertyBlock(block);
            }
    }
}
