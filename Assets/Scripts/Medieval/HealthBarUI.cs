using UnityEngine;
using UnityEngine.UI;

// Shows the player's MedievalHealth as a bar. A lighter trail behind the fill shows what you just lost.
// Sits on the HealthBar object in the MedievalUI prefab.
public class HealthBarUI : MonoBehaviour
{
    [Tooltip("Found automatically in the scene if left empty.")]
    public MedievalHealth health;

    [Tooltip("Image with Image Type = Filled, Horizontal.")]
    public Image fill;
    [Tooltip("Optional lighter bar behind the fill that catches up slowly.")]
    public Image trail;
    [Tooltip("Optional text, e.g. 75 / 100.")]
    public Text label;

    public Color fullColor = new Color(0.9f, 0.2f, 0.2f);
    public Color lowColor = new Color(0.55f, 0.05f, 0.1f);
    public Color warningColor = new Color(1f, 0.85f, 0.85f);

    [Tooltip("Below this fraction of health the bar pulses.")]
    [Range(0f, 1f)] public float lowFraction = 0.3f;

    public float fillSpeed = 12f;
    public float trailSpeed = 2f;

    float shown = 1f;
    float trailShown = 1f;

    void Update()
    {
        if (health == null)
            health = FindAnyObjectByType<MedievalHealth>();
        if (health == null || fill == null)
            return;

        float target = health.Fraction;
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-fillSpeed * dt));
        trailShown = target > trailShown ? target : Mathf.Lerp(trailShown, target, 1f - Mathf.Exp(-trailSpeed * dt));

        fill.fillAmount = shown;
        if (trail != null)
            trail.fillAmount = trailShown;

        Color c = Color.Lerp(lowColor, fullColor, shown);
        if (target <= lowFraction && !health.IsDead)
            c = Color.Lerp(c, warningColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f));
        fill.color = c;

        if (label != null)
            label.text = $"{Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.maxHealth)}";
    }
}
