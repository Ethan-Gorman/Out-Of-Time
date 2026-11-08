using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shows the Clock in the top-left corner on a panel with a clock face per era (UITheme): a sundial, an hourglass,
// a pocket watch or a digital ring, with the world time multiplier next to the time (e.g. ×0.42).
// Turns red and pulses when time is low. Flashes and pops up "-10s" / "+15s" when time is taken or given.
// When the level's objective is complete the clock turns gold and the time left and bests (RunTimer) appear under it.
// Sits on the "Clock" text object inside the HUD canvas. The plate, icon, popups and results are built at runtime
[RequireComponent(typeof(Text))]
public class ClockUI : MonoBehaviour
{
    // Below this many seconds the clock turns red and pulses
    public float warningSeconds = 10f;

    // Time multiplier: size relative to the clock
    [Range(0.2f, 1f)] public float multiplierSize = 0.55f;

    [Header("Time taken / given")]
    public float flashTime = 0.5f;
    public float popupTime = 0.9f;
    public float popupDrift = 32f;
    public int popupFontSize = 36;

    [Header("Level finished")]
    public int resultsFontSize = 32;

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
    Text results;
    RectTransform resultsPanel;
    Image plate, icon;
    Sprite[] iconFrames;
    UITheme theme;

    void Awake()
    {
        label = GetComponent<Text>();

        // The multiplier uses <size> and <color> tags inside the same label
        label.supportRichText = true;
        BuildPlate();
    }

    void OnEnable()
    {
        Clock.TimeChanged += OnTimeChanged;
        UITheme.Changed += ApplyTheme;
        ApplyTheme(UITheme.Current);
        ShowPlate(true);
    }

    void OnDisable()
    {
        Clock.TimeChanged -= OnTimeChanged;
        UITheme.Changed -= ApplyTheme;
        ShowPlate(false);
    }

    // The plate is a sibling, so it hides with the clock. Components rather than SetActive, which Unity
    // refuses while a hierarchy is being deactivated
    void ShowPlate(bool show)
    {
        if (plate != null)
            plate.enabled = show;
        if (icon != null)
            icon.enabled = show;
    }

    // The panel behind the time, with the era's clock icon at its left end
    void BuildPlate()
    {
        var own = (RectTransform)transform;
        var go = new GameObject("Clock Plate", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(own.parent, false);
        // Just before the time in the hierarchy, so it draws behind it
        rect.SetSiblingIndex(own.GetSiblingIndex());
        UILayout.TopLeft(rect, new Vector2(UILayout.Margin, UILayout.Margin), UILayout.ClockPlate);
        plate = go.GetComponent<Image>();
        plate.type = Image.Type.Sliced;
        plate.pixelsPerUnitMultiplier = UISprites.PixelMultiplier;
        plate.raycastTarget = false;

        var iconGo = new GameObject("Clock Icon", typeof(RectTransform), typeof(Image));
        var iconRect = (RectTransform)iconGo.transform;
        iconRect.SetParent(rect, false);
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(16f, 0f);
        iconRect.sizeDelta = new Vector2(UILayout.ClockIcon, UILayout.ClockIcon);
        icon = iconGo.GetComponent<Image>();
        icon.raycastTarget = false;
    }

    void ApplyTheme(UITheme t)
    {
        theme = t;
        if (plate != null)
            plate.sprite = UISprites.Panel(t);
        if (resultsPanel != null)
            resultsPanel.GetComponent<Image>().sprite = UISprites.Panel(t);
        iconFrames = UISprites.ClockFrames(t);
        // The new era's face straight away; Update picks the right frame next
        if (icon != null && iconFrames.Length > 0)
            icon.sprite = iconFrames[0];

        label.fontStyle = FontStyle.Normal;
        StyleOutline(label);
    }

    void StyleOutline(Text text)
    {
        var outline = text.GetComponent<Outline>();
        if (outline == null)
            outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.textOutline;
        outline.effectDistance = UILayout.TextOutline;
    }

    void OnTimeChanged(float delta)
    {
        if (theme == null)
            ApplyTheme(UITheme.Current);
        flash = 1f;
        flashColor = delta < 0f ? theme.negative : theme.positive;
        SpawnPopup(delta);
    }

    void Update()
    {
        if (theme == null)
            ApplyTheme(UITheme.Current);

        // Real time throughout: the HUD must keep animating while the world is frozen
        float dt = Time.unscaledDeltaTime;
        flash = Mathf.Max(0f, flash - dt / Mathf.Max(0.01f, flashTime));
        UpdatePopups(dt);

        var clock = Clock.Instance;
        if (clock == null)
            return;

        UpdateIcon(clock);

        if (clock.IsOut)
        {
            label.text = "OUT OF TIME";
            label.color = theme.negative;
            transform.localScale = Vector3.one;
            return;
        }

        // Format as m:ss.ss, e.g. 0:09.42
        float t = clock.Remaining;

        if (clock.IsFrozen)
        {
            label.text = RunTimer.Format(t);
            label.color = Color.Lerp(theme.accent, flashColor, flash);
            transform.localScale = Vector3.one;
            ShowResults();
            return;
        }

        string time = RunTimer.Format(t);
        // Future's digital readout blinks its colon. VT323 is monospaced, so the digits don't shift
        if (theme.blinkColon && Time.unscaledTime % 1f > 0.5f)
            time = time.Replace(":", $"<color=#{ColorUtility.ToHtmlStringRGBA(theme.textDim)}>:</color>");
        label.text = $"{time}  {Multiplier()}";

        bool low = t <= warningSeconds;
        label.color = Color.Lerp(low ? theme.negative : theme.clockDigits, flashColor, flash);

        // Pulse on real time so it still beats while the world is frozen, plus a punch when time changes
        float pulse = low ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
        pulse *= 1f + 0.2f * flash * flash;
        transform.localScale = Vector3.one * pulse;
    }

    // Picks the icon frame. Most faces drain with the share of the level's starting time left (Clock.StartSeconds,
    // which can differ per level); Steampunk's watch hand instead ticks once per clock second
    void UpdateIcon(Clock clock)
    {
        if (icon == null || iconFrames == null || iconFrames.Length == 0)
            return;
        int n = iconFrames.Length;
        int k;
        if (theme.clockTicks)
        {
            k = (n - Mathf.FloorToInt(Mathf.Max(0f, clock.Remaining)) % n) % n;
        }
        else
        {
            float f = clock.StartSeconds > 0f ? Mathf.Clamp01(clock.Remaining / clock.StartSeconds) : 0f;
            k = Mathf.Clamp(Mathf.FloorToInt((1f - f) * n), 0, n - 1);
        }
        icon.sprite = iconFrames[k];
        icon.color = clock.IsOut ? theme.negative : Color.white;
    }

    // Smaller, coloured "×0.42" showing how fast the clock is draining.
    // Blends towards the info colour as time slows, the sprint colour while sprinting
    string Multiplier()
    {
        float scale = WorldTime.Scale * WorldTime.ClockCost;
        Color c = Color.Lerp(
            Color.Lerp(theme.info, theme.textDim, WorldTime.Scale),
            theme.sprint,
            WorldTime.Sprint);
        int size = Mathf.RoundToInt(label.fontSize * multiplierSize);
        return $"<size={size}><color=#{ColorUtility.ToHtmlStringRGBA(c)}>×{scale:0.00}</color></size>";
    }

    // Level (and run, if it's over) time left under the clock on its own panel, with bests, and the real
    // time taken as a smaller, dimmer line. Built once, at runtime; it takes the Caveman inventory's place
    void ShowResults()
    {
        var level = RunTimer.LastLevel;
        if (level == null || results != null)
            return;

        var own = (RectTransform)transform;
        var panelGo = new GameObject("Results Panel", typeof(RectTransform), typeof(Image));
        resultsPanel = (RectTransform)panelGo.transform;
        resultsPanel.SetParent(own.parent, false);
        UILayout.TopLeft(resultsPanel, new Vector2(UILayout.Margin, 160f), new Vector2(UILayout.PanelWidth, 0f));
        var panel = panelGo.GetComponent<Image>();
        panel.sprite = UISprites.Panel(theme);
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = UISprites.PixelMultiplier;
        panel.raycastTarget = false;

        results = MakeText("Results", resultsFontSize, TextAnchor.UpperLeft, resultsPanel);
        var rect = results.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(UILayout.PadX, UILayout.PadY);
        rect.offsetMax = new Vector2(-UILayout.PadX, -UILayout.PadY);
        results.horizontalOverflow = HorizontalWrapMode.Wrap;
        results.verticalOverflow = VerticalWrapMode.Overflow;
        results.color = theme.text;

        string best = ColorUtility.ToHtmlStringRGBA(theme.positive);
        string dim = $"<size={UILayout.Small}><color=#{ColorUtility.ToHtmlStringRGBA(theme.textDim)}>";
        const string endDim = "</color></size>";
        // Bests are the most time left; cheated times aren't saved
        string text = $"TIME LEFT  {RunTimer.Format(level.clockLeft)}" +
            (level.cheated ? "  (cheats used)" :
             level.newBest ? $"  <color=#{best}>NEW BEST</color>" : $"  (best {RunTimer.Format(level.bestLeft)})");
        text += $"\n{dim}level took {RunTimer.Format(level.seconds)}";
        if (level.deaths > 0)
            text += $", {level.deaths} {(level.deaths == 1 ? "retry" : "retries")}";
        text += endDim;
        if (RunTimer.Complete)
        {
            text += $"\nRUN TIME LEFT  {RunTimer.Format(RunTimer.RunClockLeft)}" +
                (RunTimer.RunCheated ? "  (cheats used)" :
                 RunTimer.NewBestRun ? $"  <color=#{best}>NEW BEST</color>" : $"  (best {RunTimer.Format(RunTimer.BestRunLeft)})");
            text += $"\n{dim}run took {RunTimer.Format(RunTimer.RunSeconds)}{endDim}";
        }
        results.text = text;

        // The panel grows to fit the lines plus its top and bottom insets
        resultsPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(results.preferredHeight) + UILayout.PadY * 2f);
    }

    // A text in the clock's font and the shared outline style; the caller places its rect
    Text MakeText(string name, int fontSize, TextAnchor align, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = label.font;
        text.fontStyle = FontStyle.Normal;
        text.fontSize = fontSize;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;
        text.raycastTarget = false;
        StyleOutline(text);
        return text;
    }

    // A "-10s" / "+15s" text just right of the clock plate, built at runtime so the HUD prefab needs nothing extra
    void SpawnPopup(float delta)
    {
        var own = (RectTransform)transform;
        var text = MakeText("Time Popup", popupFontSize, TextAnchor.MiddleLeft, own.parent);
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(160f, 48f);
        // Level with the middle of the plate, just past its right edge
        rect.anchoredPosition = new Vector2(UILayout.Margin + UILayout.ClockPlate.x + UILayout.Gap, -(UILayout.Margin + UILayout.ClockPlate.y * 0.5f));
        Vector2 start = rect.anchoredPosition;
        text.text = delta < 0f ? $"-{-delta:0.#}s" : $"+{delta:0.#}s";
        text.color = delta < 0f ? theme.negative : theme.positive;

        // Losses sink, gains rise
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
