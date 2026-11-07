using UnityEngine;
using UnityEngine.UI;

// The player's health bar, which is really the time left on the Clock.
// A golden bar that drains as time runs out and refills when you collect a ClockPickup.
// Full = the clock's starting time. Sits on the bar's root object in the HUD.
public class GoldenTimeBar : MonoBehaviour
{
    [Tooltip("Image with Image Type = Filled, Horizontal. Shows the time left.")]
    public Image fill;

    [Tooltip("Optional lighter bar behind the fill that catches up slowly, so you can see what you just lost.")]
    public Image trail;

    [Tooltip("Optional number on the bar, e.g. 0:42.")]
    public Text label;

    [Header("Colors")]
    public Color fullColor = new Color(1f, 0.84f, 0.25f);   // bright gold
    public Color lowColor = new Color(0.85f, 0.45f, 0.1f);  // dark amber
    public Color warningColor = new Color(1f, 0.3f, 0.25f);

    [Tooltip("Below this many seconds the bar flashes.")]
    public float warningSeconds = 10f;

    [Tooltip("How quickly the bar slides to its new value. Higher = snappier.")]
    public float fillSpeed = 12f;
    public float trailSpeed = 2f;

    float shown = 1f;
    float trailShown = 1f;

    void Update()
    {
        var clock = Clock.Instance;
        if (clock == null || fill == null)
            return;

        float max = Mathf.Max(0.01f, clock.startSeconds);
        float target = Mathf.Clamp01(clock.Remaining / max);

        // Real (unscaled) time: the HUD should stay responsive while the world is slowed
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-fillSpeed * dt));

        // The trail jumps up instantly on refills, and only lags behind on losses
        trailShown = target > trailShown ? target : Mathf.Lerp(trailShown, target, 1f - Mathf.Exp(-trailSpeed * dt));

        fill.fillAmount = shown;
        if (trail != null)
            trail.fillAmount = trailShown;

        bool low = clock.Remaining <= warningSeconds && !clock.IsOut;
        Color c = Color.Lerp(lowColor, fullColor, shown);
        if (low)
            c = Color.Lerp(c, warningColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f));
        fill.color = c;

        if (label != null)
        {
            if (clock.IsOut)
                label.text = "OUT OF TIME";
            else
            {
                int total = Mathf.CeilToInt(clock.Remaining);
                label.text = $"{total / 60}:{total % 60:00}";
            }
        }
    }
}
