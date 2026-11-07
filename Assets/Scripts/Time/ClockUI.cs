using UnityEngine;
using UnityEngine.UI;

// Shows the Clock in the top-left corner. Turns red and pulses when time is low.
// Sits on the "Clock" text object inside the HUD canvas.
[RequireComponent(typeof(Text))]
public class ClockUI : MonoBehaviour
{
    // Below this many seconds the clock turns red and pulses
    public float warningSeconds = 10f;
    public Color normalColor = Color.white;
    public Color warningColor = new Color(1f, 0.3f, 0.3f);

    Text label;

    void Awake()
    {
        label = GetComponent<Text>();
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

        // Format as m:ss.s, e.g. 0:09.4
        float t = clock.Remaining;
        int minutes = (int)(t / 60f);
        float seconds = t % 60f;
        label.text = $"{minutes}:{seconds:00.0}";

        bool low = t <= warningSeconds;
        label.color = low ? warningColor : normalColor;

        // Pulse on real time so it still beats while the world is frozen
        float pulse = low ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
        transform.localScale = Vector3.one * pulse;
    }
}
