using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Small text that pops up over a spot in the world and floats away, e.g. "+5s" over a defeated skeleton.
// Same look as the boar's reward popup (CavemanFx), without the rest of the Caveman effects.
// Call FloatText.Spawn from anywhere: it makes its own overlay canvas the first time.
// Lives in the scene (not DontDestroyOnLoad), so it goes away with the level.
public class FloatText : MonoBehaviour
{
    // Colour of time gained, matching the boar's popup
    public static readonly Color GainColor = new Color(0.55f, 1f, 0.55f);

    public float riseSpeed = 60f;
    public float lifetime = 1.1f;
    public int fontSize = 22;

    struct Item
    {
        public RectTransform rect;
        public Text text;
        public Vector3 world;
        public Color color;
        public float age;
    }

    readonly List<Item> items = new List<Item>();
    Canvas canvas;

    static FloatText instance;

    public static void Spawn(Vector3 worldPosition, string message, Color color)
    {
        if (string.IsNullOrEmpty(message))
            return;
        if (instance == null)
            instance = new GameObject("FloatText").AddComponent<FloatText>();
        instance.Add(worldPosition, message, color);
    }

    // "+5s" style popup for time added to the clock
    public static void TimeGain(Vector3 worldPosition, float seconds)
    {
        if (seconds > 0f)
            Spawn(worldPosition, $"+{seconds:0.#}s", GainColor);
    }

    void Awake()
    {
        if (instance == null)
            instance = this;

        var go = new GameObject("FloatTextCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.matchWidthOrHeight = 1f;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Add(Vector3 world, string message, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var text = go.AddComponent<Text>();
        text.text = message;
        text.font = Font();
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        var item = new Item { rect = (RectTransform)go.transform, text = text, world = world, color = color };
        items.Add(item);
        Place(item);
    }

    // The dialogue font when there is one, so popups match the rest of the UI
    static Font Font()
    {
        var ui = MedievalUI.Instance;
        if (ui != null && ui.dialogueText != null && ui.dialogueText.font != null)
            return ui.dialogueText.font;
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void Place(Item item)
    {
        var cam = Camera.main;
        if (cam == null)
            return;
        float scale = canvas.transform.localScale.x > 0f ? canvas.transform.localScale.x : 1f;
        item.rect.position = cam.WorldToScreenPoint(item.world) + Vector3.up * (item.age * riseSpeed * scale);
    }

    // Real time, so the popup still reads while the world is slowed
    void Update()
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item.rect == null)
            {
                items.RemoveAt(i);
                continue;
            }

            item.age += Time.unscaledDeltaTime;
            if (item.age >= lifetime)
            {
                Destroy(item.rect.gameObject);
                items.RemoveAt(i);
                continue;
            }

            // A quick pop on spawn, then fade out at the end
            item.rect.localScale = Vector3.one * (1f + 0.4f * Mathf.Clamp01(1f - item.age / 0.12f));
            var c = item.color;
            c.a = Mathf.Clamp01((lifetime - item.age) / 0.35f);
            item.text.color = c;
            Place(item);
            items[i] = item;
        }
    }
}
