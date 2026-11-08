using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CavemanSceneBuilder
{
    const string Prefabs = "Assets/prefabs/Caveman";
    const string Mats = "Assets/Materials/Caveman";
    const string Sprites = "Assets/Sprites/Caveman";
    const string Textures = "Assets/Textures/Caveman";
    const string ScenePath = "Assets/Scenes/Caveman.unity";
    static Material floorMat, stoneMat, woodMat, barkMat, leafMat, crystalMat, caveMat, pathMat, spriteMat;
    static Sprite bramble, firewoodSprite, logSprite, potSprite, basketSprite, herbSprite, glyphSprite;
    static Sprite[] boar, fireFrames;

    [MenuItem("Tools/Out Of Time/Build Caveman Era")]
    public static void Build()
    {
        Scene original = SceneManager.GetActiveScene();
        if (original.isDirty) { Debug.LogError("Save or close the dirty scene before building Caveman."); return; }
        Folder("Assets/prefabs", "Caveman"); Folder("Assets/Materials", "Caveman"); Folder("Assets/Sprites", "Caveman"); Folder("Assets/Textures", "Caveman");
        MakeArt();
        Scene staging = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(staging);
        try
        {
            MakeInventory(); MakeEssentials(); MakeDirector(); MakeBreakables(); MakeCraftingStone(); MakeBoar(); MakeStone(); MakeDecorPrefabs(); MakeMachine(); MakeLevel();
        }
        finally
        {
            EditorSceneManager.CloseScene(staging, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Instantiate(Prefabs + "/CavemanEssentials.prefab"); Instantiate(Prefabs + "/CavemanDirector.prefab"); Instantiate(Prefabs + "/CavemanLevel.prefab");
        // Inside a cavern: dim, warm ambient so the fires and torches carry the light
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.44f, 0.38f, 0.33f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddBuildScenes();
        Debug.Log("Built Caveman prefabs and Assets/Scenes/Caveman.unity.");
    }

    static void Folder(string parent, string name) { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }

    static void MakeArt()
    {
        Texture2D caveFloor = Texture("CaveFloor32", 32, CaveFloorPixel());
        Texture2D stoneTexture = Texture("Stone32", 32, RockPixel());
        Texture2D woodTexture = Texture("Wood32", 32, (x, y) =>
        {
            int groove = (x + (y / 8) * 3) % 11;
            return groove < 2 ? Hex("493021") : groove < 4 ? Hex("80502F") : (y % 8 == 1 ? Hex("C18A50") : Hex("A46B3A"));
        });
        // Bark as clean vertical grooves (dark groove, lit ridge, two body tones), stepping sideways every
        // 11 rows, plus two knots: readable pixel shapes rather than noise
        Texture2D barkTexture = Texture("Bark32", 32, (x, y) =>
        {
            int k = (x + (y / 11) * 3) % 8;
            float kx = (x - 9) / 2.5f, ky = (y - 20) / 2f, jx = (x - 25) / 2f, jy = (y - 6) / 2.5f;
            if (kx * kx + ky * ky < 1f || jx * jx + jy * jy < 1f) return Hex("50301F");
            if (kx * kx + ky * ky < 2.2f || jx * jx + jy * jy < 2.2f) return Hex("BE8145");
            return k == 0 ? Hex("50301F") : k == 1 ? Hex("BE8145") : k < 5 ? Hex("89512F") : Hex("70432B");
        });
        Texture2D leafTexture = Texture("Leaves32", 32, (x, y) =>
        {
            int n = (x * 43 + y * 67 + x * y * 3) % 47;
            return n < 4 ? Hex("C0B95E") : n < 13 ? Hex("91A846") : n < 21 ? Hex("456E38") : Hex("638A3D");
        });
        Texture2D crystalTexture = Texture("Crystal32", 32, (x, y) =>
        {
            int n = (x + y * 3) % 17;
            return n < 3 ? Hex("C8FFF0") : n < 7 ? Hex("5FD1CC") : n < 11 ? Hex("277F8A") : Hex("1E596A");
        });
        floorMat = Material("CaveFloor", caveFloor); floorMat.SetFloat("_OOT_WorldUV", 1f); stoneMat = Material("Stone", stoneTexture); woodMat = Material("Wood", woodTexture);
        barkMat = Material("Bark", barkTexture); leafMat = Material("Leaves", leafTexture); crystalMat = Material("Crystal", crystalTexture, true);
        caveMat = Material("CaveStone", stoneTexture); caveMat.color = new Color(0.34f, 0.39f, 0.42f);
        pathMat = Material("StonePath", stoneTexture);
        floorMat.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f)); stoneMat.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f)); stoneMat.SetFloat("_OOT_WorldUV", 1f);
        woodMat.SetTextureScale("_BaseMap", new Vector2(2, 2)); barkMat.SetTextureScale("_BaseMap", new Vector2(1, 2));
        leafMat.SetTextureScale("_BaseMap", new Vector2(1.4f, 1.4f)); caveMat.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f)); caveMat.SetFloat("_OOT_WorldUV", 1f); pathMat.SetTextureScale("_BaseMap", new Vector2(15, 1));
        spriteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLit.mat");
        AssetDatabase.DeleteAsset(Sprites + "/Tree.png"); AssetDatabase.DeleteAsset(Sprites + "/Rock.png"); AssetDatabase.DeleteAsset(Sprites + "/MagneticStone.png");
        AssetDatabase.DeleteAsset(Mats + "/Grass.mat"); AssetDatabase.DeleteAsset(Textures + "/Grass32.png");
        bramble = Sprite("Bramble", 32, 32, BramblePixels);
        // Hand-drawn 40x40 art replaces the generated stack; only draw one if it is missing
        firewoodSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "/FirewoodStack.png");
        if (firewoodSprite == null) firewoodSprite = Sprite("FirewoodStack", 40, 30, FirewoodPixels);
        logSprite = Sprite("CampLog", 32, 24, LogPixels);
        potSprite = Sprite("ClayPot", 32, 32, PotPixels);
        basketSprite = Sprite("WovenBasket", 32, 28, BasketPixels);
        herbSprite = Sprite("GatheredHerbs", 32, 32, HerbPixels);
        glyphSprite = Sprite("CaveGlyph", 32, 32, GlyphPixels);
        boar = new Sprite[4];
        for (int i = 0; i < boar.Length; i++) { int frame = i; boar[i] = Sprite("WildBoar_" + (i + 1), 64, 48, t => BoarPixels(t, frame)); }
        fireFrames = new Sprite[4];
        for (int i = 0; i < fireFrames.Length; i++) { int frame = i; fireFrames[i] = Sprite("Campfire_" + (i + 1), 32, 40, t => FirePixels(t, frame)); }
    }

    static Material Material(string name, Texture texture, bool emissive = false)
    {
        string path = Mats + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Out Of Time/Lit Cutout") ?? Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", texture); m.color = Color.white;
        // RealtimeEmissive: with the default "emissive is black" flag, URP's material check strips _EMISSION on save
        if (emissive) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Hex("46D8D5") * 1.7f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        EditorUtility.SetDirty(m); return m;
    }

    // Dusty cave earth: warm grey-brown with soft lighter and darker drifts (periodic, so it tiles), grit and pebbles
    static Func<int, int, Color> CaveFloorPixel()
    {
        // Drawn like a 16-bit floor tile: no lone random pixels, only small readable shapes. Pebbles (lit top row,
        // dark row under them), short cracks and paired grit on three broad drifts of colour
        var rng = new System.Random(7);
        var tile = new Color[32, 32];
        float tau = Mathf.PI * 2f / 32f;
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float v = Mathf.Sin(tau * (x + 2 * y) + 1.3f) * 0.5f + Mathf.Sin(tau * (3 * x - y) + 0.4f) * 0.3f + Mathf.Sin(tau * (2 * x + 5 * y)) * 0.2f;
                tile[x, y] = v > 0.35f ? Hex("6D6761") : v < -0.35f ? Hex("55504B") : Hex("625C57");
            }
        void Set(int x, int y, string c) => tile[(x % 32 + 32) % 32, (y % 32 + 32) % 32] = Hex(c);
        for (int i = 0; i < 7; i++)
        {
            int x = rng.Next(32), y = rng.Next(32), w = rng.Next(2, 4);
            for (int k = 0; k < w; k++) { Set(x + k, y + 1, "8A8379"); Set(x + k, y, "766F67"); Set(x + k, y - 1, "48433F"); }
        }
        for (int i = 0; i < 4; i++)
        {
            int x = rng.Next(32), y = rng.Next(32), dy = rng.Next(2) * 2 - 1;
            for (int k = 0; k < 4; k++) Set(x + k, y + dy * (k / 2), "4B4641");
        }
        for (int i = 0; i < 9; i++) { int x = rng.Next(32), y = rng.Next(32); Set(x, y, "7F7870"); Set(x + 1, y, "7F7870"); }
        return (x, y) => tile[x, y];
    }

    // Pixel-art rock in the same warm greys as the cobbles: three broad tones in soft drifts (not per-pixel noise),
    // short dark cracks with a lit pixel above each, and a few pale flecks. Periodic, so it tiles
    static Func<int, int, Color> RockPixel()
    {
        var rng = new System.Random(11);
        var crack = new bool[32, 32];
        for (int k = 0; k < 9; k++)
        {
            int x = rng.Next(32), y = rng.Next(32), len = rng.Next(3, 7), dy = rng.Next(3) - 1;
            for (int i = 0; i < len; i++) crack[(x + i + 32) % 32, (y + dy * (i / 2) + 32) % 32] = true;
        }
        var fleck = new double[32, 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) fleck[x, y] = rng.NextDouble();
        return (x, y) =>
        {
            if (crack[x, y]) return Hex("57514A");
            if (crack[x, (y + 31) % 32]) return Hex("A69E91");
            float tau = Mathf.PI * 2f / 32f;
            float v = Mathf.Sin(tau * (2 * x + y) + 0.7f) * 0.55f + Mathf.Sin(tau * (x - 2 * y) + 2.1f) * 0.45f;
            if (fleck[x, y] < 0.035) return Hex("9E968A");
            return v > 0.3f ? Hex("958D81") : v < -0.3f ? Hex("776F66") : Hex("867E74");
        };
    }

    internal static Texture2D Texture(string name, int size, Func<int, int, Color> pixel)
    {
        string path = Textures + "/" + name + ".png";
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) t.SetPixel(x, y, pixel(x, y));
        t.Apply(); File.WriteAllBytes(Path.GetFullPath(path), t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Default; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Repeat; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    internal static Sprite Sprite(string name, int w, int h, Action<Texture2D> draw, float pixelsPerUnit = 16f)
    {
        string path = Sprites + "/" + name + ".png";
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Fill(t, Hex("00000000")); draw(t); t.Apply(); File.WriteAllBytes(Path.GetFullPath(path), t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.spritePixelsPerUnit = pixelsPerUnit; importer.alphaIsTransparency = true; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void BramblePixels(Texture2D t)
    {
        Rect(t, 1, 2, 22, 7, Hex("26352C")); Rect(t, 2, 3, 20, 5, Hex("3B5634")); Rect(t, 4, 4, 5, 3, Hex("668043")); Rect(t, 11, 3, 5, 4, Hex("668043"));
        Polygon(t, Hex("D1BA80"), new Vector2Int(3, 8), new Vector2Int(5, 15), new Vector2Int(7, 8)); Polygon(t, Hex("D1BA80"), new Vector2Int(9, 8), new Vector2Int(12, 16), new Vector2Int(15, 8));
        Polygon(t, Hex("D1BA80"), new Vector2Int(16, 8), new Vector2Int(19, 15), new Vector2Int(22, 8));
    }

    // A stack of split logs seen end-on: rows of three, two and one
    static void FirewoodPixels(Texture2D t)
    {
        Rect(t, 4, 6, 32, 14, Hex("5A3A24")); Rect(t, 9, 14, 22, 10, Hex("5A3A24"));
        int[,] ends = { { 3, 0 }, { 14, 0 }, { 25, 0 }, { 8, 9 }, { 19, 9 }, { 14, 18 } };
        for (int i = 0; i < ends.GetLength(0); i++)
        {
            int x = ends[i, 0], y = ends[i, 1];
            Ellipse(t, x, y, 12, 11, Hex("3F2819")); Ellipse(t, x + 1, y + 1, 10, 9, Hex("C8925A"));
            Ellipse(t, x + 3, y + 3, 6, 5, Hex("A06C3E")); Ellipse(t, x + 4, y + 4, 4, 3, Hex("D9A86C"));
            Pixel(t, x + 5, y + 5, Hex("7A4A2A")); Pixel(t, x + 2, y + 7, Hex("E1B47A"));
        }
    }

    static void LogPixels(Texture2D t)
    {
        Polygon(t, Hex("39241C"), new Vector2Int(2, 5), new Vector2Int(5, 2), new Vector2Int(27, 8), new Vector2Int(30, 13), new Vector2Int(27, 18), new Vector2Int(5, 14));
        Polygon(t, Hex("80502F"), new Vector2Int(4, 6), new Vector2Int(7, 4), new Vector2Int(25, 9), new Vector2Int(28, 13), new Vector2Int(26, 16), new Vector2Int(6, 12));
        Ellipse(t, 22, 8, 8, 9, Hex("D49A60")); Ellipse(t, 23, 9, 6, 7, Hex("A66B3B")); Ellipse(t, 24, 10, 4, 5, Hex("E1B47A"));
        Rect(t, 7, 6, 2, 7, Hex("B87943")); Rect(t, 14, 8, 2, 6, Hex("603A28")); Pixel(t, 11, 10, Hex("D49A60"));
    }

    static void PotPixels(Texture2D t)
    {
        Polygon(t, Hex("3F2926"), new Vector2Int(9, 25), new Vector2Int(23, 25), new Vector2Int(27, 20), new Vector2Int(25, 6), new Vector2Int(21, 2), new Vector2Int(11, 2), new Vector2Int(7, 7), new Vector2Int(5, 20));
        Polygon(t, Hex("A84F3B"), new Vector2Int(10, 23), new Vector2Int(22, 23), new Vector2Int(25, 19), new Vector2Int(23, 7), new Vector2Int(20, 4), new Vector2Int(12, 4), new Vector2Int(9, 8), new Vector2Int(7, 19));
        Rect(t, 8, 24, 16, 3, Hex("E18A5F")); Rect(t, 10, 20, 3, 3, Hex("D8754E")); Rect(t, 20, 9, 2, 7, Hex("7F3E35"));
        Rect(t, 11, 15, 11, 2, Hex("E5A06E")); Rect(t, 12, 8, 2, 5, Hex("C86648"));
    }

    static void BasketPixels(Texture2D t)
    {
        Polygon(t, Hex("37291F"), new Vector2Int(4, 19), new Vector2Int(8, 4), new Vector2Int(25, 4), new Vector2Int(29, 19), new Vector2Int(24, 25), new Vector2Int(8, 25));
        Polygon(t, Hex("A96E3E"), new Vector2Int(6, 18), new Vector2Int(9, 7), new Vector2Int(24, 7), new Vector2Int(27, 18), new Vector2Int(23, 23), new Vector2Int(9, 23));
        Rect(t, 7, 17, 19, 3, Hex("D29A58")); Rect(t, 9, 12, 15, 2, Hex("C98748")); Rect(t, 10, 8, 2, 10, Hex("E0B56B")); Rect(t, 17, 8, 2, 10, Hex("80522F")); Rect(t, 23, 8, 2, 10, Hex("E0B56B"));
        Rect(t, 10, 24, 12, 2, Hex("71462D"));
    }

    static void HerbPixels(Texture2D t)
    {
        Rect(t, 14, 3, 4, 15, Hex("4D3526"));
        Polygon(t, Hex("32523C"), new Vector2Int(15, 11), new Vector2Int(5, 17), new Vector2Int(15, 15));
        Polygon(t, Hex("628B4A"), new Vector2Int(15, 14), new Vector2Int(7, 22), new Vector2Int(16, 18));
        Polygon(t, Hex("426B42"), new Vector2Int(17, 12), new Vector2Int(27, 20), new Vector2Int(17, 16));
        Polygon(t, Hex("86A954"), new Vector2Int(16, 17), new Vector2Int(24, 25), new Vector2Int(17, 20));
        Pixel(t, 15, 23, Hex("E5C36D")); Pixel(t, 20, 27, Hex("E5C36D")); Pixel(t, 10, 20, Hex("DCA85F"));
        Rect(t, 12, 2, 8, 3, Hex("76502D")); Rect(t, 10, 0, 12, 3, Hex("A66B3B"));
    }

    static void GlyphPixels(Texture2D t)
    {
        Color ochre = Hex("D29B59"), light = Hex("F0C77C"), dark = Hex("70442F");
        Rect(t, 5, 4, 3, 22, dark); Rect(t, 7, 5, 2, 20, ochre); Rect(t, 23, 4, 3, 22, dark); Rect(t, 23, 5, 2, 20, ochre);
        Polygon(t, light, new Vector2Int(10, 10), new Vector2Int(16, 18), new Vector2Int(22, 10), new Vector2Int(21, 8), new Vector2Int(16, 14), new Vector2Int(11, 8));
        Rect(t, 13, 23, 7, 2, ochre); Rect(t, 14, 26, 5, 2, dark); Pixel(t, 16, 29, light);
    }

    static void FirePixels(Texture2D t, int frame)
    {
        int sway = frame % 2 == 0 ? 0 : 2;
        Polygon(t, Hex("5B2C23"), new Vector2Int(4, 1), new Vector2Int(28, 1), new Vector2Int(24, 6), new Vector2Int(8, 6));
        Rect(t, 8, 3, 16, 3, Hex("A5502B")); Rect(t, 10, 4, 2, 2, Hex("E0A24A")); Rect(t, 20, 3, 2, 2, Hex("E0A24A"));
        Polygon(t, Hex("D84B25"), new Vector2Int(8 + sway, 6), new Vector2Int(7, 18 + frame % 3), new Vector2Int(13, 14), new Vector2Int(16, 34), new Vector2Int(24 - sway, 20), new Vector2Int(22, 7));
        Polygon(t, Hex("F08A32"), new Vector2Int(11 + sway, 7), new Vector2Int(11, 17), new Vector2Int(16, 13), new Vector2Int(19, 28), new Vector2Int(22 - sway, 17), new Vector2Int(20, 7));
        Polygon(t, Hex("FFD45A"), new Vector2Int(14, 7), new Vector2Int(13, 16), new Vector2Int(17, 21), new Vector2Int(20, 13), new Vector2Int(18, 8));
        Rect(t, 15, 10 + frame % 3, 3, 8, Hex("FFF0A3"));
    }

    static void BoarPixels(Texture2D t, int frame)
    {
        Color outline = Hex("241916"), shadow = Hex("482A22"), darkCoat = Hex("693A28");
        Color coat = Hex("985335"), mid = Hex("B96B40"), light = Hex("D48A50");
        Color belly = Hex("E1B78B"), blush = Hex("C96F68"), tusk = Hex("F2DDA9");
        Ellipse(t, 7, 9, 43, 29, outline);
        Ellipse(t, 9, 11, 39, 25, shadow);
        Ellipse(t, 11, 14, 35, 20, darkCoat);
        Ellipse(t, 15, 20, 28, 12, coat);
        Ellipse(t, 18, 25, 21, 7, mid);
        Ellipse(t, 14, 11, 26, 10, belly);
        Rect(t, 15, 13, 14, 2, Hex("EBC99D"));
        Rect(t, 17, 18, 2, 4, Hex("7F432F")); Rect(t, 28, 17, 2, 4, Hex("7F432F"));
        Rect(t, 20, 27, 8, 2, light); Pixel(t, 31, 25, light); Pixel(t, 13, 23, mid);

        Polygon(t, outline, new Vector2Int(22, 34), new Vector2Int(26, 43), new Vector2Int(30, 35));
        Polygon(t, outline, new Vector2Int(29, 36), new Vector2Int(34, 45), new Vector2Int(37, 36));
        Polygon(t, outline, new Vector2Int(36, 35), new Vector2Int(40, 42), new Vector2Int(43, 34));
        Polygon(t, mid, new Vector2Int(24, 35), new Vector2Int(26, 40), new Vector2Int(29, 35));
        Polygon(t, light, new Vector2Int(31, 37), new Vector2Int(34, 42), new Vector2Int(36, 37));
        Rect(t, 25, 35, 2, 3, Hex("E9A15D")); Rect(t, 34, 37, 2, 3, Hex("E9A15D"));

        Ellipse(t, 39, 15, 19, 20, outline);
        Ellipse(t, 41, 17, 16, 16, darkCoat);
        Ellipse(t, 43, 23, 12, 9, mid);
        Rect(t, 47, 21, 9, 8, outline); Rect(t, 49, 23, 8, 5, blush);
        Rect(t, 51, 24, 2, 2, Hex("F3A08B")); Pixel(t, 54, 25, outline); Pixel(t, 55, 25, outline);
        Pixel(t, 47, 30, outline); Pixel(t, 48, 31, outline); Pixel(t, 49, 31, Hex("F7E5BC"));
        Ellipse(t, 44, 29, 4, 4, Hex("FFF0C7")); Pixel(t, 46, 30, outline); Pixel(t, 45, 31, Hex("E6B75E"));
        Polygon(t, outline, new Vector2Int(42, 31), new Vector2Int(45, 38), new Vector2Int(49, 33));
        Polygon(t, Hex("A95B47"), new Vector2Int(43, 32), new Vector2Int(45, 36), new Vector2Int(47, 33));
        Rect(t, 41, 33, 3, 3, Hex("D18A52")); Pixel(t, 50, 32, light);

        int stride = frame switch { 0 => 0, 1 => 2, 2 => 0, _ => -2 };
        int lift = frame == 1 || frame == 3 ? 2 : 0;
        Leg(t, 14 + stride, 2 + lift, outline, shadow);
        Leg(t, 24 - stride, 2, outline, coat);
        Leg(t, 37 - stride, 2 + lift, outline, shadow);
        Leg(t, 45 + stride, 2, outline, coat);

        Pixel(t, 7, 24, outline); Pixel(t, 5, 25, shadow); Pixel(t, 4, 27, shadow);
        Pixel(t, 6, 29, mid); Pixel(t, 4, 30, outline); Pixel(t, 7, 31, outline);
        Pixel(t, 20, 17, light); Pixel(t, 22, 18, Hex("E39A5B"));
        Pixel(t, 32, 14, Hex("7D432E")); Pixel(t, 34, 15, Hex("E0A064"));
        Pixel(t, 38, 19, Hex("7C422C")); Pixel(t, 40, 20, light);
    }

    static void Leg(Texture2D t, int x, int y, Color outline, Color fill)
    {
        Rect(t, x, y, 7, 12, outline); Rect(t, x + 1, y + 2, 4, 9, fill);
        Rect(t, x, y, 7, 3, Hex("30201C")); Pixel(t, x + 1, y + 4, Hex("D09055"));
    }

    static void Fill(Texture2D t, Color c) { for (int y = 0; y < t.height; y++) for (int x = 0; x < t.width; x++) t.SetPixel(x, y, c); }
    internal static void Pixel(Texture2D t, int x, int y, Color c) { if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, c); }
    internal static void Rect(Texture2D t, int x, int y, int w, int h, Color c) { for (int a = x; a < x + w; a++) for (int b = y; b < y + h; b++) Pixel(t, a, b, c); }
    internal static void Ellipse(Texture2D t, int x, int y, int w, int h, Color c)
    {
        float rx = w * 0.5f, ry = h * 0.5f;
        for (int a = x; a < x + w; a++) for (int b = y; b < y + h; b++) { float dx = (a + 0.5f - x - rx) / rx, dy = (b + 0.5f - y - ry) / ry; if (dx * dx + dy * dy <= 1f) Pixel(t, a, b, c); }
    }
    internal static void Polygon(Texture2D t, Color c, params Vector2Int[] p)
    {
        int minX = p.Min(v => v.x), maxX = p.Max(v => v.x), minY = p.Min(v => v.y), maxY = p.Max(v => v.y);
        for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) { bool inside = false; for (int i = 0, j = p.Length - 1; i < p.Length; j = i++) if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (float)(p[j].y - p[i].y) + p[i].x) inside = !inside; if (inside) Pixel(t, x, y, c); }
    }
    static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out Color color); return color; }

    static void MakeInventory()
    {
        var root = new GameObject("CavemanInventory", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CavemanInventoryUI));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f; // same as the HUD
        var label = new GameObject("Inventory Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline)); label.transform.SetParent(root.transform, false);
        var rect = label.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = new Vector2(348, -192); rect.sizeDelta = new Vector2(552, 64); // inside the runtime Inventory Panel (48..648 x 160..224) under the clock plate, so it pops from its centre
        var text = label.GetComponent<Text>(); text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/VT323/VT323-Regular.ttf"); text.fontSize = 32; text.color = Hex("E9E8BF"); text.alignment = TextAnchor.MiddleLeft; text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
        text.text = "WOOD 0   STONE 0   AXE --   MAGNETS 0/3"; var outline = label.GetComponent<Outline>(); outline.effectColor = new Color(0.06f, 0.08f, 0.04f, 0.95f); outline.effectDistance = new Vector2(2, -2);
        var inventoryUI = root.GetComponent<CavemanInventoryUI>(); inventoryUI.inventoryText = text; inventoryUI.popScale = 1.08f; Save(root, Prefabs + "/CavemanInventory.prefab");
    }

    static void MakeEssentials()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/prefabs/Medieval/MedievalEssentials.prefab"); root.name = "CavemanEssentials";
        var info = root.GetComponent<LevelInfo>(); if (info == null) throw new InvalidOperationException("MedievalEssentials has no LevelInfo."); info.era = Era.Caveman;
        Transform player = Find(root.transform, "MedievalPlayer"); if (player != null) player.localPosition = new Vector3(-19.2f, 0.1f, 0);
        // Warm, dim light falling in through cracks in the cavern roof; the fires do the rest
        foreach (var sun in root.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Directional)) { sun.color = new Color(1f, 0.87f, 0.72f); sun.intensity = 0.72f; sun.shadows = LightShadows.Hard; }
        var inv = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/CavemanInventory.prefab"), root.transform) as GameObject; if (inv != null) inv.name = "CavemanInventory";
        PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/CavemanEssentials.prefab"); PrefabUtility.UnloadPrefabContents(root);
    }

    static void MakeDirector() { var go = new GameObject("LevelDirector"); go.AddComponent<CavemanLevel>(); Save(go, Prefabs + "/CavemanDirector.prefab"); }

    static void MakeBreakables()
    {
        BuildFirewoodPrefab();
        BuildRockPrefab();
        BreakablePrefab("CaveBramble", CavemanLevel.Resource.Wood, bramble, new Vector3(0, 0.7f, 0), 3, true, true, 0);
        AssetDatabase.DeleteAsset(Prefabs + "/StoneAxeWorkbench.prefab");
    }

    // Wood in the cave comes from the tribe's firewood stacks (there are no trees underground)
    static void BuildFirewoodPrefab()
    {
        AssetDatabase.DeleteAsset(Prefabs + "/CavemanTree.prefab");
        var root = new GameObject("CavemanFirewood");
        var trigger = root.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.center = new Vector3(0, 0.7f, 0); trigger.radius = 1.5f;
        var breakable = root.AddComponent<Breakable>(); breakable.resource = CavemanLevel.Resource.Wood; breakable.amount = 1; breakable.health = 3; breakable.destructionStyle = Breakable.DestructionStyle.Crumble;
        var prompt = new SerializedObject(breakable); prompt.FindProperty("prompt").stringValue = "Take firewood"; prompt.ApplyModifiedPropertiesWithoutUndo();
        breakable.visual = Billboard(root.transform, firewoodSprite, new Vector3(0, firewoodSprite.bounds.extents.y, 0));
        var solid = new GameObject("Solid Collider"); solid.transform.SetParent(root.transform, false);
        var box = solid.AddComponent<BoxCollider>(); box.center = new Vector3(0, 0.6f, 0); box.size = new Vector3(2.2f, 1.2f, 0.9f);
        Save(root, Prefabs + "/CavemanFirewood.prefab");
    }

    static void BuildRockPrefab()
    {
        var root = new GameObject("CavemanRock");
        var trigger = root.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.center = new Vector3(0, 0.55f, 0); trigger.radius = 1.15f;
        var breakable = root.AddComponent<Breakable>(); breakable.resource = CavemanLevel.Resource.Stone; breakable.amount = 1; breakable.health = 4; breakable.destructionStyle = Breakable.DestructionStyle.Crumble;
        var visual = Group("Visual", root.transform); breakable.visual = visual;
        Primitive(PrimitiveType.Sphere, "Main Boulder", visual, new Vector3(0, 0.62f, 0), new Vector3(1.7f, 1.16f, 1.45f), stoneMat);
        Primitive(PrimitiveType.Sphere, "Raised Face", visual, new Vector3(-0.28f, 0.96f, -0.12f), new Vector3(0.82f, 0.68f, 0.82f), stoneMat);
        Primitive(PrimitiveType.Sphere, "Broken Shoulder", visual, new Vector3(0.62f, 0.66f, 0.16f), new Vector3(0.7f, 0.78f, 0.8f), stoneMat);
        var solid = new GameObject("Rock Collider"); solid.transform.SetParent(root.transform, false);
        var box = solid.AddComponent<BoxCollider>(); box.center = new Vector3(0, 0.55f, 0); box.size = new Vector3(1.5f, 1.05f, 1.25f);
        Save(root, Prefabs + "/CavemanRock.prefab");
    }

    static void BreakablePrefab(string name, CavemanLevel.Resource type, Sprite art, Vector3 pos, int hp, bool axe, bool boarGate, int amount)
    {
        var go = new GameObject(name); var trigger = go.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.center = new Vector3(0, 0.7f, 0); trigger.radius = type == CavemanLevel.Resource.Wood ? 1f : 0.85f;
        var b = go.AddComponent<Breakable>(); b.resource = type; b.health = hp; b.requireAxe = axe; b.requireBoarKilled = boarGate; b.amount = amount; b.visual = Billboard(go.transform, art, pos);
        var solid = new GameObject("Solid Collider"); solid.transform.SetParent(go.transform, false); var box = solid.AddComponent<BoxCollider>(); box.center = new Vector3(0, type == CavemanLevel.Resource.Wood ? 0.65f : 0.3f, 0); box.size = type == CavemanLevel.Resource.Wood ? new Vector3(0.3f, 1.3f, 0.3f) : new Vector3(0.8f, 0.55f, 0.7f);
        Save(go, Prefabs + "/" + name + ".prefab");
    }

    static void MakeCraftingStone()
    {
        var go = new GameObject("FlatCraftingStone"); var range = go.AddComponent<SphereCollider>(); range.isTrigger = true; range.center = new Vector3(0, 0.3f, 0); range.radius = 1.35f; go.AddComponent<CraftingStation>();
        Cube("Flat Knapping Slab", go.transform, new Vector3(0, 0.18f, 0), new Vector3(1.65f, 0.32f, 1.2f), stoneMat);
        Primitive(PrimitiveType.Sphere, "Hammer Stone", go.transform, new Vector3(-0.38f, 0.43f, -0.05f), new Vector3(0.36f, 0.24f, 0.32f), stoneMat);
        Primitive(PrimitiveType.Cube, "Flint Core", go.transform, new Vector3(0.3f, 0.39f, 0.1f), new Vector3(0.4f, 0.07f, 0.24f), barkMat);
        Save(go, Prefabs + "/FlatCraftingStone.prefab");
    }

    static void MakeDecorPrefabs()
    {
        SpriteProp("CavemanLog", logSprite, 0.8f);
        SpriteProp("ClayPot", potSprite, 0.65f);
        SpriteProp("WovenBasket", basketSprite, 0.72f);
        SpriteProp("GatheredHerbs", herbSprite, 0.65f);
        SpriteProp("CaveGlyph", glyphSprite, 0.75f);

        var campfire = new GameObject("CavemanCampfire");
        var visual = new GameObject("Sprite"); visual.transform.SetParent(campfire.transform, false); visual.transform.localPosition = new Vector3(0, 0.75f, 0);
        var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = fireFrames[0]; renderer.sharedMaterial = spriteMat; renderer.sortingOrder = 0; visual.AddComponent<Billboard>();
        var animation = campfire.AddComponent<CavemanSpriteAnimation>(); animation.sprite = renderer; animation.frames = fireFrames; animation.framesPerSecond = 7f;
        var lightObject = new GameObject("Fire Light"); lightObject.transform.SetParent(campfire.transform, false); lightObject.transform.localPosition = new Vector3(0, 0.7f, 0);
        var fireLight = lightObject.AddComponent<Light>(); fireLight.type = LightType.Point; fireLight.color = Hex("FF9B42"); fireLight.intensity = 1.6f; fireLight.range = 5f;
        Save(campfire, Prefabs + "/CavemanCampfire.prefab");
    }

    static void SpriteProp(string name, Sprite art, float scale)
    {
        var root = new GameObject(name); var visual = new GameObject("Sprite"); visual.transform.SetParent(root.transform, false); visual.transform.localPosition = new Vector3(0, 0.38f, 0); visual.transform.localScale = Vector3.one * scale;
        var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = art; renderer.sharedMaterial = spriteMat; renderer.sortingOrder = 0; visual.AddComponent<Billboard>(); SpriteShadows.Add(visual);
        Save(root, Prefabs + "/" + name + ".prefab");
    }

    static void MakeBoar()
    {
        var go = new GameObject("WildBoar"); var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.constraints = RigidbodyConstraints.FreezeRotation;
        var hit = go.AddComponent<CapsuleCollider>(); hit.isTrigger = true; hit.center = new Vector3(0, 0.55f, 0); hit.radius = 0.78f; hit.height = 1.25f;
        var art = new GameObject("Sprite"); art.transform.SetParent(go.transform, false); art.transform.localPosition = new Vector3(0, 0.75f, 0); art.transform.localScale = Vector3.one * 0.58f;
        var renderer = art.AddComponent<SpriteRenderer>(); renderer.sprite = boar[0]; renderer.sharedMaterial = spriteMat; renderer.sortingOrder = 0; art.AddComponent<Billboard>(); SpriteShadows.Add(art);
        var animal = go.AddComponent<WildBoar>(); animal.visual = art.transform; animal.sprite = renderer; animal.walkFrames = boar; Save(go, Prefabs + "/WildBoar.prefab");
    }

    static void MakeStone()
    {
        var go = new GameObject("MagneticStone"); var range = go.AddComponent<SphereCollider>(); range.isTrigger = true; range.center = new Vector3(0, 0.7f, 0); range.radius = 0.9f;
        var stone = go.AddComponent<MagneticStone>();
        var visual = new GameObject("Faceted Crystal").transform; visual.SetParent(go.transform, false); visual.localPosition = new Vector3(0, 0.25f, 0);
        var filter = visual.gameObject.AddComponent<MeshFilter>(); filter.sharedMesh = CrystalMesh();
        var renderer = visual.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = crystalMat;
        stone.visual = visual;
        var glow = new GameObject("Glow"); glow.transform.SetParent(go.transform, false); glow.transform.localPosition = new Vector3(0, 0.75f, 0); var light = glow.AddComponent<Light>(); light.type = LightType.Point; light.color = Hex("64E8F0"); light.intensity = 1.6f; light.range = 3.5f;
        Save(go, Prefabs + "/MagneticStone.prefab");
    }

    static Mesh CrystalMesh()
    {
        const string path = Prefabs + "/MagneticCrystalMesh.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        var vertices = new Vector3[8]; vertices[0] = new Vector3(0, 0, 0); vertices[7] = new Vector3(0, 1.25f, 0);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI / 3f;
            float radius = i % 2 == 0 ? 0.38f : 0.31f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0.52f + (i % 2) * 0.08f, Mathf.Sin(angle) * radius);
        }
        var triangles = new int[36]; int index = 0;
        for (int i = 0; i < 6; i++)
        {
            int current = i + 1, next = (i + 1) % 6 + 1;
            triangles[index++] = 0; triangles[index++] = next; triangles[index++] = current;
            triangles[index++] = 7; triangles[index++] = current; triangles[index++] = next;
        }
        mesh = new Mesh { name = "Magnetic Crystal Mesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }

    static void MakeMachine()
    {
        string path = Prefabs + "/CavemanTimeMachine.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset("Assets/prefabs/Medieval/TimeMachine.prefab", path);
        GameObject root = PrefabUtility.LoadPrefabContents(path); root.name = "CavemanTimeMachine"; var machine = root.GetComponent<TimeMachine>();
        machine.loadNextEra = true; machine.inspectPrompt = "Inspect the temporal compass"; machine.repairPrompt = "Insert the magnetic stones";
        machine.brokenLines = new[] { "The temporal compass is cracked. Three magnetic stones should restore its navigation." };
        PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
    }

    static void MakeLevel()
    {
        var root = new GameObject("Level"); var yard = Group("1 Woodpile Yard", root.transform); var camp = Group("2 Camp", root.transform); var workshop = Group("3 Workshop", root.transform);
        var glen = Group("4 Boar Glen", root.transform); var cave = Group("5 Cave", root.transform);
        Place(Prefabs + "/CavemanFirewood.prefab", yard, new Vector3(-13.4f, 0, -2.8f)); Place(Prefabs + "/CavemanFirewood.prefab", yard, new Vector3(-10.6f, 0, 5.4f)); Place(Prefabs + "/CavemanFirewood.prefab", yard, new Vector3(-11f, 0, -4.6f));
        Place(Prefabs + "/CavemanRock.prefab", yard, new Vector3(-11, 0, 2)); Place(Prefabs + "/CavemanRock.prefab", yard, new Vector3(-8, 0, -3)); Place(Prefabs + "/CavemanRock.prefab", yard, new Vector3(-7, 0, 3));
        Place(Prefabs + "/CavemanTimeMachine.prefab", camp, new Vector3(-21, 0, 0)); Place(Prefabs + "/FlatCraftingStone.prefab", workshop, new Vector3(-3.5f, 0, 0)); Place(Prefabs + "/WildBoar.prefab", glen, new Vector3(3.5f, 0, 0));
        Place(Prefabs + "/CaveBramble.prefab", cave, new Vector3(8.5f, 0, 0)); Place(Prefabs + "/MagneticStone.prefab", cave, new Vector3(14.5f, 0, -2.2f)); Place(Prefabs + "/MagneticStone.prefab", cave, new Vector3(18, 0, 2.4f)); Place(Prefabs + "/MagneticStone.prefab", cave, new Vector3(21.3f, 0, -1.5f));
        Place(Prefabs + "/CaveGlyph.prefab", cave, new Vector3(12.1f, 1.1f, -4.8f)); Place(Prefabs + "/CaveGlyph.prefab", cave, new Vector3(12.1f, 1.1f, 4.8f));
        Cube("Cave Earth Floor", root.transform, new Vector3(0, -0.3f, 0), new Vector3(54, 0.6f, 26), floorMat);
        Cube("Dark Cave Floor", cave, new Vector3(16, 0.015f, 0), new Vector3(17, 0.08f, 12), caveMat);
        // Mountains, the cave mountain, and per-rock variation (also resizes the floor and fits the bramble to the entrance)
        CavemanNature.Apply(root.transform);
        // The tribe's settlement: paths, bonfire, villagers, pillars, torches, paintings on the cavern wall
        CavemanSettlement.Apply(root.transform);
        Save(root, Prefabs + "/CavemanLevel.prefab");
    }

    static void AddBuildScenes()
    {
        const string medieval = "Assets/Scenes/Medieval.unity";
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != medieval).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); scenes.Insert(1, new EditorBuildSettingsScene(medieval, true)); EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
    }

    static void Instantiate(string path) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (p == null) throw new InvalidOperationException(path); PrefabUtility.InstantiatePrefab(p); }
    static void Place(string path, Transform parent, Vector3 pos) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (p == null) throw new InvalidOperationException(path); var go = PrefabUtility.InstantiatePrefab(p) as GameObject; go.transform.SetParent(parent, false); go.transform.localPosition = pos; }
    static Transform Group(string name, Transform parent) { var g = new GameObject(name).transform; g.SetParent(parent, false); return g; }
    static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat) { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat; return go; }
    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; var collider = go.GetComponent<Collider>(); if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat; return go;
    }
    static void Bump(Transform parent, Vector3 pos, Vector3 scale) { var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Cave Rock"; go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = stoneMat; }
    static Transform Billboard(Transform parent, Sprite art, Vector3 pos) { var go = new GameObject("Visual"); go.transform.SetParent(parent, false); go.transform.localPosition = pos; var r = go.AddComponent<SpriteRenderer>(); r.sprite = art; r.sharedMaterial = spriteMat; r.sortingOrder = 0; go.AddComponent<Billboard>(); SpriteShadows.Add(go); return go.transform; }
    static Transform Find(Transform parent, string name) { foreach (Transform child in parent) { if (child.name == name) return child; Transform result = Find(child, name); if (result != null) return result; } return null; }
    static void Save(GameObject root, string path) { PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root); }
}