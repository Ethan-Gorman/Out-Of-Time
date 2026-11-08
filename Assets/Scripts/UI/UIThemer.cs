using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Keeps UITheme.Current on the scene's era and restyles the MedievalUI (objective, prompt, dialogue, banner)
// to match, from outside, so MedievalUI itself stays untouched. Scripts we own (ClockUI, CavemanInventoryUI,
// UIRole) restyle themselves on UITheme.Changed.
// Creates itself on startup like ScreenFader, no setup needed. Only changes sprites, colours and outlines,
// never a RectTransform: layout lives in the prefabs, and MedievalUI caches its panel sizes in Awake
public class UIThemer : MonoBehaviour
{
    static UIThemer instance;

    // Statics survive between play sessions when domain reload is off, so reset on play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null)
            return;
        var go = new GameObject("UIThemer");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<UIThemer>();
        instance.Apply();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        LevelManager.EraChanged += OnEraChanged;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        LevelManager.EraChanged -= OnEraChanged;
    }

    // Every scene load, even within the same era: each scene has its own MedievalUI to restyle
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    void OnEraChanged(Era era) => Apply();

    void Apply()
    {
        Era era;
        var info = FindAnyObjectByType<LevelInfo>();
        if (info != null)
            era = info.era;
        else if (LevelManager.Instance != null)
            era = LevelManager.Instance.CurrentEra;
        else
            era = UITheme.Current.era;

        UITheme.Set(era);
        StyleMedievalUI(UITheme.Current);
    }

    static void StyleMedievalUI(UITheme t)
    {
        var ui = MedievalUI.Instance;
        if (ui == null)
            return;

        StylePanel(ui.objectiveRoot, UISprites.Panel(t));
        StylePanel(ui.promptRoot, UISprites.Panel(t));
        StylePanel(ui.dialogueRoot, UISprites.Panel(t));
        StylePanel(ui.bannerRoot, UISprites.Banner(t));

        StyleText(ui.objectiveText, t.text);
        StyleText(ui.promptText, t.text);
        StyleText(ui.dialogueText, t.text);
        StyleText(ui.bannerText, t.accent);

        if (ui.objectiveRoot != null)
        {
            var title = ui.objectiveRoot.transform.Find("Title");
            if (title != null)
                StyleText(title.GetComponent<Text>(), t.accent);
        }

        // The speaker name MedievalUI adds above the dialogue box sits over the world, so it needs the outline too.
        // MedievalUI recolours it from the dialogue text on every line
        if (ui.dialogueRoot != null)
        {
            var speaker = ui.dialogueRoot.transform.Find("SpeakerName");
            if (speaker != null)
                StyleText(speaker.GetComponent<Text>(), t.text);
        }
    }

    // A 9-sliced pixel panel at the shared 4 px per art pixel
    public static void StylePanel(GameObject go, Sprite sprite)
    {
        if (go == null)
            return;
        var img = go.GetComponent<Image>();
        if (img == null)
            return;
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = UISprites.PixelMultiplier;
        img.color = Color.white;
    }

    // The one text style: theme colour, no faux bold (VT323 has no bold), the theme's outline
    public static void StyleText(Text x, Color c)
    {
        if (x == null)
            return;
        x.color = c;
        x.fontStyle = FontStyle.Normal;
        // Not ??: a destroyed Outline is only fake-null to Unity
        var outline = x.GetComponent<Outline>();
        if (outline == null)
            outline = x.gameObject.AddComponent<Outline>();
        outline.effectColor = UITheme.Current.textOutline;
        outline.effectDistance = UILayout.TextOutline;
    }
}
