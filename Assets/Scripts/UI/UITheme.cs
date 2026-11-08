using System;
using UnityEngine;

// One era's UI look: panel and text colours. UITheme.Current follows the era (UIThemer sets it);
// UI scripts listen to UITheme.Changed to restyle themselves.
// A static table rather than ScriptableObject assets, so there are no asset files to keep in sync
public class UITheme
{
    public Era era;
    public Color panelFill, panelOutline, panelLight, panelDark, panelDetail, bannerLine;
    public Color text, textDim, accent, positive, negative, info, sprint, textOutline, clockDigits;
    // Future's digital readout blinks the colon every second
    public bool blinkColon;
    // Steampunk's watch hand steps once per clock second; the other clock faces drain with the time left
    public bool clockTicks;

    public static event Action<UITheme> Changed;

    static UITheme current;
    static UITheme[] themes;

    // Statics survive between play sessions when domain reload is off, so reset on play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        current = null;
        Changed = null;
    }

    // Falls back to the scene's LevelInfo, so scripts enabled before UIThemer still get the right era
    public static UITheme Current
    {
        get
        {
            if (current == null)
            {
                var info = UnityEngine.Object.FindAnyObjectByType<LevelInfo>();
                current = For(info != null ? info.era : Era.Caveman);
            }
            return current;
        }
    }

    public static void Set(Era era)
    {
        var t = For(era);
        if (t == current)
            return;
        current = t;
        Changed?.Invoke(t);
    }

    public static UITheme For(Era era)
    {
        if (themes == null)
            Build();
        return themes[Mathf.Clamp((int)era, 0, themes.Length - 1)];
    }

    // Colours come from each era's palette (Assets/Textures/Palettes) and its level art.
    // Every text colour reads at 4.5:1 or better against its panel fill
    static void Build()
    {
        const float Fill = 235f / 255f;
        themes = new[]
        {
            // Earth and ochre, like the cave rock and glyphs
            new UITheme
            {
                era = Era.Caveman,
                panelFill = Hex("2B1D14", Fill), panelOutline = Hex("1A110B"), panelLight = Hex("C18A50"), panelDark = Hex("493021"),
                panelDetail = Hex("493021"), bannerLine = Hex("C18A50"),
                text = Hex("F3E7C9"), textDim = Hex("C49A5C"), accent = Hex("F0C77C"), positive = Hex("A7C25A"), negative = Hex("F0603A"),
                info = Hex("8FC7E8"), sprint = Hex("F08A32"), textOutline = Hex("1A110B"), clockDigits = Hex("F3E7C9"),
            },
            // Slate stone with gold trim and a crimson banner
            new UITheme
            {
                era = Era.Medieval,
                panelFill = Hex("1F1D26", Fill), panelOutline = Hex("0E0D12"), panelLight = Hex("FFE58A"), panelDark = Hex("C98A26"),
                panelDetail = Hex("C98A26"), bannerLine = Hex("9E2B34"),
                text = Hex("F4E3B5"), textDim = Hex("A9ABB4"), accent = Hex("FFC94A"), positive = Hex("9BC95E"), negative = Hex("F25563"),
                info = Hex("8FB8F0"), sprint = Hex("F0A040"), textOutline = Hex("0E0D12"), clockDigits = Hex("F4E3B5"),
            },
            // Brass and copper rivets on soot
            new UITheme
            {
                era = Era.Steampunk,
                panelFill = Hex("1C1612", Fill), panelOutline = Hex("151210"), panelLight = Hex("D9AA58"), panelDark = Hex("7A5520"),
                panelDetail = Hex("C9814E"), bannerLine = Hex("A87A33"),
                text = Hex("E8DCC4"), textDim = Hex("9DB5AA"), accent = Hex("FFD27A"), positive = Hex("8FD694"), negative = Hex("FF5A36"),
                info = Hex("8EC9D8"), sprint = Hex("FF9A3A"), textOutline = Hex("151210"), clockDigits = Hex("FFD27A"),
                clockTicks = true,
            },
            // Steel blue with a cyan edge, like the factory tiles
            new UITheme
            {
                era = Era.Future,
                panelFill = Hex("071929", 225f / 255f), panelOutline = Hex("3FC4E0"), panelLight = Hex("14314A"), panelDark = Hex("14314A"),
                panelDetail = Hex("14314A"), bannerLine = Hex("3FC4E0"),
                text = Hex("EAEAF5"), textDim = Hex("93AFC9"), accent = Hex("3FC4E0"), positive = Hex("38E09B"), negative = Hex("FF4FA3"),
                info = Hex("9FF5FF"), sprint = Hex("FFD166"), textOutline = Hex("04101A"), clockDigits = Hex("9FF5FF"),
                blinkColon = true,
            },
        };
    }

    public static Color Hex(string s, float a = 1f)
    {
        ColorUtility.TryParseHtmlString("#" + s, out Color c);
        c.a = a;
        return c;
    }
}

// The shared layout grid (reference pixels at 1920x1080)
public static class UILayout
{
    public const float Margin = 48f, Gap = 16f, PadX = 24f, PadY = 16f, PanelWidth = 600f, ClockIcon = 64f;
    public static readonly Vector2 ClockPlate = new Vector2(432f, 96f);
    // One text outline everywhere (2 px at 1080)
    public static readonly Vector2 TextOutline = new Vector2(2f, -2f);
    // Type scale for VT323
    public const int Display = 80, Clock = 56, Heading = 36, Body = 32, Small = 24;

    // Places a rect by its top-left corner, x right and y down from the canvas' top-left
    public static void TopLeft(RectTransform r, Vector2 topLeft, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
        r.sizeDelta = size;
    }
}
