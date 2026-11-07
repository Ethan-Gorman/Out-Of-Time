using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shows the Clock in the top-left corner, with the world time multiplier next to it (e.g. ×0.42).
// Turns red and pulses when time is low. Flashes and pops up "-10s" / "+15s" when time is taken or given.
// Sits on the "Clock" text object inside the HUD canvas.
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
    public Color multiplierSprintColor = new Color(1f, 0.65f, 0.25f);

    [Header("Time taken / given")]
    public Color lossColor = new Color(1f, 0.15f, 0.15f);
    public Color gainColor = new Color(0.4f, 1f, 0.5f);
    [Tooltip("Seconds the clock flashes for")]
    public float flashTime = 0.5f;
    [Tooltip("Seconds the -10s / +15s popup lasts")]
    public float popupTime = 0.9f;
    [Tooltip("Pixels the popup drifts (down for a loss, up for a gain)")]
    public float popupDrift = 60f;
    [Range(0.2f, 1f)] public float popupSize = 0.7f;

    class Popup
    {
        public RectTransform rect;
        public Text text;
        public Vector2 start;
        public float direction;
        public float age;
    }

    readonly List<Popup> popups = new List<Popup>();
    Text label;
    float flash;
    Color flashColor;

    void Awake()
    {
        label = GetComponent<Text>();

        // The multiplier uses <size> and <color> tags inside the same label
        label.supportRichText = true;
    }

    void OnEnable() => Clock.TimeChanged += OnTimeChanged;
    void OnDisable() => Clock.TimeChanged -= OnTimeChanged;

    void OnTimeChanged(float delta)
    {
        flash = 1f;
        flashColor = delta < 0f ? lossColor : gainColor;
        SpawnPopup(delta);
    }

    void Update()
    {
        // Real time throughout: the HUD must keep animating while the world is frozen
        float dt = Time.unscaledDeltaTime;
        flash = Mathf.Max(0f, flash - dt / Mathf.Max(0.01f, flashTime));
        UpdatePopups(dt);

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
        label.color = Color.Lerp(low ? warningColor : normalColor, flashColor, flash);

        // Pulse on real time so it still beats while the world is frozen, plus a punch when time changes
        float pulse = low ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
        pulse *= 1f + 0.2f * flash * flash;
        transform.localScale = Vector3.one * pulse;
    }

    // Smaller, coloured "×0.42" showing how fast the clock is draining.
    // Blends towards blue as time slows, orange while sprinting
    string Multiplier()
    {
        float scale = WorldTime.Scale * WorldTime.ClockCost;
        Color c = Color.Lerp(
            Color.Lerp(multiplierSlowColor, multiplierFullColor, WorldTime.Scale),
            multiplierSprintColor,
            WorldTime.Sprint);
        int size = Mathf.RoundToInt(label.fontSize * multiplierSize);
        return $"<size={size}><color=#{ColorUtility.ToHtmlStringRGBA(c)}>×{scale:0.00}</color></size>";
    }

    // A "-10s" / "+15s" text just under the clock, built at runtime so the HUD prefab needs nothing extra
    void SpawnPopup(float delta)
    {
        var own = (RectTransform)transform;
        var go = new GameObject("Time Popup", typeof(RectTransform), typeof(Text), typeof(Outline));
        var rect = (RectTransform)go.transform;
        rect.SetParent(own.parent, false);
        rect.anchorMin = own.anchorMin;
        rect.anchorMax = own.anchorMax;
        rect.pivot = own.pivot;
        rect.sizeDelta = own.sizeDelta;

        // Losses start under the clock and sink; gains start lower and rise towards it
        float lineHeight = label.fontSize * 1.1f;
        Vector2 start = own.anchoredPosition + new Vector2(0f, delta < 0f ? -lineHeight : -lineHeight * 1.6f);
        rect.anchoredPosition = start;

        var text = go.GetComponent<Text>();
        text.font = label.font;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = Mathf.RoundToInt(label.fontSize * popupSize);
        text.alignment = label.alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = delta < 0f ? $"-{-delta:0.#}s" : $"+{delta:0.#}s";
        text.color = delta < 0f ? lossColor : gainColor;

        var outline = go.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        popups.Add(new Popup { rect = rect, text = text, start = start, direction = delta < 0f ? -1f : 1f });
    }

    void UpdatePopups(float dt)
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var p = popups[i];
            p.age += dt;
            float t = p.age / Mathf.Max(0.01f, popupTime);
            if (t >= 1f || p.rect == null)
            {
                if (p.rect != null)
                    Destroy(p.rect.gameObject);
                popups.RemoveAt(i);
                continue;
            }

            // Ease out the drift, pop in big, fade out towards the end
            float ease = 1f - (1f - t) * (1f - t);
            p.rect.anchoredPosition = p.start + new Vector2(0f, p.direction * popupDrift * ease);
            p.rect.localScale = Vector3.one * (1f + 0.4f * Mathf.Pow(1f - t, 4f));
            Color c = p.text.color;
            c.a = 1f - t * t;
            p.text.color = c;
        }
    }
}
