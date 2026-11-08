using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Title screen shown once when the game launches: the game is paused behind it until the player presses Start
// (Enter, Space, a click, or the gamepad's south button). Creates itself, no scene setup needed.
// Polls the input directly instead of using a UI Button, so no EventSystem is needed in the scenes
public class StartScreen : MonoBehaviour
{
    static bool shown;

    GameObject root;
    Text button;
    float pulse;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        shown = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (shown)
            return;
        shown = true;
        new GameObject("Start Screen").AddComponent<StartScreen>();
    }

    void Start()
    {
        // Everything on game time stops (clock, physics, the world); the music keeps playing
        Time.timeScale = 0f;
        Build();
    }

    void Build()
    {
        var theme = UITheme.Current;
        Font font = FindFont();

        root = new GameObject("Start Canvas", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;  // above the HUD and dialogue, below the fader
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var shade = MakeImage("Shade", root.transform, new Color(0f, 0f, 0f, 0.7f));
        Stretch(shade.rectTransform);

        MakeText("Title", root.transform, font, "OUT OF TIME", 140, theme.accent, new Vector2(0f, 120f), new Vector2(1600f, 180f));
        MakeText("Tagline", root.transform, font, "Time only moves when you move", 40, theme.textDim, new Vector2(0f, 20f), new Vector2(1600f, 60f));

        var panel = MakeImage("Start Button", root.transform, theme.panelFill);
        panel.rectTransform.anchoredPosition = new Vector2(0f, -140f);
        panel.rectTransform.sizeDelta = new Vector2(420f, 96f);
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.panelOutline;
        outline.effectDistance = new Vector2(4f, -4f);
        button = MakeText("Label", panel.transform, font, "START", 64, theme.text, Vector2.zero, panel.rectTransform.sizeDelta);

        MakeText("Hint", root.transform, font, "Enter / Space / click", 28, theme.textDim, new Vector2(0f, -230f), new Vector2(1600f, 40f));
    }

    void Update()
    {
        // Real time: game time is paused
        pulse += Time.unscaledDeltaTime;
        if (button != null)
            button.transform.localScale = Vector3.one * (1f + Mathf.Sin(pulse * 4f) * 0.04f);

        bool pressed =
            Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            || Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame);
        if (pressed)
            Begin();
    }

    void Begin()
    {
        Time.timeScale = 1f;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Never leave the game paused, e.g. if Play mode stops on the title screen
        Time.timeScale = 1f;
    }

    // The UI font (VT323) lives on the HUD's clock text, so borrow it, as ScreenFader and CheatMenu do
    static Font FindFont()
    {
        var clock = FindAnyObjectByType<ClockUI>();
        var text = clock != null ? clock.GetComponent<Text>() : null;
        return text != null && text.font != null ? text.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Text MakeText(string name, Transform parent, Font font, string value, int size, Color color, Vector2 position, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(3f, -3f);
        return text;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
