using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the Steampunk Era: pixel-art textures and materials, the factory's prefabs, and Assets/Scenes/Steampunk.unity.
// Run via Tools > Out Of Time > Build Steampunk Era. Safe to run again: it rebuilds its own assets and the scene,
// and touches nothing outside the Steampunk folders (apart from adding the scene to Build Settings after Medieval).
//
// Look: a dark Victorian factory. Bronze, brass and riveted iron; warm amber bulbs against cool daylight from the
// arched windows and roof glass; thick flanged pipes, wall gears, a great clock, steam everywhere. All of it with
// 32px point-filtered textures on chunky 3D shapes plus billboard sprites, like the other eras.
// The level layout itself lives in SteampunkFactory.cs.
public static partial class SteampunkSceneBuilder
{
    const string Prefabs = "Assets/prefabs/Steampunk";
    const string Meshes = "Assets/prefabs/Steampunk/Meshes";
    const string Mats = "Assets/Materials/Steampunk";
    const string Sprites = "Assets/Sprites/Steampunk";
    const string Textures = "Assets/Textures/Steampunk";
    const string ScenePath = "Assets/Scenes/Steampunk.unity";

    static Material floorMat, brickMat, ironMat, brassMat, copperMat, woodMat, beltMat, grateMat, glassMat, lampMat, glowMat, furnaceMat, soot, redMat, clockMat, gaugeMat, darkWoodMat;
    static Material spriteMat, puffMat;
    static Sprite puffSprite, sparkSprite;

    [MenuItem("Tools/Out Of Time/Build Steampunk Era")]
    public static void Build()
    {
        Scene original = SceneManager.GetActiveScene();
        if (original.isDirty) { Debug.LogError("Save or close the dirty scene before building Steampunk."); return; }
        Folder("Assets/prefabs", "Steampunk"); Folder(Prefabs, "Meshes"); Folder("Assets/Materials", "Steampunk");
        Folder("Assets/Sprites", "Steampunk"); Folder("Assets/Textures", "Steampunk");
        MakeArt();
        Scene staging = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(staging);
        try
        {
            MakeEssentials();
            MakeProps(); MakeHazards(); MakeItems(); MakeConveyor(); MakeLift(); MakeNpcs(); MakeGuard(); MakeMachine();
            MakeLevel();
        }
        finally
        {
            EditorSceneManager.CloseScene(staging, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Instantiate(Prefabs + "/SteampunkEssentials.prefab"); Instantiate(Prefabs + "/SteampunkLevel.prefab");
        // Inside the factory: dim bronze ambient, so the amber bulbs and the cool window light carry the scene
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.44f, 0.39f, 0.35f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddBuildScene();
        Debug.Log("Built Steampunk prefabs and " + ScenePath + ".");
    }

    // ---------------------------------------------------------------- art

    static void MakeArt()
    {
        floorMat = Material("IronFloor", Texture("IronFloor32", 32, IronFloorPixel())); floorMat.SetFloat("_OOT_WorldUV", 1f); floorMat.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));
        brickMat = Material("FactoryBrick", Texture("Brick32", 32, BrickPixel())); brickMat.SetFloat("_OOT_WorldUV", 1f); brickMat.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));
        ironMat = Material("RivetedIron", Texture("Iron32", 32, PlatePixel(new[] { "28221E", "3A332D", "4A4139", "62564A" })));
        brassMat = Material("Brass", Texture("Brass32", 32, PlatePixel(new[] { "4E3415", "7A5520", "A87A33", "D9AA58" })));
        copperMat = Material("Copper", Texture("Copper32", 32, PlatePixel(new[] { "4A2416", "73391F", "9E5630", "C9814E" }, true)));
        woodMat = Material("Planks", Texture("Planks32", 32, PlankPixel("4A3220", "7A5232", "9A6A3E")));
        darkWoodMat = Material("DarkPlanks", Texture("DarkPlanks32", 32, PlankPixel("33241A", "4E3624", "60442C")));
        beltMat = Material("BeltRubber", Texture("Belt32", 32, (x, y) => (y % 8) < 2 ? Hex("2F2A26") : (y % 8) == 2 ? Hex("13110F") : Hex("1E1A17")));
        beltMat.SetTextureScale("_BaseMap", new Vector2(1f, 6f));
        grateMat = Material("Grate", Texture("Grate32", 32, (x, y) => (x % 8 < 2 || y % 16 < 2) ? Hex("4A413A") : (x % 8 == 2 ? Hex("120F0D") : Hex("1A1613"))));
        glassMat = Material("WindowGlass", Texture("Glass32", 32, GlassPixel()), new Color(0.48f, 0.62f, 0.70f) * 1.15f);
        lampMat = Material("AmberBulb", Texture("Bulb32", 32, (x, y) => (x + y) % 9 == 0 ? Hex("FFF2C2") : Hex("FFC266")), Hex("FFB347") * 2.4f);
        glowMat = Material("GaugeGlow", Texture("Glow32", 32, (x, y) => (x / 4 + y / 4) % 3 == 0 ? Hex("F7D27A") : Hex("D99A3E")), Hex("F0A040") * 1.6f);
        furnaceMat = Material("FurnaceFire", Texture("Fire32", 32, (x, y) => ((x * 7 + y * 13) % 11) < 3 ? Hex("FFE08A") : ((x + y * 3) % 5 == 0 ? Hex("D8401A") : Hex("FF7A24"))), Hex("FF6A20") * 2.6f);
        soot = Material("Soot", Texture("Soot32", 32, (x, y) => ((x * 31 + y * 17) % 13) == 0 ? Hex("2A2522") : Hex("151210")));
        redMat = Material("RedPaint", Texture("Red32", 32, PlatePixel(new[] { "3A0F0C", "5E1A14", "8A2A1E", "B8452E" })));
        clockMat = Material("ClockFace", Texture("ClockFace64", 64, ClockFacePixel()), Hex("F3DFA8") * 0.35f);
        clockMat.SetTextureScale("_BaseMap", Vector2.one);
        gaugeMat = Material("GaugeFace", Texture("Gauge16", 16, GaugePixel()));
        spriteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLit.mat");
        puffMat = FindMaterial("Sprite-Unlit-Default") ?? spriteMat;
        puffSprite = Sprite("SteamPuff", 12, 12, PuffPixels, 16f);
        sparkSprite = Sprite("Spark", 4, 4, t => { Rect(t, 1, 0, 2, 4, Hex("FFD27A")); Rect(t, 0, 1, 4, 2, Hex("FF9A3A")); Pixel(t, 1, 1, Hex("FFF6D6")); }, 16f);
    }

    static Material FindMaterial(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null && m.name == name) return m;
        }
        return null;
    }

    // Riveted iron floor plates: 16px plates, a dark seam with a lit edge, rivets in the corners, soft wear drifts
    static Func<int, int, Color> IronFloorPixel() => (x, y) =>
    {
        int px = x % 16, py = y % 16;
        if (px == 0 || py == 0) return Hex("191513");
        if (px == 1 || py == 1) return Hex("4A4038");
        if ((px == 3 || px == 13) && (py == 3 || py == 13)) return Hex("6A5D50");
        if ((px == 3 || px == 13) && (py == 2 || py == 12)) return Hex("221D1A");
        float v = Mathf.Sin((x + 2 * y) * 0.39f) + Mathf.Sin((3 * x - y) * 0.2f);
        if (((x * 13 + y * 7) % 29) == 0) return Hex("4F453C");
        return v > 0.8f ? Hex("3A332D") : v < -0.8f ? Hex("2A2420") : Hex("332C27");
    };

    // Soot-darkened red brick with deep mortar
    static Func<int, int, Color> BrickPixel() => (x, y) =>
    {
        int row = y / 8, bx = (x + (row % 2) * 8) % 16, by = y % 8;
        if (by == 0 || bx == 0) return Hex("1C1512");
        int brick = ((x + (row % 2) * 8) / 16 * 7 + row * 3) % 4;
        string[] tones = { "4E2A20", "5C3326", "43241C", "6A3B2A" };
        if (by == 7) return Hex("6E4232");
        if (by == 1 && bx < 3) return Hex("2E1C16");
        return Hex(tones[brick]);
    };

    // Metal plate: banded body with a bright rim, a dark seam and rivets every 16px (+ green patina flecks for copper)
    static Func<int, int, Color> PlatePixel(string[] c, bool patina = false) => (x, y) =>
    {
        int py = y % 16;
        if (py == 0) return Hex(c[0]);
        if (py == 1) return Hex(c[3]);
        if (py == 3 && x % 8 == 4) return Hex(c[3]);
        if (py == 2 && x % 8 == 4) return Hex(c[0]);
        if (patina && ((x * 11 + y * 5) % 37) == 0) return Hex("5E8C6A");
        float v = Mathf.Sin(x * 0.4f + y * 0.1f);
        return py < 6 ? Hex(c[2]) : v > 0.6f ? Hex(c[2]) : Hex(c[1]);
    };

    static Func<int, int, Color> PlankPixel(string dark, string mid, string light) => (x, y) =>
    {
        int py = y % 8, plank = y / 8;
        if (py == 0) return Hex(dark);
        if ((x + plank * 11) % 32 == 0) return Hex(dark);
        if (py == 4 && (x + plank * 5) % 16 == 3) return Hex(dark);
        return ((x + plank * 3) / 5) % 3 == 0 ? Hex(light) : Hex(mid);
    };

    // Window panes in an iron frame: cool daylight, a diagonal glint
    static Func<int, int, Color> GlassPixel() => (x, y) =>
    {
        if (x % 16 == 0 || y % 16 == 0 || x % 16 == 15 || y % 16 == 15) return Hex("1E1A17");
        if ((x - y + 64) % 16 == 3) return Hex("E4F0EE");
        return y % 16 > 9 ? Hex("9DBCC4") : Hex("7E9FAA");
    };

    // A cream clock face with a brass ring, hour ticks and a dark centre (hands are separate, so they can move)
    static Func<int, int, Color> ClockFacePixel()
    {
        var face = new Color[64, 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = x - 31.5f, dy = y - 31.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = r > 31.5f ? Hex("1C1512") : r > 29f ? Hex("6B4A1F") : r > 27f ? Hex("D9AA58") : r > 25.5f ? Hex("8A6326") : Hex("E8D7AE");
                if (r <= 25.5f && r > 20f)
                {
                    float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f;
                    float tick = Mathf.Repeat(a + 3.75f, 30f);
                    if (tick < 7.5f && r > 21f) c = Hex("2E2218");
                    else if (Mathf.Repeat(a + 1.5f, 6f) < 3f && r > 23.5f) c = Hex("6E5A3C");
                }
                if (r < 12f && r > 10.5f) c = Hex("C9B48A");
                if (r < 2.5f) c = Hex("2E2218");
                face[x, y] = c;
            }
        return (x, y) => face[x, y];
    }

    static Func<int, int, Color> GaugePixel() => (x, y) =>
    {
        float dx = x - 7.5f, dy = y - 7.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
        if (r > 7.5f) return Hex("1C1512");
        if (r > 6.2f) return Hex("C8913E");
        if (dy > 1 && Mathf.Abs(dx - dy * 0.6f) < 0.8f && r < 5.5f) return Hex("8A2A1E");
        if (r < 1.2f) return Hex("2E2218");
        return dy < -3 && Mathf.Abs(dx) < 4 ? Hex("C8B994") : Hex("EADDB8");
    };

    static void PuffPixels(Texture2D t)
    {
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < 12; x++)
            {
                float dx = x - 5.5f, dy = y - 5.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r < 3.2f) Pixel(t, x, y, new Color(1f, 1f, 1f, 0.95f));
                else if (r < 4.6f) Pixel(t, x, y, new Color(0.92f, 0.92f, 0.92f, 0.7f));
                else if (r < 5.8f && (x + y) % 2 == 0) Pixel(t, x, y, new Color(0.85f, 0.85f, 0.85f, 0.45f));
            }
    }

    static Material Material(string name, Texture texture, Color? emission = null)
    {
        string path = Mats + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Out Of Time/Lit Cutout") ?? Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", texture); m.color = Color.white;
        if (emission.HasValue)
        {
            // RealtimeEmissive: with the default "emissive is black" flag, URP's material check strips _EMISSION on save
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.SetTexture("_EmissionMap", texture);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m); return m;
    }

    static Texture2D Texture(string name, int size, Func<int, int, Color> pixel)
    {
        string path = Textures + "/" + name + ".png";
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) t.SetPixel(x, y, pixel(x, y));
        t.Apply(); File.WriteAllBytes(Path.GetFullPath(path), t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Default; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = false; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Sprite Sprite(string name, int w, int h, Action<Texture2D> draw, float pixelsPerUnit)
    {
        string path = Sprites + "/" + name + ".png";
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) t.SetPixel(x, y, new Color(0, 0, 0, 0));
        draw(t); t.Apply(); File.WriteAllBytes(Path.GetFullPath(path), t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.spritePixelsPerUnit = pixelsPerUnit; importer.alphaIsTransparency = true; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void Pixel(Texture2D t, int x, int y, Color c) { if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, c); }
    static void Rect(Texture2D t, int x, int y, int w, int h, Color c) { for (int a = x; a < x + w; a++) for (int b = y; b < y + h; b++) Pixel(t, a, b, c); }
    static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out Color color); return color; }

    // A flat-shaded gear in the local XY plane (teeth facing the camera), turning about local Z.
    // Saved as a mesh asset so prefabs can share it
    static Mesh GearMesh(int teeth, float rootRadius, float tipRadius, float thickness)
    {
        string path = $"{Meshes}/Gear_{teeth}_{rootRadius:0.00}_{tipRadius:0.00}_{thickness:0.00}.asset";
        var outline = new System.Collections.Generic.List<Vector2>();
        int steps = teeth * 4;
        for (int i = 0; i < steps; i++)
        {
            float a = i * Mathf.PI * 2f / steps;
            float r = (i % 4 == 1 || i % 4 == 2) ? tipRadius : rootRadius;
            outline.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
        var v = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>(); var tri = new System.Collections.Generic.List<int>();
        float h = thickness * 0.5f;
        void Face(float z, bool front)
        {
            int c = v.Count; v.Add(new Vector3(0, 0, z)); uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < outline.Count; i++) { v.Add(new Vector3(outline[i].x, outline[i].y, z)); uv.Add(outline[i] / (tipRadius * 2f) + Vector2.one * 0.5f); }
            for (int i = 0; i < outline.Count; i++)
            {
                int a = c + 1 + i, b = c + 1 + (i + 1) % outline.Count;
                if (front) { tri.Add(c); tri.Add(b); tri.Add(a); } else { tri.Add(c); tri.Add(a); tri.Add(b); }
            }
        }
        Face(-h, true); Face(h, false);
        for (int i = 0; i < outline.Count; i++)
        {
            Vector2 p = outline[i], q = outline[(i + 1) % outline.Count];
            int c = v.Count;
            v.Add(new Vector3(p.x, p.y, -h)); v.Add(new Vector3(q.x, q.y, -h)); v.Add(new Vector3(q.x, q.y, h)); v.Add(new Vector3(p.x, p.y, h));
            float len = (q - p).magnitude;
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(len, 0)); uv.Add(new Vector2(len, thickness)); uv.Add(new Vector2(0, thickness));
            tri.Add(c); tri.Add(c + 2); tri.Add(c + 1); tri.Add(c); tri.Add(c + 3); tri.Add(c + 2);
        }
        // Rebuilt in place when it already exists, so the asset keeps its GUID
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear(); mesh.name = Path.GetFileNameWithoutExtension(path);
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // ---------------------------------------------------------------- shared prefabs

    static void MakeEssentials()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/prefabs/Medieval/MedievalEssentials.prefab"); root.name = "SteampunkEssentials";
        var info = root.GetComponent<LevelInfo>(); if (info == null) throw new InvalidOperationException("MedievalEssentials has no LevelInfo."); info.era = Era.Steampunk;
        // Arrives above the broken first-floor boards and drops through them onto the ground floor
        Transform player = Find(root.transform, "MedievalPlayer"); if (player != null) player.localPosition = SteampunkFactory.ArrivalPoint;
        // Cool daylight through the roof glass; the amber bulbs inside do the rest
        foreach (var sun in root.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Directional)) { sun.color = new Color(0.78f, 0.86f, 1f); sun.intensity = 0.95f; sun.shadows = LightShadows.Hard; }
        PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/SteampunkEssentials.prefab"); PrefabUtility.UnloadPrefabContents(root);
    }

    static void MakeProps()
    {
        // Wooden crate with iron corner straps
        var crate = new GameObject("Crate");
        Box("Body", crate.transform, new Vector3(0, 0.5f, 0), Vector3.one, woodMat, true);
        foreach (float x in new[] { -0.47f, 0.47f }) foreach (float z in new[] { -0.47f, 0.47f }) Box("Strap", crate.transform, new Vector3(x, 0.5f, z), new Vector3(0.1f, 1.02f, 0.1f), ironMat);
        Save(crate, Prefabs + "/Crate.prefab");

        var barrel = new GameObject("Barrel");
        Cyl("Body", barrel.transform, new Vector3(0, 0.55f, 0), new Vector3(0.8f, 0.55f, 0.8f), darkWoodMat, true);
        Cyl("Hoop", barrel.transform, new Vector3(0, 0.25f, 0), new Vector3(0.84f, 0.04f, 0.84f), ironMat);
        Cyl("Hoop", barrel.transform, new Vector3(0, 0.85f, 0), new Vector3(0.84f, 0.04f, 0.84f), ironMat);
        Cyl("Lid", barrel.transform, new Vector3(0, 1.11f, 0), new Vector3(0.7f, 0.02f, 0.7f), woodMat);
        Save(barrel, Prefabs + "/Barrel.prefab");

        // Storage shelving with spare parts, boxes and coils of rope
        var shelf = new GameObject("StorageShelf");
        Box("Collider", shelf.transform, new Vector3(0, 1.6f, 0), new Vector3(2.6f, 3.2f, 1.1f), null, true);
        foreach (float x in new[] { -1.25f, 1.25f }) foreach (float z in new[] { -0.5f, 0.5f }) Box("Post", shelf.transform, new Vector3(x, 1.6f, z), new Vector3(0.1f, 3.2f, 0.1f), ironMat);
        for (int i = 0; i < 4; i++)
        {
            float y = 0.15f + i * 0.95f;
            Box("Shelf", shelf.transform, new Vector3(0, y, 0), new Vector3(2.55f, 0.08f, 1.05f), darkWoodMat);
            if (i == 3) continue;
            Box("Box", shelf.transform, new Vector3(-0.7f + (i % 2) * 0.3f, y + 0.3f, 0), new Vector3(0.7f, 0.5f, 0.6f), woodMat);
            Cyl("Spare cog", shelf.transform, new Vector3(0.55f, y + 0.32f, -0.1f), new Vector3(0.55f, 0.05f, 0.55f), brassMat, false, new Vector3(90, 0, 0));
            Cyl("Pipe stock", shelf.transform, new Vector3(0.1f, y + 0.12f, 0.2f), new Vector3(0.14f, 1f, 0.14f), copperMat, false, new Vector3(0, 0, 90));
        }
        Save(shelf, Prefabs + "/StorageShelf.prefab");

        // Copper boiler: riveted drum, brass bands, a pressure gauge, a valve venting a wisp of steam
        var boiler = new GameObject("Boiler");
        Cyl("Collider", boiler.transform, new Vector3(0, 1.4f, 0), new Vector3(2.2f, 1.4f, 2.2f), null, true);
        Cyl("Drum", boiler.transform, new Vector3(0, 1.4f, 0), new Vector3(2.1f, 1.3f, 2.1f), copperMat);
        Sphere("Dome", boiler.transform, new Vector3(0, 2.7f, 0), new Vector3(2.0f, 0.9f, 2.0f), copperMat);
        foreach (float y in new[] { 0.5f, 1.4f, 2.3f }) Cyl("Band", boiler.transform, new Vector3(0, y, 0), new Vector3(2.16f, 0.06f, 2.16f), brassMat);
        Cyl("Firebox", boiler.transform, new Vector3(0, 0.2f, 0), new Vector3(2.3f, 0.2f, 2.3f), soot);
        Box("Fire door", boiler.transform, new Vector3(0, 0.45f, -1.05f), new Vector3(0.7f, 0.45f, 0.08f), furnaceMat);
        Cyl("Gauge", boiler.transform, new Vector3(0.55f, 1.9f, -1.0f), new Vector3(0.45f, 0.03f, 0.45f), gaugeMat, false, new Vector3(-90, 0, 0));
        Cyl("Stack", boiler.transform, new Vector3(0, 3.9f, 0), new Vector3(0.45f, 1.0f, 0.45f), ironMat);
        var wisp = Emitter("Valve steam", boiler.transform, new Vector3(0.4f, 3.1f, 0), 3f, 0.6f, 0.25f, 1.0f);
        wisp.lifetime = 2f;
        Save(boiler, Prefabs + "/Boiler.prefab");

        // Hanging Edison-style lamp: brass shade, glowing amber bulb, warm point light
        var lamp = new GameObject("FactoryLamp");
        Cyl("Cord", lamp.transform, new Vector3(0, 0.8f, 0), new Vector3(0.04f, 0.8f, 0.04f), soot);
        Cyl("Shade", lamp.transform, new Vector3(0, 0.1f, 0), new Vector3(0.75f, 0.12f, 0.75f), brassMat);
        Sphere("Bulb", lamp.transform, new Vector3(0, -0.12f, 0), new Vector3(0.32f, 0.38f, 0.32f), lampMat);
        var bulbLight = Light("Light", lamp.transform, new Vector3(0, -0.4f, 0), Hex("FFB55A"), 2.2f, 8f);
        var flicker = lamp.AddComponent<TorchFlicker>(); flicker.flame = bulbLight; flicker.flickerAmount = 0.06f; flicker.flickerSpeed = 3f;
        Save(lamp, Prefabs + "/FactoryLamp.prefab");

        // Control console: an iron desk with glowing gauges, levers and switches
        var console = new GameObject("ControlConsole");
        Box("Desk", console.transform, new Vector3(0, 0.5f, 0), new Vector3(2.0f, 1.0f, 0.9f), ironMat, true);
        var panel = Box("Panel", console.transform, new Vector3(0, 1.15f, 0.2f), new Vector3(2.0f, 0.5f, 0.12f), brassMat);
        panel.transform.localRotation = Quaternion.Euler(-25, 0, 0);
        for (int i = 0; i < 3; i++) Cyl("Gauge", console.transform, new Vector3(-0.6f + i * 0.6f, 1.2f, 0.1f), new Vector3(0.35f, 0.02f, 0.35f), gaugeMat, false, new Vector3(-115, 0, 0));
        for (int i = 0; i < 5; i++) Box("Lamp", console.transform, new Vector3(-0.8f + i * 0.4f, 1.0f, -0.35f), new Vector3(0.12f, 0.06f, 0.12f), i % 2 == 0 ? glowMat : redMat);
        Box("Lever", console.transform, new Vector3(0.85f, 1.25f, -0.2f), new Vector3(0.06f, 0.5f, 0.06f), brassMat).transform.localRotation = Quaternion.Euler(-20, 0, 0);
        Light("Glow", console.transform, new Vector3(0, 1.5f, -0.6f), Hex("F0A040"), 0.9f, 3f);
        Save(console, Prefabs + "/ControlConsole.prefab");

        // Mechanic's workbench with a vice, tools, gears and a lamp
        var bench = new GameObject("Workbench");
        Box("Collider", bench.transform, new Vector3(0, 0.5f, 0), new Vector3(3.2f, 1.0f, 1.2f), null, true);
        Box("Top", bench.transform, new Vector3(0, 0.95f, 0), new Vector3(3.2f, 0.12f, 1.2f), woodMat);
        foreach (float x in new[] { -1.5f, 1.5f }) Box("Leg", bench.transform, new Vector3(x, 0.45f, 0), new Vector3(0.12f, 0.9f, 1.1f), ironMat);
        Box("Vice", bench.transform, new Vector3(-1.1f, 1.15f, -0.3f), new Vector3(0.35f, 0.3f, 0.3f), ironMat);
        Box("Wrench", bench.transform, new Vector3(-0.2f, 1.03f, -0.2f), new Vector3(0.6f, 0.04f, 0.08f), ironMat);
        Box("Hammer", bench.transform, new Vector3(0.4f, 1.04f, 0.1f), new Vector3(0.12f, 0.06f, 0.5f), darkWoodMat);
        Cyl("Cog", bench.transform, new Vector3(0.9f, 1.03f, -0.1f), new Vector3(0.45f, 0.03f, 0.45f), brassMat);
        Cyl("Cog", bench.transform, new Vector3(1.25f, 1.03f, 0.25f), new Vector3(0.3f, 0.03f, 0.3f), copperMat);
        Box("Tool board", bench.transform, new Vector3(0, 1.9f, 0.55f), new Vector3(3.0f, 1.4f, 0.06f), darkWoodMat);
        for (int i = 0; i < 6; i++) Box("Tool", bench.transform, new Vector3(-1.2f + i * 0.48f, 1.9f, 0.5f), new Vector3(0.07f, 0.7f - (i % 3) * 0.15f, 0.04f), i % 2 == 0 ? ironMat : brassMat);
        Save(bench, Prefabs + "/Workbench.prefab");

        // Big wall clock with hands that turn in world time (the era of the clock: this game's theme)
        var clock = new GameObject("FactoryClock");
        var face = GameObject.CreatePrimitive(PrimitiveType.Quad); face.name = "Face"; UnityEngine.Object.DestroyImmediate(face.GetComponent<Collider>());
        face.transform.SetParent(clock.transform, false); face.transform.localScale = new Vector3(4.4f, 4.4f, 1f); face.GetComponent<Renderer>().sharedMaterial = clockMat;
        Cyl("Rim", clock.transform, new Vector3(0, 0, 0.08f), new Vector3(4.7f, 0.06f, 4.7f), brassMat, false, new Vector3(90, 0, 0));
        Hand(clock.transform, "Hour hand", 1.15f, 0.16f, 3f);
        Hand(clock.transform, "Minute hand", 1.7f, 0.1f, 36f);
        Cyl("Pin", clock.transform, new Vector3(0, 0, -0.12f), new Vector3(0.22f, 0.05f, 0.22f), brassMat, false, new Vector3(90, 0, 0));
        Save(clock, Prefabs + "/FactoryClock.prefab");

        // Decorative wall gear that turns slowly (wall machinery)
        var wallGear = new GameObject("WallGear");
        var g = MeshObject("Gear", wallGear.transform, Vector3.zero, GearMesh(18, 1.25f, 1.5f, 0.25f), brassMat);
        var spin = g.AddComponent<GearSpinner>(); spin.degreesPerSecond = 12f;
        Cyl("Hub", g.transform, new Vector3(0, 0, -0.16f), new Vector3(0.6f, 0.08f, 0.6f), ironMat, false, new Vector3(90, 0, 0));
        Save(wallGear, Prefabs + "/WallGear.prefab");
    }

    static void Hand(Transform clock, string name, float length, float width, float degreesPerSecond)
    {
        var pivot = new GameObject(name); pivot.transform.SetParent(clock, false); pivot.transform.localPosition = new Vector3(0, 0, -0.06f);
        Box("Blade", pivot.transform, new Vector3(0, length * 0.42f, 0), new Vector3(width, length, 0.04f), soot);
        var s = pivot.AddComponent<GearSpinner>(); s.degreesPerSecond = -degreesPerSecond; s.spinUpTime = 0f;
    }

    static void MakeHazards()
    {
        // Floor vent: an iron grate with a valve lamp; quiet, a warning hiss, then a scalding column of steam
        var vent = new GameObject("SteamVent");
        Box("Grate", vent.transform, new Vector3(0, 0.03f, 0), new Vector3(1.6f, 0.06f, 1.6f), grateMat);
        Box("Frame", vent.transform, new Vector3(0, 0.02f, 0), new Vector3(1.8f, 0.04f, 1.8f), ironMat);
        Cyl("Pipe", vent.transform, new Vector3(0.95f, 0.35f, 0.7f), new Vector3(0.25f, 0.35f, 0.25f), copperMat);
        var valveLamp = Sphere("Valve lamp", vent.transform, new Vector3(0.95f, 0.78f, 0.7f), Vector3.one * 0.2f, brassMat);
        var hazardGo = new GameObject("Steam column (hazard)"); hazardGo.transform.SetParent(vent.transform, false);
        var box = hazardGo.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(0, 1.3f, 0); box.size = new Vector3(1.5f, 2.6f, SteampunkFactory.VentDepth);
        var hazard = hazardGo.AddComponent<Hazard>();
        var steam = Emitter("Steam", vent.transform, new Vector3(0, 0.1f, 0), 0f, 5.5f, 0.35f, 1.3f);
        steam.spread = 0.18f; steam.lifetime = 0.75f; steam.maxPuffs = 80;
        var glow = Light("Heat glow", vent.transform, new Vector3(0, 0.6f, 0), Hex("FF8A3A"), 0f, 3.5f);
        var sv = vent.AddComponent<SteamVent>(); sv.hazard = hazard; sv.steam = steam; sv.warningLight = valveLamp.GetComponent<Renderer>(); sv.glow = glow;
        Save(vent, Prefabs + "/SteamVent.prefab");

        // Stamping press straddling the conveyor: brass head on an iron frame
        var press = new GameObject("SteamPress");
        foreach (float x in new[] { -1.55f, 1.55f }) Box("Column", press.transform, new Vector3(x, 1.9f, 0), new Vector3(0.35f, 3.8f, 0.6f), ironMat, true);
        Box("Crown", press.transform, new Vector3(0, 3.95f, 0), new Vector3(3.6f, 0.5f, 0.9f), ironMat);
        Cyl("Cylinder", press.transform, new Vector3(0, 3.4f, 0), new Vector3(0.9f, 0.45f, 0.9f), copperMat);
        var head = new GameObject("Head"); head.transform.SetParent(press.transform, false); head.transform.localPosition = new Vector3(0, 2.75f, 0);
        Cyl("Ram", head.transform, new Vector3(0, 0.5f, 0), new Vector3(0.3f, 0.5f, 0.3f), ironMat);
        Box("Die", head.transform, new Vector3(0, 0, 0), new Vector3(2.2f, 0.4f, 1.1f), brassMat);
        Box("Warning stripe", head.transform, new Vector3(0, -0.15f, -0.56f), new Vector3(2.2f, 0.1f, 0.02f), redMat);
        var pressHazard = head.AddComponent<BoxCollider>(); pressHazard.isTrigger = true; pressHazard.center = new Vector3(0, -0.3f, 0); pressHazard.size = new Vector3(2.2f, 0.8f, 1.1f);
        var ph = head.AddComponent<Hazard>();
        var slam = Emitter("Slam steam", press.transform, new Vector3(0, 0.7f, 0), 0f, 2.5f, 0.4f, 1.1f);
        slam.direction = Vector3.up; slam.spread = 0.9f; slam.burstCount = 14;
        var sp = press.AddComponent<SteamPress>(); sp.head = head.transform; sp.hazard = ph; sp.slamPuff = slam; sp.dropDistance = 2.05f;
        Save(press, Prefabs + "/SteamPress.prefab");

        // Crane hook on a chain, swinging from an overhead rail (SwingingAxe does the pendulum)
        var hook = new GameObject("CraneHook");
        var swing = hook.AddComponent<SwingingAxe>(); swing.axis = Vector3.forward; swing.maxAngle = 38f; swing.swingsPerSecond = 0.3f;
        Box("Trolley", hook.transform, new Vector3(0, 0.15f, 0), new Vector3(0.7f, 0.3f, 0.5f), ironMat);
        float len = SteampunkFactory.HookLength;
        for (int i = 0; i < 12; i++) Box("Chain link", hook.transform, new Vector3(0, -0.2f - i * (len - 0.6f) / 12f, 0), new Vector3(i % 2 == 0 ? 0.14f : 0.05f, (len - 0.6f) / 12f + 0.05f, i % 2 == 0 ? 0.05f : 0.14f), ironMat);
        var block = new GameObject("Hook block"); block.transform.SetParent(hook.transform, false); block.transform.localPosition = new Vector3(0, -len, 0);
        Box("Block", block.transform, new Vector3(0, 0.25f, 0), new Vector3(0.6f, 0.5f, 0.45f), brassMat);
        Box("Hook", block.transform, new Vector3(0, -0.2f, 0), new Vector3(0.14f, 0.5f, 0.14f), ironMat);
        Box("Hook tip", block.transform, new Vector3(0.15f, -0.42f, 0), new Vector3(0.36f, 0.12f, 0.14f), ironMat);
        Box("Load", block.transform, new Vector3(0, -0.85f, 0), new Vector3(0.9f, 0.6f, 0.7f), woodMat);
        var hookCol = block.AddComponent<SphereCollider>(); hookCol.isTrigger = true; hookCol.center = new Vector3(0, -0.45f, 0); hookCol.radius = 0.62f;
        block.AddComponent<Hazard>();
        Save(hook, Prefabs + "/CraneHook.prefab");

        // Cargo that rides the belt and hurts on touch
        var cargoCrate = new GameObject("CargoCrate");
        Box("Crate", cargoCrate.transform, new Vector3(0, 0.38f, 0), new Vector3(0.85f, 0.75f, 0.85f), woodMat);
        Box("Strap", cargoCrate.transform, new Vector3(0, 0.38f, 0), new Vector3(0.88f, 0.12f, 0.88f), ironMat);
        CargoHazard(cargoCrate, new Vector3(0.9f, 0.8f, 0.9f));
        Save(cargoCrate, Prefabs + "/CargoCrate.prefab");

        var cargoParts = new GameObject("CargoIronParts");
        MeshObject("Cog", cargoParts.transform, new Vector3(0, 0.45f, 0), GearMesh(10, 0.38f, 0.48f, 0.22f), ironMat);
        Cyl("Boiler plate", cargoParts.transform, new Vector3(0.1f, 0.12f, 0), new Vector3(0.9f, 0.1f, 0.9f), copperMat);
        CargoHazard(cargoParts, new Vector3(0.95f, 0.95f, 0.8f));
        Save(cargoParts, Prefabs + "/CargoIronParts.prefab");
    }

    static void CargoHazard(GameObject go, Vector3 size)
    {
        var c = go.AddComponent<BoxCollider>(); c.isTrigger = true; c.center = new Vector3(0, size.y * 0.5f, 0); c.size = size;
        go.AddComponent<Hazard>();
    }

    static void MakeItems()
    {
        // Spare iron chain: a coil of links on the floor, glinting
        var chain = new GameObject("SpareChain");
        var trigger = chain.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.center = new Vector3(0, 0.5f, 0); trigger.radius = 0.8f;
        var visual = new GameObject("Visual"); visual.transform.SetParent(chain.transform, false); visual.transform.localPosition = new Vector3(0, 0.45f, 0);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            var l = Box("Link", visual.transform, new Vector3(Mathf.Cos(a) * 0.38f, Mathf.Sin(a * 2f) * 0.05f, Mathf.Sin(a) * 0.38f), new Vector3(0.18f, 0.08f, 0.1f), ironMat);
            l.transform.localRotation = Quaternion.Euler(i % 2 == 0 ? 0 : 90, -a * Mathf.Rad2Deg, 0);
        }
        Light("Glint", chain.transform, new Vector3(0, 1.0f, 0), Hex("FFD27A"), 1.2f, 2.5f);
        var item = chain.AddComponent<SteampunkItem>(); item.item = SteampunkLevel.Item.Chain; item.visual = visual.transform;
        Save(chain, Prefabs + "/SpareChain.prefab");

        // Oil can: brass body, long spout, handle
        var oil = new GameObject("OilCan");
        var ot = oil.AddComponent<SphereCollider>(); ot.isTrigger = true; ot.center = new Vector3(0, 0.5f, 0); ot.radius = 0.8f;
        var ov = new GameObject("Visual"); ov.transform.SetParent(oil.transform, false); ov.transform.localPosition = new Vector3(0, 0.35f, 0);
        Cyl("Body", ov.transform, new Vector3(0, 0, 0), new Vector3(0.45f, 0.22f, 0.45f), brassMat);
        Sphere("Top", ov.transform, new Vector3(0, 0.22f, 0), new Vector3(0.45f, 0.2f, 0.45f), brassMat);
        Box("Spout", ov.transform, new Vector3(0.28f, 0.42f, 0), new Vector3(0.06f, 0.5f, 0.06f), copperMat).transform.localRotation = Quaternion.Euler(0, 0, -40);
        Box("Handle", ov.transform, new Vector3(-0.28f, 0.1f, 0), new Vector3(0.06f, 0.35f, 0.18f), ironMat);
        Light("Glint", oil.transform, new Vector3(0, 1.0f, 0), Hex("FFD27A"), 1.2f, 2.5f);
        var oilItem = oil.AddComponent<SteampunkItem>(); oilItem.item = SteampunkLevel.Item.Oil; oilItem.visual = ov.transform;
        Save(oil, Prefabs + "/OilCan.prefab");

        // Pocket watch: optional, +15 seconds through the shared PickupItem
        var watch = new GameObject("PocketWatch");
        var wt = watch.AddComponent<SphereCollider>(); wt.isTrigger = true; wt.center = new Vector3(0, 0.5f, 0); wt.radius = 0.7f;
        var pickup = watch.AddComponent<PickupItem>(); pickup.timeValue = 15f;
        var wv = new GameObject("Visual"); wv.transform.SetParent(watch.transform, false); wv.transform.localPosition = new Vector3(0, 0.55f, 0);
        Cyl("Case", wv.transform, Vector3.zero, new Vector3(0.5f, 0.06f, 0.5f), brassMat, false, new Vector3(90, 0, 0));
        Cyl("Face", wv.transform, new Vector3(0, 0, -0.065f), new Vector3(0.4f, 0.01f, 0.4f), gaugeMat, false, new Vector3(-90, 0, 0));
        Cyl("Crown", wv.transform, new Vector3(0, 0.3f, 0), new Vector3(0.1f, 0.06f, 0.1f), brassMat);
        var ws = wv.AddComponent<GearSpinner>(); ws.axis = Vector3.up; ws.degreesPerSecond = 90f;
        Light("Glint", watch.transform, new Vector3(0, 1.0f, -0.3f), Hex("FFE09A"), 1.4f, 2.5f);
        Save(watch, Prefabs + "/PocketWatch.prefab");
    }

    static void MakeConveyor()
    {
        var belt = new GameObject("ConveyorBelt");
        float length = SteampunkFactory.BeltLength;
        var deck = Box("Deck", belt.transform, new Vector3(0, 0.25f, 0), new Vector3(2.4f, 0.5f, length), ironMat, true);
        var surface = Box("Belt", belt.transform, new Vector3(0, 0.505f, 0), new Vector3(2.1f, 0.02f, length), beltMat);
        foreach (float x in new[] { -1.2f, 1.2f }) Box("Side rail", belt.transform, new Vector3(x, 0.55f, 0), new Vector3(0.12f, 0.12f, length), brassMat);
        var rollers = new System.Collections.Generic.List<GearSpinner>();
        for (float z = -length * 0.5f + 0.6f; z < length * 0.5f; z += 2.4f)
        {
            var r = Cyl("Roller", belt.transform, new Vector3(0, 0.3f, z), new Vector3(0.36f, 1.25f, 0.36f), ironMat, false, new Vector3(0, 0, 90));
            var s = r.AddComponent<GearSpinner>(); s.axis = Vector3.up; rollers.Add(s);
        }
        // Hatches the cargo comes out of and drops into
        Box("Feed hatch", belt.transform, new Vector3(0, 1.3f, length * 0.5f - 0.3f), new Vector3(2.8f, 2.6f, 0.6f), ironMat, true);
        Box("Hatch mouth", belt.transform, new Vector3(0, 0.95f, length * 0.5f - 0.62f), new Vector3(2.0f, 0.9f, 0.04f), soot);
        Box("Chute", belt.transform, new Vector3(0, 0.6f, -length * 0.5f + 0.3f), new Vector3(2.8f, 1.2f, 0.6f), copperMat, true);
        var zone = belt.AddComponent<BoxCollider>(); zone.isTrigger = true; zone.center = new Vector3(0, 0.85f, 0); zone.size = new Vector3(2.4f, 0.7f, length - 1.2f);
        var cb = belt.AddComponent<ConveyorBelt>();
        cb.start = new Vector3(0, 0.51f, length * 0.5f - 0.7f); cb.end = new Vector3(0, 0.51f, -length * 0.5f + 0.7f);
        cb.cargoPrefabs = new[] { Load(Prefabs + "/CargoCrate.prefab"), Load(Prefabs + "/CargoIronParts.prefab") };
        cb.beltSurface = surface.GetComponent<Renderer>(); cb.rollers = rollers.ToArray();
        Save(belt, Prefabs + "/ConveyorBelt.prefab");
    }

    static void MakeLift()
    {
        // The frame stays put; the deck (with MechanicalLift) rides up and down inside it
        var lift = new GameObject("MechanicalLift");
        float h = SteampunkFactory.RoofY - 0.6f;
        foreach (float x in new[] { -1.7f, 1.7f }) foreach (float z in new[] { -1.7f, 1.7f })
            Box("Guide post", lift.transform, new Vector3(x, h * 0.5f, z), new Vector3(0.22f, h, 0.22f), ironMat, true);
        Box("Head beam", lift.transform, new Vector3(0, h, 0), new Vector3(3.8f, 0.4f, 3.8f), ironMat);
        var headWheel = MeshObject("Head pulley", lift.transform, new Vector3(0, h + 0.7f, -1.2f), GearMesh(14, 0.8f, 0.98f, 0.25f), brassMat);
        var headSpin = headWheel.AddComponent<GearSpinner>(); headSpin.degreesPerSecond = 120f; headSpin.running = false;
        var lamp = Light("Status lamp", lift.transform, new Vector3(-1.95f, 2.3f, -1.95f), Hex("E8331E"), 1.6f, 3.5f);
        Sphere("Status bulb", lift.transform, new Vector3(-1.95f, 2.3f, -1.75f), Vector3.one * 0.22f, redMat);

        var deck = new GameObject("Deck"); deck.transform.SetParent(lift.transform, false); deck.transform.localPosition = new Vector3(0, -0.1f, 0);
        var body = deck.AddComponent<Rigidbody>(); body.isKinematic = true;
        var solid = deck.AddComponent<BoxCollider>(); solid.size = new Vector3(3.1f, 0.3f, 3.1f);
        var range = deck.AddComponent<BoxCollider>(); range.isTrigger = true; range.center = new Vector3(0, 1.1f, 0); range.size = new Vector3(3.3f, 2f, 3.3f);
        Box("Floor", deck.transform, Vector3.zero, new Vector3(3.1f, 0.3f, 3.1f), floorMat);
        Box("Trim", deck.transform, new Vector3(0, 0.02f, 0), new Vector3(3.2f, 0.26f, 3.2f), brassMat);
        foreach (float x in new[] { -1.45f, 1.45f }) Box("Cage rail", deck.transform, new Vector3(x, 1.1f, 0), new Vector3(0.08f, 0.08f, 3f), brassMat);
        foreach (float x in new[] { -1.45f, 1.45f }) foreach (float z in new[] { -1.45f, 1.45f }) Box("Cage post", deck.transform, new Vector3(x, 0.6f, z), new Vector3(0.08f, 1.1f, 0.08f), brassMat);
        var drum = Cyl("Winding drum", deck.transform, new Vector3(1.2f, 0.45f, 1.2f), new Vector3(0.35f, 0.3f, 0.35f), copperMat, false, new Vector3(0, 0, 90));
        var drumSpin = drum.AddComponent<GearSpinner>(); drumSpin.axis = Vector3.up; drumSpin.degreesPerSecond = 200f; drumSpin.running = false;
        var puff = Emitter("Start steam", deck.transform, new Vector3(0, 0.3f, 0), 0f, 2.5f, 0.4f, 1.2f); puff.spread = 0.9f; puff.burstCount = 18;
        var ml = deck.AddComponent<MechanicalLift>();
        ml.travelHeight = SteampunkFactory.FirstFloorY; ml.statusLamp = lamp; ml.startPuff = puff; ml.rideWheels = new[] { headSpin, drumSpin };
        SetField(ml, "prompt", "Lift");
        Save(lift, Prefabs + "/MechanicalLift.prefab");
    }

    static void MakeNpcs()
    {
        Npc("WarehouseWorker", 1, new Color(0.86f, 0.78f, 0.66f), "Talk to the warehouse worker",
            "WAREHOUSE WORKER: \"Spare chain? Back shelves, past the crates. Mind your fingers.\"",
            "WAREHOUSE WORKER: \"Forty years of boxes. Never once fell through the ceiling, mind.\"");
        Npc("Mechanic", 2, new Color(0.8f, 0.72f, 0.62f), "Talk to the mechanic",
            "MECHANIC: \"That middle gear's seized solid. Oil can's by my bench, take it.\"",
            "MECHANIC: \"Chain first, then oil. A gear tower's only as good as its slowest cog.\"");
        Npc("FactoryWorker", 3, new Color(0.78f, 0.8f, 0.86f), "Talk to the factory worker",
            "FACTORY WORKER: \"The lift runs off the gear tower. No gears, no lift.\"",
            "FACTORY WORKER: \"Cross the belt when there's a gap. And keep clear of the press.\"");
        Npc("SeniorEngineer", 1, new Color(0.95f, 0.86f, 0.7f), "Talk to the senior engineer",
            "SENIOR ENGINEER: \"Gear one is chain-driven from the engine. Gear one drives gear two...\"",
            "SENIOR ENGINEER: \"...and gear two's rod frees gear three. Three turning gears, one working lift.\"");
        Npc("Supervisor", 2, new Color(0.72f, 0.66f, 0.8f), "Talk to the supervisor",
            "SUPERVISOR: \"Keep moving! ...Odd. Everything stops whenever you do.\"",
            "SUPERVISOR: \"Clockwork Guard's on patrol past the catwalk. Jump on its head, it hates that.\"");
        Npc("MachineOperator", 3, new Color(0.9f, 0.8f, 0.7f), "Talk to the machine operator",
            "MACHINE OPERATOR: \"Lift's running again? Splendid. I've been up here since Tuesday.\"",
            "MACHINE OPERATOR: \"Watch the vents. They hiss before they blow.\"");
    }

    static void Npc(string name, int farmer, Color tint, string prompt, params string[] lines)
    {
        var go = new GameObject(name);
        var range = go.AddComponent<SphereCollider>(); range.isTrigger = true; range.center = new Vector3(0, 0.6f, 0); range.radius = 1.6f;
        var art = new GameObject("Sprite"); art.transform.SetParent(go.transform, false); art.transform.localPosition = new Vector3(0, 0.56f, 0);
        var renderer = art.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = spriteMat; renderer.color = tint; art.AddComponent<Billboard>();
        var frames = AssetDatabase.LoadAllAssetsAtPath($"Assets/Undead Survivor/Sprites/Farmer {farmer}.png").OfType<Sprite>().Where(s => s.name.StartsWith("Stand")).OrderBy(s => s.name).ToArray();
        if (frames.Length == 0) Debug.LogWarning($"[Steampunk] Undead Survivor 'Farmer {farmer}' sprites not found: {name} has no art. Import the pack, then rebuild.");
        else renderer.sprite = frames[0];
        var npc = go.AddComponent<CaveNpc>(); npc.sprite = renderer; npc.frames = frames; npc.framesPerSecond = 4f; npc.lines = lines; npc.talkPrompt = prompt;
        Save(go, Prefabs + "/" + name + ".prefab");
    }

    static void MakeGuard()
    {
        // Built from the Medieval skeleton's body (rigidbody, hitbox, art), with the guard's own brain and blades
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/prefabs/Medieval/Skeleton.prefab"); root.name = "ClockworkGuard";
        var skeleton = root.GetComponent<SkeletonEnemy>(); if (skeleton != null) UnityEngine.Object.DestroyImmediate(skeleton);
        var guard = root.AddComponent<ClockworkGuard>();
        Transform art = root.transform.Find("Sprite");
        if (art != null)
        {
            guard.sprite = art.GetComponent<SpriteRenderer>(); guard.animator = art.GetComponent<Animator>();
            if (guard.sprite != null) guard.sprite.color = new Color(1f, 0.76f, 0.42f); // placeholder: the skeleton art tinted brass
            art.localScale = Vector3.one * 1.2f;
        }
        var blades = new GameObject("Blades"); blades.transform.SetParent(root.transform, false); blades.transform.localPosition = new Vector3(0, 0.6f, 0);
        Box("Blade", blades.transform, Vector3.zero, new Vector3(2.7f, 0.06f, 0.2f), brassMat);
        Box("Blade", blades.transform, Vector3.zero, new Vector3(0.2f, 0.06f, 2.7f), brassMat);
        Cyl("Hub", blades.transform, Vector3.zero, new Vector3(0.4f, 0.08f, 0.4f), ironMat);
        guard.blades = blades.transform;
        var sparks = Emitter("Death sparks", root.transform, new Vector3(0, 0.6f, 0), 0f, 3.5f, 0.2f, 0.2f);
        sparks.sprite = sparkSprite; sparks.spread = 1f; sparks.gravity = 6f; sparks.burstCount = 20; sparks.startColor = Hex("FFD27A"); sparks.endColor = new Color(1f, 0.4f, 0.1f, 0f);
        guard.deathSparks = sparks;
        PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/ClockworkGuard.prefab"); PrefabUtility.UnloadPrefabContents(root);
    }

    static void MakeMachine()
    {
        string path = Prefabs + "/SteampunkTimeMachine.prefab";
        // Copied once and then edited in place, so its GUID (and every reference to it) stays stable across rebuilds
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset("Assets/prefabs/Medieval/TimeMachine.prefab", path);
        GameObject root = PrefabUtility.LoadPrefabContents(path); root.name = "SteampunkTimeMachine"; var machine = root.GetComponent<TimeMachine>();
        machine.loadNextEra = false; // SteampunkLevel turns this on when the Future scene follows in Build Settings
        machine.inspectPrompt = "Inspect the time machine"; machine.repairPrompt = "Repair the Chrono Regulator";
        machine.brokenLines = new[] { "SYSTEM: \"Chrono Regulator offline.\"" };
        PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
    }

    static void AddBuildScene()
    {
        // Play order: ... Medieval, Steampunk, then everything else as it was
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        int medieval = scenes.FindIndex(s => s.path == "Assets/Scenes/Medieval.unity");
        scenes.Insert(medieval >= 0 ? medieval + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- helpers

    static SteamEmitter Emitter(string name, Transform parent, Vector3 pos, float rate, float speed, float startSize, float endSize)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        var e = go.AddComponent<SteamEmitter>(); e.sprite = puffSprite; e.material = puffMat; e.rate = rate; e.speed = speed; e.startSize = startSize; e.endSize = endSize;
        return e;
    }

    static Light Light(string name, Transform parent, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }

    static void SetField(UnityEngine.Object target, string field, object value)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[Steampunk] No field {field} on {target.GetType().Name}"); return; }
        if (value is UnityEngine.Object o) p.objectReferenceValue = o; else if (value is string s) p.stringValue = s; else if (value is float f) p.floatValue = f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool solid = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = size;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat; else UnityEngine.Object.DestroyImmediate(go.GetComponent<Renderer>());
        if (mat == null) UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshFilter>());
        if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    internal static GameObject Cyl(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool solid = false, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale; go.transform.localRotation = Quaternion.Euler(euler);
        var col = go.GetComponent<Collider>();
        if (solid) { UnityEngine.Object.DestroyImmediate(col); var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(1f, 2f, 1f); }
        else UnityEngine.Object.DestroyImmediate(col);
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat; else { UnityEngine.Object.DestroyImmediate(go.GetComponent<Renderer>()); UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshFilter>()); }
        return go;
    }

    internal static GameObject Sphere(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    internal static GameObject MeshObject(string name, Transform parent, Vector3 pos, Mesh mesh, Material mat)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static void Folder(string parent, string name) { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }
    static GameObject Load(string path) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (p == null) throw new InvalidOperationException(path); return p; }
    static void Instantiate(string path) => PrefabUtility.InstantiatePrefab(Load(path));
    internal static GameObject Place(string prefab, Transform parent, Vector3 pos, float yaw = 0f)
    {
        var go = PrefabUtility.InstantiatePrefab(Load(Prefabs + "/" + prefab + ".prefab")) as GameObject;
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return go;
    }
    internal static Transform Group(string name, Transform parent) { var g = new GameObject(name).transform; g.SetParent(parent, false); return g; }
    static Transform Find(Transform parent, string name) { foreach (Transform child in parent) { if (child.name == name) return child; Transform result = Find(child, name); if (result != null) return result; } return null; }
    static void Save(GameObject root, string path) { PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root); }
}
