using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

// Fades the screen to black before every scene load and back in once the new scene is ready.
// Shows the era name during the black when the era changes (and on game start).
// Creates itself on startup with its own overlay canvas, no setup needed. All scene loading goes through
// ScreenFader.LoadScene so a load can't be triggered twice while a fade is running.
// Uses real time, so it still works while the world (WorldTime / Time.timeScale) is frozen.
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    // True from the start of a fade out until the new scene has faded back in
    public static bool Busy { get; private set; }

    public float fadeOutTime = 0.35f;
    public float fadeInTime = 0.45f;
    // How long the black stays up with the era name on it
    public float eraHoldTime = 0.6f;
    // The fade moves in this many steps, for a chunky pixel-art look
    public int steps = 8;

    // Above every HUD / MedievalUI canvas
    const int SortingOrder = 1000;

    Image black;
    Text eraText;
    CanvasGroup group;
    bool hasEra;
    Era lastEra;

    // Statics survive between play sessions when domain reload is off, so reset on play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        Busy = false;
    }

    // Runs before the first frame is drawn, so the game starts on black and fades in
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        Ensure();
        FadeIn();
    }

    static ScreenFader Ensure()
    {
        if (Instance != null)
            return Instance;
        var go = new GameObject("ScreenFader");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<ScreenFader>();
        return Instance;
    }

    void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // Lets the black swallow mouse clicks on UI underneath while it's up
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();

        black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
        black.transform.SetParent(transform, false);
        black.color = Color.black;
        Stretch(black.rectTransform);

        eraText = new GameObject("Era", typeof(RectTransform)).AddComponent<Text>();
        eraText.transform.SetParent(transform, false);
        eraText.alignment = TextAnchor.MiddleCenter;
        eraText.fontSize = 96;
        eraText.color = new Color(0.95f, 0.9f, 0.75f);
        eraText.raycastTarget = false;
        eraText.horizontalOverflow = HorizontalWrapMode.Overflow;
        eraText.verticalOverflow = VerticalWrapMode.Overflow;
        eraText.text = "";
        Stretch(eraText.rectTransform);

        SetAlpha(1f);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ---------------------------------------------------------------- public API

    // Fades out, loads the scene with this Build Settings index, then fades in.
    // Returns false (and does nothing) if a fade is already running
    public static bool LoadScene(int buildIndex)
    {
        return Load(() => SceneManager.LoadSceneAsync(buildIndex));
    }

    // Same, by scene path. In the Editor this also works for scenes not in Build Settings yet
    public static bool LoadScene(string path)
    {
        int index = SceneUtility.GetBuildIndexByScenePath(path);
        if (index >= 0)
            return LoadScene(index);
#if UNITY_EDITOR
        return Load(() => EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single)));
#else
        return Load(() => SceneManager.LoadSceneAsync(path));
#endif
    }

    // Fades from black back to the current scene (the game start uses this)
    public static void FadeIn()
    {
        var fader = Ensure();
        if (Busy)
            return;
        Busy = true;
        fader.StartCoroutine(fader.FadeInRoutine());
    }

    static bool Load(Func<AsyncOperation> load)
    {
        var fader = Ensure();
        if (Busy)
            return false;
        Busy = true;
        fader.StartCoroutine(fader.LoadRoutine(load));
        return true;
    }

    // ---------------------------------------------------------------- fades

    IEnumerator LoadRoutine(Func<AsyncOperation> load)
    {
        eraText.text = "";
        yield return Fade(0f, 1f, fadeOutTime);

        // A bad scene path can throw; catch it so the screen never gets stuck on black
        AsyncOperation op = null;
        try { op = load(); }
        catch (Exception e) { Debug.LogException(e); }

        if (op != null)
            yield return op;
        else
            Debug.LogWarning("[ScreenFader] Scene load failed, fading back in.");

        yield return FadeInRoutine();
    }

    IEnumerator FadeInRoutine()
    {
        SetAlpha(1f);

        // One frame so the new scene's Start methods run before it shows
        yield return null;

        if (ShowEraName())
        {
            float until = Time.unscaledTime + eraHoldTime;
            while (Time.unscaledTime < until)
                yield return null;
        }

        yield return Fade(1f, 0f, fadeInTime);
        eraText.text = "";
        Busy = false;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            SetAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
            // Capped, so the long frame of a scene load doesn't skip the whole fade
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        }
        SetAlpha(to);
    }

    void SetAlpha(float alpha)
    {
        if (steps > 1)
            alpha = Mathf.Round(alpha * steps) / steps;
        group.alpha = alpha;
        group.blocksRaycasts = alpha > 0f;
    }

    // Puts the era name on the black when the new scene is a different era. Returns true if it did
    bool ShowEraName()
    {
        var info = FindAnyObjectByType<LevelInfo>();
        if (info == null || (hasEra && info.era == lastEra))
            return false;

        hasEra = true;
        lastEra = info.era;
        FindFont();
        eraText.text = info.era == Era.Future ? "THE FUTURE" : $"{info.era.ToString().ToUpperInvariant()} ERA";
        return true;
    }

    // The UI font (VT323) is set on the scene's HUD texts rather than loaded from Resources,
    // so borrow it from them; falls back to Unity's built-in font
    void FindFont()
    {
        if (eraText.font != null && eraText.font.name.Contains("VT323"))
            return;

        Font fallback = null;
        foreach (var text in FindObjectsByType<Text>())
        {
            if (text == eraText || text.font == null)
                continue;
            if (text.font.name.Contains("VT323"))
            {
                eraText.font = text.font;
                return;
            }
            if (fallback == null)
                fallback = text.font;
        }

        if (eraText.font == null)
            eraText.font = fallback != null ? fallback : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
