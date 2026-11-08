using System;
using System.Collections.Generic;
using UnityEngine;

// Tiny pixel-art UI sprites drawn in code (point filtered), like CavemanSceneBuilder's, so each era gets
// its own panels and clock without image files. Built once per era and cached
public static class UISprites
{
    // Sprites are 100 PPU; at 0.25 each art pixel draws as 4 reference pixels, the same as UIFrame
    public const float PixelMultiplier = 0.25f;

    static readonly Dictionary<Era, Sprite> panels = new Dictionary<Era, Sprite>();
    static readonly Dictionary<Era, Sprite> banners = new Dictionary<Era, Sprite>();
    static readonly Dictionary<Era, Sprite[]> clocks = new Dictionary<Era, Sprite[]>();
    static readonly Dictionary<(Era, int), Texture2D> panelTextures = new Dictionary<(Era, int), Texture2D>();

    static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    // The textures die with the play session, so drop them when a new one starts
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        panels.Clear();
        banners.Clear();
        clocks.Clear();
        panelTextures.Clear();
    }

    // ---------------------------------------------------------------- panels

    // O outline, H light edge, S dark edge, F fill, R detail (rivets). Rows 3-4 and columns 3-4 are
    // uniform so the 3 px 9-slice border stretches cleanly
    static string[] PanelMap(Era era)
    {
        switch (era)
        {
            // Chipped stone slab
            case Era.Caveman: return new[] { "..OOOO..", ".OHHHHO.", "OHFFFFSO", "OHFFFFSO", "OHFFFFSO", "OHFFFFSO", ".OSSSSO.", "..OOOO.." };
            // Square gold frame
            case Era.Medieval: return new[] { "OOOOOOOO", "OHHHHHHO", "OHFFFFSO", "OHFFFFSO", "OHFFFFSO", "OHFFFFSO", "OSSSSSSO", "OOOOOOOO" };
            // Riveted brass plate
            case Era.Steampunk: return new[] { "..OOOO..", ".RHHHHR.", "OHFFFFSO", "OHFFFFSO", "OHFFFFSO", "OHFFFFSO", ".RSSSSR.", "..OOOO.." };
            // Notched cyan screen
            default: return new[] { "..OOOO..", ".OHHHHO.", "OHFFFFHO", "OHFFFFHO", "OHFFFFHO", "OHFFFFHO", ".OHHHHO.", "..OOOO.." };
        }
    }

    // Full-width strip: a line top and bottom, see-through fill between
    static readonly string[] BannerMap = { "OOOOOOOO", "LLLLLLLL", "FFFFFFFF", "FFFFFFFF", "FFFFFFFF", "FFFFFFFF", "LLLLLLLL", "OOOOOOOO" };

    static Color32? PanelColour(UITheme t, char c)
    {
        switch (c)
        {
            case 'O': return (Color32)t.panelOutline;
            case 'H': return (Color32)t.panelLight;
            case 'S': return (Color32)t.panelDark;
            case 'F': return (Color32)t.panelFill;
            case 'R': return (Color32)t.panelDetail;
            case 'L': return (Color32)t.bannerLine;
            default: return null;
        }
    }

    public static Sprite Panel(UITheme t)
    {
        if (panels.TryGetValue(t.era, out var cached) && cached != null)
            return cached;
        var sprite = MakeSprite(8, 8, FromMap(PanelMap(t.era), c => PanelColour(t, c)), new Vector4(3f, 3f, 3f, 3f), "UIPanel " + t.era);
        panels[t.era] = sprite;
        return sprite;
    }

    public static Sprite Banner(UITheme t)
    {
        if (banners.TryGetValue(t.era, out var cached) && cached != null)
            return cached;
        // The banner's fill is lighter than a panel's, so the scene still shows through behind the big text
        Color32 fill = t.panelFill;
        fill.a = 217;
        var sprite = MakeSprite(8, 8, FromMap(BannerMap, c => c == 'F' ? fill : PanelColour(t, c)), new Vector4(0f, 3f, 0f, 3f), "UIBanner " + t.era);
        banners[t.era] = sprite;
        return sprite;
    }

    // The panel art for IMGUI (CheatMenu), each art pixel drawn as scale x scale
    public static Texture2D PanelTexture(UITheme t, int scale)
    {
        scale = Mathf.Max(1, scale);
        if (panelTextures.TryGetValue((t.era, scale), out var cached) && cached != null)
            return cached;
        Color32[] small = FromMap(PanelMap(t.era), c => PanelColour(t, c));
        int w = 8 * scale;
        var px = new Color32[w * w];
        for (int y = 0; y < w; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = small[(y / scale) * 8 + x / scale];
        var tex = MakeTexture(w, w, px, "UIPanelTexture " + t.era);
        panelTextures[(t.era, scale)] = tex;
        return tex;
    }

    // ---------------------------------------------------------------- clock icons

    // Every frame of the era's 16x16 clock icon. ClockUI picks one from the time left
    public static Sprite[] ClockFrames(UITheme t)
    {
        if (clocks.TryGetValue(t.era, out var cached) && cached != null && cached.Length > 0 && cached[0] != null)
            return cached;
        int count = t.era == Era.Future ? 13 : 8;
        var frames = new Sprite[count];
        for (int k = 0; k < count; k++)
            frames[k] = MakeSprite(16, 16, ClockArt(t.era, k), Vector4.zero, $"UIClock {t.era} {k}");
        clocks[t.era] = frames;
        return frames;
    }

    static Color32[] ClockArt(Era era, int k)
    {
        switch (era)
        {
            case Era.Caveman: return Sundial(k);
            case Era.Medieval: return Hourglass(k);
            case Era.Steampunk: return PocketWatch(k);
            default: return DigitalRing(k);
        }
    }

    // Carved stone sundial: the gnomon's shadow sweeps round as time runs down
    static Color32[] Sundial(int k)
    {
        var px = FromMap(new[]
        {
            "................", "................", ".....OOOOOO.....", "...OOWWTWWWOO...",
            "..OWWLLLLLLLWO..", "..OWLLLLLLLLLO..", ".OTLLLLLLLLLLTO.", ".OWLLLLLLLLLLMO.",
            ".OWLLLLLLLLLLMO.", "..OLLLLLLLLLMO..", "..OMLLLLLLLMMO..", "..ODMMMMMMMMDO..",
            "...OODDDDDDOO...", ".....OOOOOO.....", "................", "................",
        }, Keys(('O', "1A110B"), ('D', "493021"), ('M', "80502F"), ('L', "A46B3A"), ('W', "C18A50"), ('T', "F0C77C")));
        Line(px, 7.5, 8, 160 - k * 20, 5, Hex32("2B1D14"));
        Color32 gnomon = Hex32("493021");
        for (int y = 5; y <= 7; y++)
        {
            Px(px, 7, y, gnomon);
            Px(px, 8, y, gnomon);
        }
        Px(px, 7, 4, Hex32("C18A50"));
        return px;
    }

    // Hourglass: sand drains from the top bulb into the bottom one
    static Color32[] Hourglass(int k)
    {
        var px = FromMap(new[]
        {
            "................", "..OOOOOOOOOOOO..", "..OHHHHHHHHHHO..", "..OCCCCCCCCCCO..",
            "...OPG....GPO...", "...OPG....GPO...", "...OP.G..G.PO...", "...OP..GG..PO...",
            "...OP..GG..PO...", "...OP.G..G.PO...", "...OPG....GPO...", "...OPG....GPO...",
            "..OHHHHHHHHHHO..", "..OCCCCCCCCCCO..", "..OOOOOOOOOOOO..", "................",
        }, Keys(('O', "0E0D12"), ('H', "FFE58A"), ('C', "C98A26"), ('P', "6B3B2A"), ('G', "8A8C97")));
        Color32 sand = Hex32("E0A24A");
        int top = Mathf.RoundToInt((1f - k / 7f) * 4f);
        int bot = 4 - top;
        // Rows of sand from the neck outwards; the neck rows themselves hold none
        int[][] topRows = { new int[0], new[] { 7, 8 }, new[] { 6, 7, 8, 9 }, new[] { 6, 7, 8, 9 } };
        int[] topY = { 7, 6, 5, 4 };
        int[][] botRows = { new[] { 6, 7, 8, 9 }, new[] { 6, 7, 8, 9 }, new[] { 7, 8 }, new int[0] };
        int[] botY = { 11, 10, 9, 8 };
        for (int i = 0; i < top; i++)
            foreach (int x in topRows[i])
                Px(px, x, topY[i], sand);
        for (int i = 0; i < bot; i++)
            foreach (int x in botRows[i])
                Px(px, x, botY[i], sand);
        // A falling stream while there is sand on both sides
        if (k > 0 && k < 7)
        {
            Px(px, 7, 8, Hex32("F4E3B5"));
            Px(px, 8, 9, sand);
        }
        return px;
    }

    // Brass pocket watch: the hand ticks round once per clock second
    static Color32[] PocketWatch(int k)
    {
        var px = FromMap(new[]
        {
            "................", "......OOOO......", "......OBBO......", ".......OO.......",
            ".....OOOOOO.....", "...OOHHHHHHOO...", "..OHHFFFFFFBSO..", "..OHFFFFFFFFSO..",
            ".OHFFFFTFFFFFSO.", ".OHFFFFFFFFFFSO.", ".OHTFFFFFFFFTSO.", ".OHFFFFFFFFFFSO.",
            "..OBFFFFFFFFSO..", "..OBSFFFFFFSSO..", "...OOSSSSSSOO...", ".....OOOOOO.....",
        }, Keys(('O', "151210"), ('B', "A87A33"), ('H', "D9AA58"), ('S', "4E3415"), ('F', "F0D79A"), ('T', "8A5A36")));
        Color32 hand = Hex32("1C1612");
        Line(px, 7.5, 10, 90 - k * 45, 3, hand);
        Px(px, 7, 10, hand);
        Px(px, 8, 10, hand);
        return px;
    }

    // Future: a ring of 12 lit ticks that go dark one by one, centre turns pink when the time is gone
    static Color32[] DigitalRing(int k)
    {
        var px = new Color32[16 * 16];
        for (int i = 0; i < px.Length; i++)
            px[i] = Clear;
        Color32 lit = Hex32("3FC4E0"), dark = Hex32("1D3A55");
        for (int t = 0; t < 12; t++)
        {
            double a = (90 - 30 * t) * Math.PI / 180.0;
            Color32 c = t < 12 - k ? lit : dark;
            foreach (double r in new[] { 6.5, 5.2 })
                Px(px, Round(7.5 + Math.Cos(a) * r), Round(7.5 - Math.Sin(a) * r), c);
        }
        Color32 centre = k == 12 ? Hex32("FF4FA3") : Hex32("9FF5FF");
        Px(px, 7, 7, centre);
        Px(px, 8, 7, centre);
        Px(px, 7, 8, centre);
        Px(px, 8, 8, centre);
        return px;
    }

    // ---------------------------------------------------------------- helpers

    // Rows are written top-down; textures store rows bottom-up, so flip y
    static Color32[] FromMap(string[] rows, Func<char, Color32?> palette)
    {
        int h = rows.Length, w = rows[0].Length;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = palette(rows[y][x]) ?? Clear;
        return px;
    }

    static Func<char, Color32?> Keys(params (char key, string hex)[] keys)
    {
        var map = new Dictionary<char, Color32>();
        foreach (var (key, hex) in keys)
            map[key] = Hex32(hex);
        return c => map.TryGetValue(c, out var col) ? col : (Color32?)null;
    }

    // One pixel of a 16x16 icon, x right and y down from the top-left
    static void Px(Color32[] px, int x, int y, Color32 c)
    {
        if (x < 0 || x >= 16 || y < 0 || y >= 16)
            return;
        px[(15 - y) * 16 + x] = c;
    }

    // n pixels out from (cx, cy) at deg degrees (0 = right, 90 = up), for clock hands and shadows
    static void Line(Color32[] px, double cx, double cy, double deg, int n, Color32 c)
    {
        double a = deg * Math.PI / 180.0;
        for (int i = 1; i <= n; i++)
            Px(px, Round(cx + Math.Cos(a) * i), Round(cy - Math.Sin(a) * i), c);
    }

    // Halves round to even, like Mathf.RoundToInt; doubles so 90 degree lines land exactly on the half
    static int Round(double v) => (int)Math.Round(v, MidpointRounding.ToEven);

    static Color32 Hex32(string s) => UITheme.Hex(s);

    static Texture2D MakeTexture(int w, int h, Color32[] px, string name)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
        };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Sprite MakeSprite(int w, int h, Color32[] px, Vector4 border, string name)
    {
        var sprite = Sprite.Create(MakeTexture(w, h, px, name), new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        sprite.name = name;
        return sprite;
    }
}
