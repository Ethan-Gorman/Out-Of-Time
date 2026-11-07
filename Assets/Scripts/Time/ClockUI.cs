using UnityEngine;
using UnityEngine.UI;

// Shows the Clock in the top-left corner, with the world time multiplier next to it (e.g. ×0.42).
// Turns red and pulses when time is low. Sits on the "Clock" text object inside the HUD canvas.
[RequireComponent(typeof(Text))]
public class ClockUI : MonoBehaviour
{
    // Below this many seconds the clock turns red and pulses
    public float warningSeconds = 10f;
    public Color normalColor = Color.white;
    public Color warningColor = new Color(1f, 0.3f, 0.3f);

    // Time multiplier: size relative to the clock, and colours for full speed vs slowed
    [Range(0.2f, 1f)] public float multiplierSize = 0.55f;
    public Color multiplierFullColor = new Color(1f, 1f, 1f, 0.6f);
    public Color multiplierSlowColor = new Color(0.45f, 0.8f, 1f);

    Text label;

    void Awake()
    {
        label = GetComponent<Text>();

        // The multiplier uses <size> and <color> tags inside the same label
        label.supportRichText = true;
    }

    void Update()
    {
        var clock = Clock.Instance;
        if (clock == null)
            return;

        if (clock.IsOut)
        {
            label.text = "OUT OF TIME";
            label.color = warningColor;
            transform.localScale = Vector3.one;
            return;
        }

        // Format as m:ss.ss, e.g. 0:09.42
        float t = clock.Remaining;
        int minutes = (int)(t / 60f);
        float seconds = t % 60f; // 0-59.999
        label.text = $"{minutes}:{seconds:00.00}  {Multiplier()}";

        bool low = t <= warningSeconds;
        label.color = low ? warningColor : normalColor;

        // Pulse on real time so it still beats while the world is frozen
        float pulse = low ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
        transform.localScale = Vector3.one * pulse;
    }

    // Smaller, coloured "×0.42" showing how fast the world is running. Blends towards blue as time slows
    string Multiplier()
    {
        float scale = WorldTime.Scale;
        Color c = Color.Lerp(multiplierSlowColor, multiplierFullColor, scale);
        int size = Mathf.RoundToInt(label.fontSize * multiplierSize);
        return $"<size={size}><color=#{ColorUtility.ToHtmlStringRGBA(c)}>×{scale:0.00}</color></size>";
    }
}
