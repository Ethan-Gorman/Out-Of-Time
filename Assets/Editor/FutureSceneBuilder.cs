using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Layout numbers for the Future era (Chronos Lab), shared by the builder.
//
//   x: -26 (west wall) .. +26 (east wall)      z: -14 (south, toward the camera) .. +14 (north wall). Floor top y = 0.
//   Rooms, west to east: Arrival Bay (x -26..-12: spawn, time machine), Archive (x -12..4, z -4..14: racks, keycard,
//   Sentry Bot 1), Core Hall (x 4..26, z -4..14: Chrono Core, rift field, Stabiliser A, drone), Calibration Bay
//   (x 20..26, z 5..14: two laser fences, Stabiliser B), Transit Concourse (x -12..26, z -14..-4: pistons at x 8,
//   Sentry Bot 2, the Core Barrier at x 20..23 back into the Core Hall).
// Full design: .claude/future-level/design.md
public static class FutureLab
{
    public const float WallHeight = 6f;
    public const float WallThickness = 0.5f;
    public const float MinX = -26f, MaxX = 26f, MinZ = -14f, MaxZ = 14f;
    public static readonly Vector3 ArrivalPoint = new Vector3(-19f, 0.1f, -2f);
    public static readonly Vector3 MachinePoint = new Vector3(-19f, 0f, 6f);
}

// Builds the Future Era: placeholder materials, the gameplay prefabs, the art set, Assets/prefabs/Future/FutureLevel.prefab
// and Assets/Scenes/Future.unity, and puts the scene in Build Settings right after Steampunk.
// Run via Tools > Out Of Time > Build Future Era. Safe to run again: it rebuilds its prefabs and the scene, but never
// overwrites a material that already exists or the art set, so the team's art choices survive every rebuild.
//
// Props are data: every decorative or solid prop is a row in Slots below (a PropSlot in the level). Its art comes from
// FutureArtSet.asset (a prefab or sprite per role or slot id) or a placeholder, and its collider from its footprint,
// so changing the art never changes the route. Tools > Out Of Time > Future > Apply Art Set re-dresses the level
// prefab without a full rebuild.
public static class FutureSceneBuilder
{
    const string Prefabs = "Assets/prefabs/Future";
    const string Mats = "Assets/Materials/Future";
    const string Textures = "Assets/Materials/Future/Textures";
    const string ScenePath = "Assets/Scenes/Future.unity";
    const string ArtSetPath = Prefabs + "/FutureArtSet.asset";
    const string LevelPath = Prefabs + "/FutureLevel.prefab";
    const float H = FutureLab.WallHeight;

    static FutureArtSet art;
    static Material spriteMat, puffMat;
    static Sprite puffSprite, sparkSprite;
    static readonly List<(string id, Transform piece, Vector3 min, Vector3 max)> walls = new List<(string, Transform, Vector3, Vector3)>();

    static Material M(FutureSurface s) => art.Get(s);

    [MenuItem("Tools/Out Of Time/Build Future Era")]
    public static void Build()
    {
        // NewScene throws in Play mode, halfway through the build
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Leave Play mode before building Future."); return; }
        Scene original = SceneManager.GetActiveScene();
        if (original.isDirty) { Debug.LogError("Save or close the dirty scene before building Future."); return; }
        Folders();
        MakeMaterials(false);
        MakeArtSet();
        spriteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLit.mat");
        puffMat = FindMaterial("Sprite-Unlit-Default") ?? spriteMat;
        puffSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Steampunk/SteamPuff.png");
        sparkSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Steampunk/Spark.png");
        if (puffSprite == null || sparkSprite == null) Debug.LogWarning("[Future] Steampunk SteamPuff/Spark sprites missing: run Build Steampunk Era once for the sparks and puffs.");

        Scene staging = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(staging);
        try
        {
            MakeEssentials();
            MakeCoins(); MakeHazards(); MakeGameplay(); MakeNpcs(); MakeEnemies(); MakeMachine();
            MakeLevel();
        }
        finally
        {
            EditorSceneManager.CloseScene(staging, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PrefabUtility.InstantiatePrefab(Load(Prefabs + "/FutureEssentials.prefab"));
        PrefabUtility.InstantiatePrefab(Load(LevelPath));
        // Dim cool lab ambient: the cyan lamps do most of the work
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.30f, 0.34f, 0.42f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddBuildScene();
        Debug.Log("Built Future prefabs and " + ScenePath + ".");
    }

    // Re-dresses every prop slot in FutureLevel.prefab from the art set (no scene rebuild, no clean scene needed)
    [MenuItem("Tools/Out Of Time/Future/Apply Art Set")]
    public static void ApplyArtSet()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LevelPath) == null) { Debug.LogError("[Future] No " + LevelPath + " yet: run Build Future Era first."); return; }
        Folders();
        MakeMaterials(false);
        MakeArtSet();
        GameObject root = PrefabUtility.LoadPrefabContents(LevelPath);
        try
        {
            int count = 0;
            foreach (var slot in root.GetComponentsInChildren<PropSlot>(true)) { slot.Dress(art); count++; }
            PrefabUtility.SaveAsPrefabAsset(root, LevelPath);
            Debug.Log($"[Future] Re-dressed {count} prop slots in {LevelPath} from {ArtSetPath}.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Every slot as CSV (logged and copied to the clipboard), for the art team
    [MenuItem("Tools/Out Of Time/Future/List Prop Slots")]
    public static void ListSlots()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LevelPath) == null) { Debug.LogError("[Future] No " + LevelPath + " yet: run Build Future Era first."); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(LevelPath);
        var csv = new StringBuilder("id,role,customRole,width,height,depth,x,y,z,yaw,blocks,tall,mountTo,label\n");
        foreach (var s in root.GetComponentsInChildren<PropSlot>(true))
        {
            Vector3 p = s.transform.position, f = s.footprint;
            csv.AppendLine(string.Join(",", s.id, s.role, s.customRole, f.x, f.y, f.z, p.x.ToString("0.##"), p.y.ToString("0.##"), p.z.ToString("0.##"),
                // Back to the slot table's yaw (90 = faces east), the opposite sign to Unity's
                Mathf.DeltaAngle(0f, -s.transform.eulerAngles.y).ToString("0"), s.blocks, s.tallCollider, s.mountTo, "\"" + s.label + "\""));
        }
        PrefabUtility.UnloadPrefabContents(root);
        EditorGUIUtility.systemCopyBuffer = csv.ToString();
        Debug.Log("[Future] Prop slots (also copied to the clipboard):\n" + csv);
    }

    [MenuItem("Tools/Out Of Time/Future/Reset Placeholder Materials")]
    public static void ResetMaterials()
    {
        if (!EditorUtility.DisplayDialog("Reset Future materials",
            "Overwrite every Assets/Materials/Future/Future_*.mat with the generated placeholder look? Edits to those materials are lost.", "Reset", "Cancel"))
            return;
        Folders();
        MakeMaterials(true);
        AssetDatabase.SaveAssets();
        Debug.Log("[Future] Placeholder materials reset.");
    }

    static void Folders()
    {
        Folder("Assets/prefabs", "Future"); Folder("Assets/Materials", "Future"); Folder(Mats, "Textures");
    }

    // ---------------------------------------------------------------- materials and the art set

    // One material per surface role, made only if missing (or when reset), so hand edits survive rebuilds
    static Dictionary<FutureSurface, Material> MakeMaterials(bool overwrite)
    {
        var made = new Dictionary<FutureSurface, Material>();
        foreach (FutureSurface role in Enum.GetValues(typeof(FutureSurface)))
        {
            string path = $"{Mats}/Future_{role}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && !overwrite) { made[role] = existing; continue; }
            Placeholder(role, out var pixel, out string baseHex, out float emission, out bool worldUV);
            var tex = Texture(role + "32", 32, pixel);
            var m = existing;
            if (m == null) { m = new Material(Shader.Find("Out Of Time/Lit Cutout") ?? Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex); m.color = Color.white;
            if (worldUV) { m.SetFloat("_OOT_WorldUV", 1f); m.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f)); }
            if (emission > 0f)
            {
                // RealtimeEmissive: with the default "emissive is black" flag, URP's material check strips _EMISSION on save
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Hex(baseHex) * emission); m.SetTexture("_EmissionMap", tex);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(m);
            made[role] = m;
        }
        return made;
    }

    // The art set: created once, then only topped up (missing surface roles get the placeholder material)
    static void MakeArtSet()
    {
        art = AssetDatabase.LoadAssetAtPath<FutureArtSet>(ArtSetPath);
        if (art == null) { art = ScriptableObject.CreateInstance<FutureArtSet>(); AssetDatabase.CreateAsset(art, ArtSetPath); }
        var placeholders = MakeMaterials(false);
        var surfaces = (art.surfaces ?? new FutureArtSet.Surface[0]).Where(s => s != null).ToList();
        foreach (FutureSurface role in Enum.GetValues(typeof(FutureSurface)))
        {
            var s = surfaces.FirstOrDefault(x => x.role == role);
            if (s == null) surfaces.Add(s = new FutureArtSet.Surface { role = role });
            if (s.material == null) s.material = placeholders[role];
        }
        art.surfaces = surfaces.OrderBy(s => (int)s.role).ToArray();
        if (art.props == null) art.props = new FutureArtSet.Prop[0];
        if (art.spriteMaterial == null) art.spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLit.mat");
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
    }

    // The generated 32px look of each surface role (see the design doc's material table)
    static void Placeholder(FutureSurface role, out Func<int, int, Color> pixel, out string baseHex, out float emission, out bool worldUV)
    {
        emission = 0f; worldUV = false;
        switch (role)
        {
            case FutureSurface.Floor:
                baseHex = "2A3140"; worldUV = true;
                pixel = (x, y) => { int px = x % 16, py = y % 16; if (px == 0 || py == 0) return Hex("1B202B"); if (px == 1 || py == 1) return Hex("3C4558"); return ((x * 13 + y * 7) % 23) == 0 ? Hex("323A4B") : Hex("2A3140"); };
                break;
            case FutureSurface.FloorAccent:
                baseHex = "37C8D8"; emission = 0.6f;
                pixel = (x, y) => (y % 16) >= 6 && (y % 16) <= 9 ? Hex("37C8D8") : Hex("1F2633");
                break;
            case FutureSurface.Wall:
                baseHex = "3A4256"; worldUV = true;
                pixel = (x, y) => { if (x % 16 == 0 || y % 16 == 0) return Hex("262C3A"); if (y % 32 >= 20 && y % 32 <= 22 && x % 4 < 2) return Hex("262C3A"); return Hex("3A4256"); };
                break;
            case FutureSurface.WallPanel:
                baseHex = "4A5468";
                pixel = (x, y) => x % 16 == 8 ? Hex("4FE3F0") : y % 16 == 0 ? Hex("3A4256") : Hex("4A5468");
                break;
            case FutureSurface.Trim:
                baseHex = "C9D2DE";
                pixel = (x, y) => y % 8 == 0 ? Hex("9AA4B2") : Hex("C9D2DE");
                break;
            case FutureSurface.Glass:
                baseHex = "7FB8D0";
                pixel = (x, y) => (x - y + 64) % 16 == 3 ? Hex("E4F4FA") : Hex("7FB8D0");
                break;
            case FutureSurface.Metal:
                baseHex = "5A6475";
                pixel = (x, y) => y % 4 == 0 ? Hex("4E5868") : ((x * 7 + y * 3) % 11) == 0 ? Hex("667183") : Hex("5A6475");
                break;
            case FutureSurface.MetalDark:
                baseHex = "252A35";
                pixel = (x, y) => y % 16 == 0 ? Hex("1B1F28") : Hex("252A35");
                break;
            case FutureSurface.Glow:
                baseHex = "4FE3F0"; emission = 2f;
                pixel = (x, y) => (x + y) % 9 == 0 ? Hex("BFF8FF") : Hex("4FE3F0");
                break;
            case FutureSurface.GlowWarm:
                baseHex = "FFB54A"; emission = 1.6f;
                pixel = (x, y) => (x + y) % 9 == 0 ? Hex("FFE3B0") : Hex("FFB54A");
                break;
            case FutureSurface.Danger:
                baseHex = "FF3B4A"; emission = 2.6f;
                pixel = (x, y) => ((x + y) / 4) % 2 == 0 ? Hex("FF3B4A") : Hex("FF6A74");
                break;
            case FutureSurface.Screen:
                baseHex = "3D7BFF"; emission = 1.4f;
                pixel = (x, y) => y % 2 == 0 ? Hex("2C5BC0") : ((x * 5 + y) % 13 == 0 ? Hex("9CC0FF") : Hex("3D7BFF"));
                break;
            case FutureSurface.Rift:
                baseHex = "9A4DFF"; emission = 2.2f;
                pixel = (x, y) =>
                {
                    float dx = x - 15.5f, dy = y - 15.5f, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                    if (r < 3f) return Hex("E0C8FF");
                    return Mathf.Sin(a * 3f + r * 0.5f) > 0f ? Hex("9A4DFF") : Hex("5A2A9A");
                };
                break;
            case FutureSurface.Hologram:
                baseHex = "6FF2FF"; emission = 1.8f;
                pixel = (x, y) => (x + y) % 2 == 0 ? Hex("6FF2FF") : Hex("3FB8C8");
                break;
            case FutureSurface.Plant:
                baseHex = "4FB06A";
                pixel = (x, y) => ((x * 5 + y * 3) % 7) < 2 ? Hex("2F7A45") : ((x * 3 + y) % 11 == 0 ? Hex("8AD69A") : Hex("4FB06A"));
                break;
            case FutureSurface.CoinBronze: baseHex = "CD7F32"; emission = 0.6f; pixel = CoinPixel("CD7F32", "8A5220"); break;
            case FutureSurface.CoinSilver: baseHex = "C8D2E0"; emission = 0.6f; pixel = CoinPixel("C8D2E0", "8A94A6"); break;
            case FutureSurface.CoinGold: baseHex = "FFD24A"; emission = 1f; pixel = CoinPixel("FFD24A", "B8902A"); break;
            default:
                baseHex = "0B0D12";
                pixel = (x, y) => Hex("0B0D12");
                break;
        }
    }

    // A coin face with a darker rim ring
    static Func<int, int, Color> CoinPixel(string face, string rim) => (x, y) =>
    {
        float dx = x - 15.5f, dy = y - 15.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
        return r > 13f || (r > 9f && r < 10f) ? Hex(rim) : Hex(face);
    };

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

    static Material FindMaterial(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null && m.name == name) return m;
        }
        return null;
    }

    static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out Color color); return color; }

    // ---------------------------------------------------------------- shared prefabs

    static void MakeEssentials()
    {
        // The traveller: a Futurist look when its sheet exists, else the Gentleman arriving from Steampunk
        var futurist = new PlayerLookBuilder.Look { name = "Futurist", sheet = "Assets/Sprites/Future/Futurist/FuturistSheet.png", folder = "Assets/Sprites/Future/Futurist", facesLeft = false };
        bool hasFuturist = File.Exists(futurist.sheet);
        string steampunk = "Assets/prefabs/Steampunk/SteampunkEssentials.prefab";
        string source = !hasFuturist && AssetDatabase.LoadAssetAtPath<GameObject>(steampunk) != null ? steampunk : "Assets/prefabs/Medieval/MedievalEssentials.prefab";

        GameObject root = PrefabUtility.LoadPrefabContents(source); root.name = "FutureEssentials";
        try
        {
            var info = root.GetComponent<LevelInfo>(); if (info == null) throw new InvalidOperationException(source + " has no LevelInfo."); info.era = Era.Future;
            Transform player = Find(root.transform, "MedievalPlayer"); if (player != null) player.localPosition = FutureLab.ArrivalPoint;
            if (hasFuturist)
            {
                var look = PlayerLookBuilder.Build(futurist);
                if (player != null && look != null) PlayerLookBuilder.Apply(player, look);
            }
            else if (source != steampunk) Debug.LogWarning("[Future] No Futurist sheet or SteampunkEssentials: the player keeps the Medieval look.");
            else Debug.Log("[Future] No " + futurist.sheet + " yet: the player keeps the Steampunk gentleman look.");

            // Cool, dim sun; the lab lamps do the rest
            foreach (var sun in root.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Directional)) { sun.color = new Color(0.72f, 0.82f, 1f); sun.intensity = 0.5f; sun.shadows = LightShadows.Hard; }
            foreach (var oob in root.GetComponentsInChildren<OutOfBounds>(true)) { oob.material = M(FutureSurface.Void); oob.background = new Color(0.04f, 0.05f, 0.07f, 1f); }
            PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/FutureEssentials.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Era-neutral Time Coins in three tiers; drop them into any era
    static void MakeCoins()
    {
        Coin("TimeCoinBronze", 2f, 0.45f, FutureSurface.CoinBronze, Hex("CD7F32"));
        Coin("TimeCoinSilver", 5f, 0.55f, FutureSurface.CoinSilver, Hex("C8D2E0"));
        Coin("TimeCoinGold", 10f, 0.70f, FutureSurface.CoinGold, Hex("FFD24A"));
    }

    static void Coin(string name, float seconds, float diameter, FutureSurface face, Color tint)
    {
        var go = new GameObject(name);
        var trigger = go.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.center = new Vector3(0, 0.6f, 0); trigger.radius = 0.55f;
        var coin = go.AddComponent<TimeCoin>(); coin.seconds = seconds;
        var visual = new GameObject("Visual").transform; visual.SetParent(go.transform, false); visual.localPosition = new Vector3(0, 0.6f, 0);
        // A clock coin: the face toward the camera, a rim, two little hands
        FutureProps.Cyl("Face", visual, Vector3.zero, diameter, 0.08f, M(face)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        FutureProps.Cyl("Rim", visual, new Vector3(0, 0, 0.01f), diameter * 1.1f, 0.05f, M(FutureSurface.Trim)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        FutureProps.Box("Hour hand", visual, new Vector3(0, diameter * 0.12f, -0.05f), new Vector3(0.04f, diameter * 0.28f, 0.02f), M(FutureSurface.MetalDark));
        FutureProps.Box("Minute hand", visual, new Vector3(diameter * 0.12f, 0, -0.05f), new Vector3(diameter * 0.3f, 0.035f, 0.02f), M(FutureSurface.MetalDark));
        coin.visual = visual;
        FutureProps.Light("Glint", go.transform, new Vector3(0, 1f, 0), tint, 1.2f, 2.5f);
        var burst = Emitter("Collect burst", go.transform, new Vector3(0, 0.6f, 0), sparkSprite, 2.5f, 0.15f, 0.05f);
        burst.burstCount = 16; burst.spread = 1f; burst.gravity = 4f; burst.startColor = tint; burst.endColor = new Color(tint.r, tint.g, tint.b, 0f);
        coin.collectBurst = burst;
        Save(go, Prefabs + "/" + name + ".prefab");
    }

    static void MakeHazards()
    {
        // Laser fence: two emitter posts, three beams, a trigger too tall to jump
        var fence = new GameObject("LaserFence");
        var lf = fence.AddComponent<LaserFence>();
        var lenses = new List<Renderer>();
        foreach (float x in new[] { -2.75f, 2.75f })
        {
            FutureProps.Box("Post", fence.transform, new Vector3(x, 0.8f, 0), new Vector3(0.3f, 1.6f, 0.3f), M(FutureSurface.Metal));
            lenses.Add(FutureProps.Box("Lens", fence.transform, new Vector3(x, 1.45f, -0.16f), new Vector3(0.2f, 0.2f, 0.04f), M(FutureSurface.Danger)).GetComponent<Renderer>());
        }
        var beams = new GameObject("Beams"); beams.transform.SetParent(fence.transform, false);
        foreach (float y in new[] { 0.35f, 0.8f, 1.25f }) FutureProps.Box("Beam", beams.transform, new Vector3(0, y, 0), new Vector3(5.5f, 0.05f, 0.05f), M(FutureSurface.Danger));
        var hz = new GameObject("Hazard"); hz.transform.SetParent(fence.transform, false);
        var hzBox = hz.AddComponent<BoxCollider>(); hzBox.isTrigger = true; hzBox.center = new Vector3(0, 1.3f, 0); hzBox.size = new Vector3(5.5f, 2.6f, 0.4f);
        lf.hazard = hz.AddComponent<Hazard>(); lf.beams = beams; lf.lenses = lenses.ToArray();
        lf.glow = FutureProps.Light("Laser glow", fence.transform, new Vector3(0, 1f, -0.3f), Hex("FF3B4A"), 0f, 3f);
        Save(fence, Prefabs + "/LaserFence.prefab");

        // Time rift: two counter-turning violet discs on the floor, a flickering light, a small time cost on touch
        var rift = new GameObject("TimeRift");
        var riftTrigger = rift.AddComponent<SphereCollider>(); riftTrigger.isTrigger = true; riftTrigger.center = new Vector3(0, 0.5f, 0); riftTrigger.radius = 0.9f;
        var riftHazard = rift.AddComponent<Hazard>(); riftHazard.timeCost = 4f;
        var disc = FutureProps.Cyl("Disc", rift.transform, new Vector3(0, 0.02f, 0), 1.8f, 0.02f, M(FutureSurface.Rift));
        var s1 = disc.AddComponent<GearSpinner>(); s1.axis = Vector3.up; s1.degreesPerSecond = 25f;
        var inner = FutureProps.Cyl("Inner disc", rift.transform, new Vector3(0, 0.04f, 0), 1.1f, 0.02f, M(FutureSurface.Rift));
        var s2 = inner.AddComponent<GearSpinner>(); s2.axis = Vector3.up; s2.degreesPerSecond = -40f;
        var riftLight = FutureProps.Light("Rift light", rift.transform, new Vector3(0, 0.6f, 0), new Color(0.6f, 0.3f, 1f), 1.6f, 3.5f);
        var flicker = riftLight.gameObject.AddComponent<TorchFlicker>(); flicker.flame = riftLight; flicker.flickerAmount = 0.3f; flicker.flickerSpeed = 5f;
        var close = Emitter("Close burst", rift.transform, new Vector3(0, 0.3f, 0), sparkSprite, 3f, 0.15f, 0.05f);
        close.burstCount = 24; close.spread = 1f; close.gravity = 2f; close.startColor = new Color(0.8f, 0.6f, 1f); close.endColor = new Color(0.6f, 0.3f, 1f, 0f);
        var tr = rift.AddComponent<TimeRift>(); tr.hazard = riftHazard; tr.closeBurst = close;
        Save(rift, Prefabs + "/TimeRift.prefab");

        // Piston gate: a press head in a checkpoint doorway (the wall is its frame)
        var piston = new GameObject("PistonGate");
        var head = new GameObject("Head"); head.transform.SetParent(piston.transform, false); head.transform.localPosition = new Vector3(0, 3f, 0);
        FutureProps.Box("Die", head.transform, Vector3.zero, new Vector3(1.4f, 0.5f, 2.2f), M(FutureSurface.Metal));
        FutureProps.Cyl("Ram", head.transform, new Vector3(0, 1.3f, 0), 0.3f, 2.4f, M(FutureSurface.Metal));
        FutureProps.Box("Danger stripe", head.transform, new Vector3(0, -0.12f, -1.11f), new Vector3(1.4f, 0.12f, 0.02f), M(FutureSurface.Danger));
        foreach (float x in new[] { -0.71f, 0.71f }) FutureProps.Box("Danger stripe", head.transform, new Vector3(x, -0.12f, 0), new Vector3(0.02f, 0.12f, 2.2f), M(FutureSurface.Danger));
        var headTrigger = head.AddComponent<BoxCollider>(); headTrigger.isTrigger = true; headTrigger.center = new Vector3(0, -0.35f, 0); headTrigger.size = new Vector3(1.4f, 0.9f, 2.2f);
        var pistonHazard = head.AddComponent<Hazard>();
        FutureProps.Box("Housing", piston.transform, new Vector3(0, 5.4f, 0), new Vector3(1.6f, 0.6f, 2.4f), M(FutureSurface.MetalDark));
        var slam = Emitter("Slam puff", piston.transform, new Vector3(0, 0.3f, 0), puffSprite, 2.5f, 0.4f, 1.1f);
        slam.spread = 0.9f; slam.burstCount = 14; slam.startColor = new Color(0.8f, 0.95f, 1f, 0.85f); slam.endColor = new Color(0.7f, 0.85f, 0.9f, 0f);
        var press = piston.AddComponent<SteamPress>(); press.head = head.transform; press.hazard = pistonHazard; press.slamPuff = slam; press.dropDistance = 2.5f;
        Save(piston, Prefabs + "/PistonGate.prefab");
    }

    static void MakeGameplay()
    {
        // Keycard: walk into it
        var card = new GameObject("Keycard");
        var ct = card.AddComponent<SphereCollider>(); ct.isTrigger = true; ct.center = new Vector3(0, 0.5f, 0); ct.radius = 0.8f;
        var cv = new GameObject("Visual").transform; cv.SetParent(card.transform, false); cv.localPosition = new Vector3(0, 0.6f, 0);
        FutureProps.Box("Card", cv, Vector3.zero, new Vector3(0.5f, 0.32f, 0.03f), M(FutureSurface.Trim));
        FutureProps.Box("Stripe", cv, new Vector3(0, 0.08f, -0.02f), new Vector3(0.5f, 0.06f, 0.01f), M(FutureSurface.Glow));
        FutureProps.Box("Chip", cv, new Vector3(-0.12f, -0.04f, -0.02f), new Vector3(0.1f, 0.08f, 0.01f), M(FutureSurface.GlowWarm));
        FutureProps.Light("Glint", card.transform, new Vector3(0, 1f, -0.3f), Hex("4FE3F0"), 1.4f, 2.5f);
        var item = card.AddComponent<FutureItem>(); item.item = FutureLevel.Item.Keycard; item.visual = cv;
        Save(card, Prefabs + "/Keycard.prefab");

        // Security door: keycard reader + sliding leaves, 2.55 wide (the gap in the Archive / Core Hall wall)
        var door = SlidingDoor("SecurityDoor", 2.55f, FutureSurface.MetalDark, out Gate doorGate, out Renderer lamp);
        var range = door.AddComponent<BoxCollider>(); range.isTrigger = true; range.center = new Vector3(0, 1.1f, 0); range.size = new Vector3(3.2f, 2.2f, 3.6f);
        var kd = door.AddComponent<KeycardDoor>(); kd.gate = doorGate; kd.statusLamp = lamp;
        Save(door, Prefabs + "/SecurityDoor.prefab");

        // Core Barrier: opens by itself when the core comes online (FutureLevel)
        var barrier = SlidingDoor("CoreBarrier", 3f, FutureSurface.Danger, out _, out _);
        Save(barrier, Prefabs + "/CoreBarrier.prefab");

        // Chrono Stabiliser: a pedestal with a ring that spins up once restarted
        var stab = new GameObject("ChronoStabiliser");
        var sr = stab.AddComponent<SphereCollider>(); sr.isTrigger = true; sr.center = new Vector3(0, 0.8f, 0); sr.radius = 1.6f;
        // Solid on a child with the art under it, so the OccluderFader cuts the whole thing
        var solid = new GameObject("Solid"); solid.transform.SetParent(stab.transform, false);
        var sb = solid.AddComponent<BoxCollider>(); sb.center = new Vector3(0, 0.9f, 0); sb.size = new Vector3(1.1f, 1.8f, 1.1f);
        FutureProps.Box("Base", solid.transform, new Vector3(0, 0.15f, 0), new Vector3(1.2f, 0.3f, 1.2f), M(FutureSurface.MetalDark));
        FutureProps.Cyl("Column", solid.transform, new Vector3(0, 0.9f, 0), 0.6f, 1.2f, M(FutureSurface.Metal));
        var ring = new GameObject("Ring"); ring.transform.SetParent(solid.transform, false); ring.transform.localPosition = new Vector3(0, 1.2f, 0);
        FutureProps.Cyl("Band", ring.transform, Vector3.zero, 1.0f, 0.08f, M(FutureSurface.Trim));
        foreach (float a in new[] { 0f, 90f }) FutureProps.Box("Spoke", ring.transform, Vector3.zero, new Vector3(1.1f, 0.1f, 0.12f), M(FutureSurface.MetalDark)).transform.localRotation = Quaternion.Euler(0, a, 0);
        var ringSpin = ring.AddComponent<GearSpinner>(); ringSpin.axis = Vector3.up; ringSpin.degreesPerSecond = 120f; ringSpin.running = false;
        var off = FutureProps.Box("Offline lamp", solid.transform, new Vector3(0, 1.62f, 0), new Vector3(0.35f, 0.35f, 0.35f), M(FutureSurface.GlowWarm));
        var on = FutureProps.Sphere("Online core", solid.transform, new Vector3(0, 1.65f, 0), Vector3.one * 0.42f, M(FutureSurface.Glow));
        var cs = stab.AddComponent<ChronoStabiliser>();
        cs.SetPrompt("Restart the stabiliser");
        cs.offVisual = off; cs.onVisual = on; cs.spinners = new[] { ringSpin };
        cs.glow = FutureProps.Light("Glow", stab.transform, new Vector3(0, 1.8f, -0.5f), Hex("4FE3F0"), 1.8f, 4f);
        var puff = Emitter("Start burst", stab.transform, new Vector3(0, 1.4f, 0), sparkSprite, 3f, 0.15f, 0.05f);
        puff.burstCount = 18; puff.spread = 1f; puff.gravity = 3f; puff.startColor = Hex("BFF8FF"); puff.endColor = new Color(0.3f, 0.9f, 1f, 0f);
        cs.startPuff = puff;
        Save(stab, Prefabs + "/ChronoStabiliser.prefab");
    }

    // A door in a wall gap: two leaves that slide apart along local X (each its own collider, art under it), and a
    // lintel up to the wall top. Built facing -Z; rotate it 90 for a north-south wall
    static GameObject SlidingDoor(string name, float width, FutureSurface stripe, out Gate gate, out Renderer lamp)
    {
        const float doorH = 4.2f;
        var root = new GameObject(name);
        var doorGo = new GameObject("Door"); doorGo.transform.SetParent(root.transform, false);
        gate = doorGo.AddComponent<Gate>(); gate.openOffset = Vector3.zero; gate.openTime = 0.9f;
        var parts = new List<Gate.Part>();
        foreach (int side in new[] { -1, 1 })
        {
            var leaf = new GameObject(side < 0 ? "Leaf L" : "Leaf R"); leaf.transform.SetParent(doorGo.transform, false);
            leaf.transform.localPosition = new Vector3(side * width * 0.25f, 0, 0);
            var c = leaf.AddComponent<BoxCollider>(); c.center = new Vector3(0, doorH * 0.5f, 0); c.size = new Vector3(width * 0.5f, doorH, 0.3f);
            FutureProps.Box("Panel", leaf.transform, new Vector3(0, doorH * 0.5f, 0), new Vector3(width * 0.5f - 0.02f, doorH, 0.3f), M(FutureSurface.MetalDark));
            FutureProps.Box("Stripe", leaf.transform, new Vector3(-side * (width * 0.25f - 0.08f), doorH * 0.5f, -0.16f), new Vector3(0.06f, doorH * 0.85f, 0.02f), M(stripe));
            FutureProps.Box("Stripe", leaf.transform, new Vector3(-side * (width * 0.25f - 0.08f), doorH * 0.5f, 0.16f), new Vector3(0.06f, doorH * 0.85f, 0.02f), M(stripe));
            parts.Add(new Gate.Part { part = leaf.transform, openOffset = new Vector3(side * width * 0.5f, 0, 0) });
        }
        gate.parts = parts.ToArray();
        var lintel = new GameObject("Lintel"); lintel.transform.SetParent(root.transform, false);
        var lc = lintel.AddComponent<BoxCollider>(); lc.center = new Vector3(0, (doorH + H) * 0.5f, 0); lc.size = new Vector3(width, H - doorH, FutureLab.WallThickness);
        FutureProps.Box("Lintel", lintel.transform, lc.center, lc.size, M(FutureSurface.Wall));
        FutureProps.Box("Frame top", lintel.transform, new Vector3(0, doorH + 0.05f, 0), new Vector3(width + 0.2f, 0.1f, 0.62f), M(FutureSurface.Trim));
        foreach (int side in new[] { -1, 1 })
            FutureProps.Box("Frame", root.transform, new Vector3(side * (width * 0.5f + 0.05f), doorH * 0.5f, 0), new Vector3(0.1f, doorH, 0.62f), M(FutureSurface.Trim));
        lamp = FutureProps.Box("Status lamp", root.transform, new Vector3(width * 0.5f + 0.3f, 1.5f, -0.3f), new Vector3(0.16f, 0.16f, 0.04f), M(FutureSurface.Glow)).GetComponent<Renderer>();
        return root;
    }

    static void MakeNpcs()
    {
        Npc("LabTechnician", 1, new Color(0.75f, 0.85f, 1f), 4f,
            "LAB TECHNICIAN: \"Another traveller? The Archive's through that doorway. Keycards are filed there.\"",
            "LAB TECHNICIAN: \"Have you noticed? Nothing moves unless you do.\"");
        Npc("CuriousKid", 3, new Color(1f, 0.9f, 0.8f), 4f,
            "CURIOUS KID: \"There are time coins in the concourse! Shiny ones!\"",
            "CURIOUS KID: \"Big ones are where it's scary. Past the pistons.\"");
        Npc("Archivist", 2, new Color(0.6f, 1f, 1f), 2f,
            "ARCHIVIST: \"Keycards? Far west shelf, top of the first aisle.\"",
            "ARCHIVIST: \"Mind the sentry on the north aisle. Jump on it; it hates that.\"");
        Npc("MaintenanceUnit", 1, new Color(0.7f, 0.75f, 0.8f), 4f,
            "MAINTENANCE UNIT: \"STABILISER A: BEYOND THE RIFT FIELD. STABILISER B: CALIBRATION BAY.\"",
            "MAINTENANCE UNIT: \"RIFTS COST TIME. DO NOT STAND IN THE PAST.\"");
        Npc("SecurityOfficer", 2, new Color(0.8f, 0.85f, 1f), 3f,
            "SECURITY OFFICER: \"The lasers cycle when you move. Watch them, then go.\"",
            "SECURITY OFFICER: \"That drone? Stomp it. I would, if I could still move.\"");
        // Frozen mid-step: no animation at all
        Npc("FrozenCommuter", 3, new Color(0.85f, 0.85f, 0.95f), 0f,
            "FROZEN COMMUTER: \"I've been late for the 8:15 for three hundred years.\"",
            "FROZEN COMMUTER: \"If that barrier ever opens, it's a straight run back to the bay.\"");
    }

    // Placeholder art: the Undead Survivor farmer frames, tinted. Swap CaveNpc.sprite / frames freely
    static void Npc(string name, int farmer, Color tint, float fps, params string[] lines)
    {
        var go = new GameObject(name);
        var range = go.AddComponent<SphereCollider>(); range.isTrigger = true; range.center = new Vector3(0, 0.6f, 0); range.radius = 1.6f;
        var sprite = new GameObject("Sprite"); sprite.transform.SetParent(go.transform, false); sprite.transform.localPosition = new Vector3(0, 0.56f, 0);
        var renderer = sprite.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = spriteMat; renderer.color = tint; sprite.AddComponent<Billboard>();
        var frames = AssetDatabase.LoadAllAssetsAtPath($"Assets/Undead Survivor/Sprites/Farmer {farmer}.png").OfType<Sprite>().Where(s => s.name.StartsWith("Stand")).OrderBy(s => s.name).ToArray();
        if (frames.Length == 0) Debug.LogWarning($"[Future] Undead Survivor 'Farmer {farmer}' sprites not found: {name} has no art.");
        else renderer.sprite = frames[0];
        // "LabTechnician" -> "Talk to the lab technician" (CaveNpc's own default would run the words together)
        string spaced = System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();
        var npc = go.AddComponent<CaveNpc>(); npc.sprite = renderer; npc.frames = frames; npc.framesPerSecond = fps; npc.lines = lines; npc.talkPrompt = "Talk to the " + spaced;
        Save(go, Prefabs + "/" + name + ".prefab");
    }

    static void MakeEnemies()
    {
        // Sentry Bot: the Medieval skeleton (patrol, chase, stomp), re-tinted as a lab robot
        GameObject bot = PrefabUtility.LoadPrefabContents("Assets/prefabs/Medieval/Skeleton.prefab"); bot.name = "SentryBot";
        try
        {
            var skeleton = bot.GetComponent<SkeletonEnemy>(); if (skeleton == null) throw new InvalidOperationException("Skeleton.prefab has no SkeletonEnemy.");
            skeleton.killTimeReward = 5f;
            if (skeleton.sprite != null) skeleton.sprite.color = new Color(0.55f, 0.9f, 1f);
            PrefabUtility.SaveAsPrefabAsset(bot, Prefabs + "/SentryBot.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(bot); }

        // Security Drone: the Clockwork Guard's brain and blades on the same body (as SteampunkSceneBuilder.MakeGuard)
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/prefabs/Medieval/Skeleton.prefab"); root.name = "SecurityDrone";
        try
        {
            var skeleton = root.GetComponent<SkeletonEnemy>(); if (skeleton != null) UnityEngine.Object.DestroyImmediate(skeleton);
            var guard = root.AddComponent<ClockworkGuard>();
            Transform sprite = root.transform.Find("Sprite");
            if (sprite != null)
            {
                guard.sprite = sprite.GetComponent<SpriteRenderer>(); guard.animator = sprite.GetComponent<Animator>();
                if (guard.sprite != null) guard.sprite.color = new Color(0.8f, 0.95f, 1f);
            }
            var blades = new GameObject("Blades"); blades.transform.SetParent(root.transform, false); blades.transform.localPosition = new Vector3(0, 0.6f, 0);
            FutureProps.Box("Blade", blades.transform, Vector3.zero, new Vector3(2.7f, 0.06f, 0.2f), M(FutureSurface.Danger));
            FutureProps.Box("Blade", blades.transform, Vector3.zero, new Vector3(0.2f, 0.06f, 2.7f), M(FutureSurface.Danger));
            FutureProps.Cyl("Hub", blades.transform, Vector3.zero, 0.4f, 0.16f, M(FutureSurface.MetalDark));
            guard.blades = blades.transform;
            var sparks = Emitter("Death sparks", root.transform, new Vector3(0, 0.6f, 0), sparkSprite, 3.5f, 0.2f, 0.2f);
            sparks.spread = 1f; sparks.gravity = 6f; sparks.burstCount = 20; sparks.startColor = Hex("BFF8FF"); sparks.endColor = new Color(0.3f, 0.9f, 1f, 0f);
            guard.deathSparks = sparks;
            guard.killReward = 10f;
            PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/SecurityDrone.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void MakeMachine()
    {
        string path = Prefabs + "/FutureTimeMachine.prefab";
        // Copied once and then edited in place, so its GUID (and every reference to it) stays stable across rebuilds
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset("Assets/prefabs/Medieval/TimeMachine.prefab", path);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = "FutureTimeMachine"; var machine = root.GetComponent<TimeMachine>();
            machine.loadNextEra = false; // FutureLevel turns this on if an era is ever added after Future
            machine.inspectPrompt = "Inspect the time machine"; machine.repairPrompt = "Charge from the Chrono Core";
            machine.brokenLines = new[] { "SYSTEM: \"Temporal charge 0%.\"" };
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------------------------------------------------------- the level

    static void MakeLevel()
    {
        walls.Clear();
        var root = new GameObject("FutureLevel");
        var shell = Group("Shell", root.transform);
        var props = Group("Props", root.transform);
        var gameplay = Group("Gameplay", root.transform);
        var hazards = Group("Hazards", root.transform);
        var enemies = Group("Enemies", root.transform);
        var people = Group("People", root.transform);
        var coins = Group("Time Coins", root.transform);
        var director = new GameObject("LevelDirector"); director.transform.SetParent(root.transform, false);
        var level = director.AddComponent<FutureLevel>();

        MakeShell(shell);
        var placed = MakeProps(props);
        MakeCore(level, placed["CORE-01"]);
        // Calibration Bay's lamp is the warning red one
        var bayLamp = placed["BAY-03"].transform.Find(PropSlot.FunctionName + "/Light");
        if (bayLamp != null) bayLamp.GetComponent<Light>().color = new Color(1f, 0.35f, 0.35f);

        // Time machine, and a floor conduit that lights up when the core can charge it
        var machine = Place("FutureTimeMachine", gameplay, FutureLab.MachinePoint);
        level.timeMachine = machine.GetComponent<TimeMachine>();
        FutureProps.Box("Charging conduit", gameplay, new Vector3(-15.4f, 0.015f, 6f), new Vector3(6.6f, 0.03f, 0.3f), M(FutureSurface.MetalDark));
        var conduit = FutureProps.Box("Charging conduit (online)", gameplay, new Vector3(-15.4f, 0.035f, 6f), new Vector3(6.6f, 0.02f, 0.14f), M(FutureSurface.Glow));
        level.coreOnlineVisuals = level.coreOnlineVisuals.Concat(new[] { conduit }).ToArray();
        var board = Emitter("Board burst", gameplay, FutureLab.MachinePoint + new Vector3(0, 0.4f, 0), puffSprite, 3f, 0.4f, 1.4f);
        board.spread = 1f; board.burstCount = 30; board.startColor = new Color(0.9f, 1f, 1f, 0.9f); board.endColor = new Color(0.4f, 0.9f, 1f, 0f);
        level.boardBurst = board;

        // Keycard, the Core Hall door, the barrier back to the concourse, the two stabilisers
        Place("Keycard", gameplay, new Vector3(-10.6f, 0, 12.8f));
        level.securityDoor = Place("SecurityDoor", gameplay, new Vector3(4f, 0, 12.475f), 90f).GetComponent<KeycardDoor>();
        level.coreBarrier = Place("CoreBarrier", gameplay, new Vector3(21.5f, 0, -4f)).GetComponentInChildren<Gate>();

        var lasers = new List<LaserFence>();
        foreach (var (z, phase) in new[] { (7.0f, 0f), (9.4f, 0.5f) })
        {
            var lf = Place("LaserFence", hazards, new Vector3(23f, 0, z)).GetComponent<LaserFence>();
            lf.phaseOffset = phase; lasers.Add(lf);
        }
        var stabA = Place("ChronoStabiliser", gameplay, new Vector3(7f, 0, 3f)).GetComponent<ChronoStabiliser>();
        stabA.name = "Stabiliser A"; stabA.hint = "Past the rift field";
        var stabB = Place("ChronoStabiliser", gameplay, new Vector3(23f, 0, 11.8f)).GetComponent<ChronoStabiliser>();
        stabB.name = "Stabiliser B"; stabB.hint = "In the Calibration Bay, behind the lasers";
        stabB.powerOff = lasers.ToArray(); stabB.startLines = new[] { "SYSTEM: \"Calibration complete. Laser grid offline.\"" };
        level.stabilisers = new[] { stabA, stabB };

        // The bay entry: a hint that the lasers run on local time
        var story = new GameObject("Story: laser grid"); story.transform.SetParent(gameplay, false); story.transform.localPosition = new Vector3(24.375f, 1f, 5f);
        var storyBox = story.AddComponent<BoxCollider>(); storyBox.isTrigger = true; storyBox.size = new Vector3(2.75f, 2f, 2f);
        story.AddComponent<StoryTrigger>().lines = new[]
        {
            "SYSTEM: \"Laser grid active. It cycles with local time.\"",
            "PLAYER: \"So it only moves when I do. Wonderful.\"",
        };

        // Rifts: the field west of the core, and two in the concourse
        var rifts = new List<TimeRift>();
        foreach (var p in new[] { new Vector2(6.6f, 10.6f), new Vector2(9.0f, 9.4f), new Vector2(5.6f, 8.2f), new Vector2(8.0f, 6.6f), new Vector2(5.4f, 5.0f), new Vector2(-6f, -6f), new Vector2(0f, -11.5f) })
            rifts.Add(Place("TimeRift", hazards, new Vector3(p.x, 0, p.y)).GetComponent<TimeRift>());
        level.rifts = rifts.ToArray();

        // Piston checkpoint at x 8: one press per doorway, staggered
        foreach (var (z, phase) in new[] { (-12f, 0f), (-9f, 0.33f), (-6f, 0.66f) })
            Place("PistonGate", hazards, new Vector3(8f, 0, z)).GetComponent<SteamPress>().phaseOffset = phase;

        // Enemies
        Bot(enemies, new Vector3(-9.5f, 0, 12.5f), new Vector3(2.5f, 0, 12.5f));
        Bot(enemies, new Vector3(12f, 0, -10f), new Vector3(25f, 0, -10f));
        var drone = Place("SecurityDrone", enemies, new Vector3(16f, 0, 3f)).GetComponent<ClockworkGuard>();
        drone.patrolA = new Vector3(13f, 0, 3f); drone.patrolB = new Vector3(19f, 0, 3f);

        // People
        Place("LabTechnician", people, new Vector3(-23f, 0, 1f));
        Orbit(Place("CuriousKid", people, new Vector3(-15.2f, 0, -9f)), new Vector3(-16f, 0, -9f), 0.8f, 0.6f);
        Place("Archivist", people, new Vector3(-6f, 0, 0.6f));
        Orbit(Place("MaintenanceUnit", people, new Vector3(6.7f, 0, 12.3f)), new Vector3(6.2f, 0, 12.3f), 0.5f, 0.9f);
        Place("SecurityOfficer", people, new Vector3(24.5f, 0, 0f));
        Place("FrozenCommuter", people, new Vector3(-4f, 0, -12.4f));

        // Time Coins: +12 s on the way, +40 s for the greedy
        foreach (var (tier, x, z) in new[]
        {
            ("Bronze", -15.5f, -1.6f), ("Bronze", -10.2f, 7.0f), ("Bronze", -2.0f, 12.4f), ("Bronze", 5.0f, 6.6f), ("Bronze", 14.0f, -6.0f), ("Bronze", -4.0f, -8.0f),
            ("Silver", 2.2f, 4.5f), ("Silver", -24.5f, 12.5f), ("Silver", 17.5f, 3.0f), ("Silver", 8.0f, -12.0f),
            ("Gold", 7.2f, 8.6f), ("Gold", 25.0f, -12.8f),
        })
            Place("TimeCoin" + tier, coins, new Vector3(x, 0, z));

        Save(root, LevelPath);
    }

    static void Bot(Transform parent, Vector3 a, Vector3 b)
    {
        var bot = Place("SentryBot", parent, (a + b) * 0.5f).GetComponent<SkeletonEnemy>();
        bot.patrolA = a; bot.patrolB = b;
    }

    static void Orbit(GameObject go, Vector3 centre, float radius, float speed)
    {
        var npc = go.GetComponent<CaveNpc>();
        npc.orbitCenter = centre; npc.orbitRadius = radius; npc.orbitSpeed = speed;
    }

    // The Chrono Core (slot CORE-01): offline cracks and an online glow on its Function child, so they survive an art swap
    static void MakeCore(FutureLevel level, PropSlot core)
    {
        var function = core.transform.Find(PropSlot.FunctionName);
        if (function == null) { function = new GameObject(PropSlot.FunctionName).transform; function.SetParent(core.transform, false); }
        Vector3 f = core.footprint;
        var offline = Group("Offline", function);
        foreach (float x in new[] { -0.8f, 0.5f })
            FutureProps.Box("Rift crack", offline, new Vector3(x, f.y * 0.45f, -f.z * 0.5f - 0.02f), new Vector3(0.12f, f.y * 0.6f, 0.02f), M(FutureSurface.Rift));
        FutureProps.Box("Warning band", offline, new Vector3(0, 0.3f, 0), new Vector3(f.x + 0.06f, 0.12f, f.z + 0.06f), M(FutureSurface.GlowWarm));
        var online = Group("Online", function);
        foreach (float y in new[] { 0.3f, 0.5f, 0.7f, 0.9f })
            FutureProps.Box("Glow ring", online, new Vector3(0, f.y * y, 0), new Vector3(f.x + 0.1f, 0.15f, f.z + 0.1f), M(FutureSurface.Glow));
        var light = FutureProps.Light("Core light", function, new Vector3(0, f.y * 0.5f, -f.z * 0.5f - 0.8f), new Color(0.6f, 0.3f, 1f), 1.5f, 10f);
        level.coreOfflineVisuals = new[] { offline.gameObject };
        level.coreOnlineVisuals = new[] { online.gameObject };
        level.coreLight = light;
    }

    // ---------------------------------------------------------------- shell: floor and walls

    static void MakeShell(Transform s)
    {
        // Floor: one collider, 4x4 visual tiles (so each tile picks up its nearest lamps)
        var floorCol = new GameObject("Floor collider"); floorCol.transform.SetParent(s, false); floorCol.transform.localPosition = new Vector3(0, -0.25f, 0);
        floorCol.AddComponent<BoxCollider>().size = new Vector3(52f, 0.5f, 28f);
        var floor = Group("Floor", s);
        for (float x = FutureLab.MinX; x < FutureLab.MaxX - 0.01f; x += 4f)
            for (float z = FutureLab.MinZ; z < FutureLab.MaxZ - 0.01f; z += 4f)
                FutureProps.Box("Floor", floor, new Vector3(x + 2f, -0.25f, z + 2f), new Vector3(4f, 0.5f, 4f), M(FutureSurface.Floor));
        // Dark ground outside, so the camera never looks past the walls
        FutureProps.Box("Outside ground", s, new Vector3(0, -0.7f, 0), new Vector3(160f, 0.4f, 120f), M(FutureSurface.Void));

        const float t = FutureLab.WallThickness * 0.5f;
        // Outer walls. The north wall faces the camera and never hides anything, so it carries the screens and signs
        Wall("N", s, new Vector3(-26.25f, 0, 13.75f), new Vector3(26.25f, H, 14.25f), FutureSurface.Wall, true);
        Wall("W", s, new Vector3(-26 - t, 0, -14f), new Vector3(-26 + t, H, 14f), FutureSurface.Wall);
        Wall("E", s, new Vector3(26 - t, 0, -14f), new Vector3(26 + t, H, 14f), FutureSurface.Wall);
        SouthKneeWall(s);

        // X1: Arrival Bay | Archive and Concourse, with the Archive doorway (z -3..0) and the Concourse archway (z -10.5..-7.5)
        foreach (var (z0, z1) in new[] { (-13.75f, -10.5f), (-7.5f, -3f), (0f, 13.75f) })
            Wall("X1", s, new Vector3(-12 - t, 0, z0), new Vector3(-12 + t, H, z1), FutureSurface.Wall);
        foreach (float z in new[] { -10.5f, -7.5f, -3f, 0f })
            Dress("X1", -12f, z, new Vector3(-12f, H * 0.5f, z), new Vector3(0.62f, H, 0.1f), FutureSurface.Trim);

        // W3: the north rooms | Concourse (the only interior east-west wall), gap x 20..23 for the Core Barrier,
        // with a wall panel strip along its camera side
        foreach (var (x0, x1) in new[] { (-11.75f, 20f), (23f, 25.75f) })
        {
            var piece = Wall("W3", s, new Vector3(x0, 0, -4 - t), new Vector3(x1, H, -4 + t), FutureSurface.Wall);
            FutureProps.Box("Panel strip", piece, new Vector3(0, -H * 0.5f + 0.7f, -t - 0.02f), new Vector3(x1 - x0, 1.2f, 0.04f), M(FutureSurface.WallPanel));
            FutureProps.Box("Glow line", piece, new Vector3(0, -H * 0.5f + 1.35f, -t - 0.03f), new Vector3(x1 - x0, 0.06f, 0.03f), M(FutureSurface.Glow));
        }
        // X2: Archive | Core Hall, gap z 11.2..13.75 for the Security Door. X3: Core Hall | Calibration Bay
        Wall("X2", s, new Vector3(4 - t, 0, -3.75f), new Vector3(4 + t, H, 11.2f), FutureSurface.Wall);
        Wall("X3", s, new Vector3(20 - t, 0, 5f), new Vector3(20 + t, H, 13.75f), FutureSurface.Wall);
        // The Calibration Bay's front is a low railing, so the camera sees the lasers. Entry at x 23..25.75
        Railing(s, new Vector3(20.25f, 0, 5f), new Vector3(23f, 0, 5f));

        // CHK: the piston checkpoint at x 8, three 2.2-wide doorways (z -12, -9, -6) with danger stripes on their frames
        foreach (var (z0, z1) in new[] { (-13.75f, -13.1f), (-10.9f, -10.1f), (-7.9f, -7.1f), (-4.9f, -4.25f) })
            Wall("CHK", s, new Vector3(8 - t, 0, z0), new Vector3(8 + t, H, z1), FutureSurface.Wall);
        foreach (float z in new[] { -13.1f, -10.9f, -10.1f, -7.9f, -7.1f, -4.9f })
            Dress("CHK", 8f, z, new Vector3(8f, H * 0.5f, z), new Vector3(0.54f, H, 0.06f), FutureSurface.Danger);
    }

    // A wall segment: its BoxCollider on the root and its ~4 unit visual tiles as children, so the OccluderFader cuts
    // every tile of the segment it hits (and lights stay per tile). Registered by id for wall-mounted props
    static Transform Wall(string id, Transform parent, Vector3 min, Vector3 max, FutureSurface surface, bool cap = false)
    {
        var seg = new GameObject("Wall " + id).transform; seg.SetParent(parent, false);
        Vector3 size = max - min; seg.localPosition = (min + max) * 0.5f;
        seg.gameObject.AddComponent<BoxCollider>().size = size;
        Tiles(seg, size, M(surface));
        if (cap) FutureProps.Box("Cap", seg, new Vector3(0, size.y * 0.5f + 0.04f, 0), new Vector3(size.x + 0.04f, 0.08f, size.z + 0.04f), M(FutureSurface.Trim));
        walls.Add((id, seg, min, max));
        return seg;
    }

    static void Tiles(Transform seg, Vector3 size, Material mat)
    {
        const float tile = 4f;
        int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / tile - 0.01f)), ny = Mathf.Max(1, Mathf.CeilToInt(size.y / tile - 0.01f)), nz = Mathf.Max(1, Mathf.CeilToInt(size.z / tile - 0.01f));
        Vector3 min = -size * 0.5f;
        for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) for (int k = 0; k < nz; k++)
        {
            Vector3 a = new Vector3(min.x + size.x * i / nx, min.y + size.y * j / ny, min.z + size.z * k / nz);
            Vector3 c = new Vector3(min.x + size.x * (i + 1) / nx, min.y + size.y * (j + 1) / ny, min.z + size.z * (k + 1) / nz);
            FutureProps.Box("Tile", seg, (a + c) * 0.5f, c - a, mat);
        }
    }

    // The camera side: a low visible knee wall, and above it a wall that only casts the sun's shadow (like Steampunk's front)
    static void SouthKneeWall(Transform parent)
    {
        const float knee = 1f;
        var seg = Wall("S", parent, new Vector3(-26.25f, 0, -14.25f), new Vector3(26.25f, 0, -13.75f), FutureSurface.Wall);
        // Wall() tiled nothing visible at zero height: give it the real collider and pieces
        for (int i = seg.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(seg.GetChild(i).gameObject);
        seg.localPosition = new Vector3(0, H * 0.5f, -14f);
        var col = seg.GetComponent<BoxCollider>(); col.size = new Vector3(52.5f, H, 0.5f);
        for (float x = -26.25f; x < 26.2f; x += 52.5f / 13f)
            FutureProps.Box("Knee", seg, new Vector3(x + 52.5f / 26f, -H * 0.5f + knee * 0.5f, 0), new Vector3(52.5f / 13f, knee, 0.5f), M(FutureSurface.Wall));
        FutureProps.Box("Knee cap", seg, new Vector3(0, -H * 0.5f + knee + 0.04f, 0), new Vector3(52.54f, 0.08f, 0.6f), M(FutureSurface.Trim));
        var shadow = FutureProps.Box("Wall (shadow only)", seg, new Vector3(0, knee * 0.5f, 0), new Vector3(52.5f, H - knee, 0.5f), M(FutureSurface.Wall));
        shadow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        walls[walls.Count - 1] = ("S", seg, new Vector3(-26.25f, 0, -14.25f), new Vector3(26.25f, H, -13.75f));
    }

    // Low railing on the floor: posts and rails you can see over, and a taller invisible collider so it can't be jumped
    static void Railing(Transform parent, Vector3 a, Vector3 b)
    {
        var r = new GameObject("Railing").transform; r.SetParent(parent, false); r.localPosition = (a + b) * 0.5f;
        Vector3 d = b - a; float len = d.magnitude; bool alongX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
        Vector3 Size(float thick, float h) => alongX ? new Vector3(len, h, thick) : new Vector3(thick, h, len);
        var col = r.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.1f, 0); col.size = Size(0.15f, 2.2f);
        FutureProps.Box("Top rail", r, new Vector3(0, 1.05f, 0), Size(0.1f, 0.1f), M(FutureSurface.Trim));
        FutureProps.Box("Glow rail", r, new Vector3(0, 0.6f, 0), Size(0.05f, 0.05f), M(FutureSurface.Glow));
        int posts = Mathf.Max(1, Mathf.RoundToInt(len / 1.3f));
        for (int i = 0; i <= posts; i++)
            FutureProps.Box("Post", r, Vector3.Lerp(-d * 0.5f, d * 0.5f, i / (float)posts) + Vector3.up * 0.55f, new Vector3(0.08f, 1.1f, 0.08f), M(FutureSurface.Trim));
    }

    // A non-solid piece of dressing parented to the wall piece it sits on (so it's cut with that wall)
    static void Dress(string wallId, float x, float z, Vector3 worldCentre, Vector3 size, FutureSurface surface)
    {
        var piece = WallAt(wallId, x, z);
        if (piece == null) return;
        FutureProps.Box(surface + " frame", piece, piece.InverseTransformPoint(worldCentre), size, M(surface));
    }

    // The registered wall piece with this id nearest to (x, z)
    static Transform WallAt(string id, float x, float z)
    {
        Transform best = null; float bestDistance = float.MaxValue;
        foreach (var w in walls)
        {
            if (w.id != id) continue;
            float dx = Mathf.Max(0f, Mathf.Max(w.min.x - x, x - w.max.x)), dz = Mathf.Max(0f, Mathf.Max(w.min.z - z, z - w.max.z));
            float distance = dx * dx + dz * dz;
            if (distance < bestDistance) { bestDistance = distance; best = w.piece; }
        }
        return best;
    }

    // ---------------------------------------------------------------- prop slots (the flexible part)

    // One row per prop. Pos (x, z), y = height of the slot's base. Yaw is in the design doc's sense:
    // 0 = front faces the camera (south), 90 = faces east, -90 = faces west. An empty size uses the role's default.
    // Move, add or remove a prop here, then run Build Future Era again
    struct Slot
    {
        public string id; public PropRole role; public float x, y, z, yaw; public Vector3? size; public bool? blocks; public bool tall; public string mount, label;
    }

    static Slot S(string id, PropRole role, float x, float z, float y = 0f, float yaw = 0f, Vector3? size = null, bool? blocks = null, bool tall = false, string mount = null, string label = null)
        => new Slot { id = id, role = role, x = x, y = y, z = z, yaw = yaw, size = size, blocks = blocks, tall = tall || role == PropRole.Pillar, mount = mount, label = label };

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    static IEnumerable<Slot> Slots()
    {
        // Arrival Bay
        yield return S("ARR-01", PropRole.Decal, -19, 6, y: 0.01f, size: V(6, 0.02f, 6), label: "Landing ring under the machine");
        yield return S("ARR-02", PropRole.Console, -25.2f, 6, yaw: 90);
        yield return S("ARR-03", PropRole.Console, -25.2f, 2.5f, yaw: 90);
        yield return S("ARR-04", PropRole.Terminal, -14, 7, yaw: -90);
        yield return S("ARR-05", PropRole.CargoStack, -24.8f, -12.8f);
        yield return S("ARR-06", PropRole.Crate, -23.3f, -13.0f, yaw: 15);
        yield return S("ARR-07", PropRole.Crate, -23.4f, -11.8f, yaw: -10);
        yield return S("ARR-08", PropRole.Crate, -23.3f, -13.0f, y: 1f, yaw: 30, label: "Stacked on ARR-06");
        yield return S("ARR-09", PropRole.Pillar, -22.5f, 10, label: "Pad pillar");
        yield return S("ARR-10", PropRole.Pillar, -15.5f, 10, label: "Pad pillar");
        yield return S("ARR-11", PropRole.Screen, -22, 13.65f, y: 3.2f);
        yield return S("ARR-12", PropRole.Screen, -16, 13.65f, y: 3.2f);
        yield return S("ARR-13", PropRole.Sign, -19, 13.65f, y: 4.8f, label: "ARRIVAL BAY");
        yield return S("ARR-14", PropRole.Lamp, -19, 6, y: 5.6f);
        yield return S("ARR-15", PropRole.Lamp, -19, -6, y: 5.6f);
        yield return S("ARR-16", PropRole.Plant, -13, 12.8f);
        yield return S("ARR-17", PropRole.Plant, -13, -12.8f);
        yield return S("ARR-18", PropRole.Bench, -18, -13.1f);
        yield return S("ARR-19", PropRole.WallPanel, -25.7f, -4, y: 3f, yaw: 90);
        yield return S("ARR-20", PropRole.WallPanel, -25.7f, 0, y: 3f, yaw: 90);
        yield return S("ARR-21", PropRole.Desk, -23, -0.3f, label: "Lab technician's desk");

        // Archive: three rows of server racks
        int n = 1;
        foreach (float x in new[] { -8f, -4f, 0f })
            foreach (float z in new[] { 4f, 6f, 8f, 10f })
                yield return S($"ARC-{n++:00}", PropRole.ServerRack, x, z);
        yield return S("ARC-13", PropRole.Shelf, 2.2f, 10.6f, size: V(3.1f, 3, 0.8f), tall: true, label: "Closes the east aisle's north end");
        yield return S("ARC-14", PropRole.Desk, -6, -0.6f, label: "Archivist's desk");
        yield return S("ARC-15", PropRole.Terminal, -11.2f, 13.4f, label: "Filing terminal by the keycard");
        yield return S("ARC-16", PropRole.Lamp, -6, 7, y: 5.6f);
        yield return S("ARC-17", PropRole.Lamp, -2, 12.5f, y: 5.6f);
        yield return S("ARC-18", PropRole.Screen, -4, 13.65f, y: 3.5f);
        yield return S("ARC-19", PropRole.Sign, -4, 13.65f, y: 5.0f, label: "ARCHIVE");
        yield return S("ARC-20", PropRole.Crate, -11, -3.3f, yaw: 10);
        yield return S("ARC-21", PropRole.Plant, 3.2f, -3.3f);

        // Core Hall
        yield return S("CORE-01", PropRole.Machine, 12, 8, size: V(4, 7, 4), tall: true, label: "Chrono Core");
        yield return S("CORE-02", PropRole.Hologram, 12, 8, y: 7.3f, label: "Time readout above the core");
        yield return S("CORE-03", PropRole.Console, 15, 12.9f);
        yield return S("CORE-04", PropRole.Console, 17.5f, 12.9f);
        yield return S("CORE-05", PropRole.CargoStack, 19.2f, 13.1f);
        yield return S("CORE-06", PropRole.Pillar, 9, -2.5f);
        yield return S("CORE-07", PropRole.Pillar, 15.5f, -2.5f);
        yield return S("CORE-08", PropRole.Screen, 8, 13.65f, y: 3.5f);
        yield return S("CORE-09", PropRole.Screen, 16, 13.65f, y: 3.5f);
        yield return S("CORE-10", PropRole.Sign, 12, 13.65f, y: 5.2f, label: "CHRONO CORE");
        yield return S("CORE-11", PropRole.Lamp, 8, 8, y: 5.6f);
        yield return S("CORE-12", PropRole.Lamp, 16, 4, y: 5.6f);
        yield return S("CORE-13", PropRole.Lamp, 12, 0, y: 5.6f);
        yield return S("CORE-14", PropRole.Vent, 16, 11, y: 0.01f);
        yield return S("CORE-15", PropRole.Crate, 25.3f, -3.3f);
        yield return S("CORE-16", PropRole.Crate, 24.2f, -3.4f, yaw: 20);
        yield return S("CORE-17", PropRole.Plant, 5, -3.3f);
        yield return S("CORE-18", PropRole.Desk, 24.5f, -1.3f, label: "Security officer's desk");
        yield return S("CORE-19", PropRole.Pipe, 19.6f, 9.5f, y: 4.5f, size: V(0.3f, 0.3f, 9), mount: "X3");

        // Calibration Bay
        yield return S("BAY-01", PropRole.Machine, 25, 13, size: V(1.6f, 2.5f, 1.6f), label: "Calibration rig");
        yield return S("BAY-02", PropRole.Terminal, 21, 13.2f);
        yield return S("BAY-03", PropRole.Lamp, 23, 10.5f, y: 5.6f, label: "Red warning lamp");
        yield return S("BAY-04", PropRole.Sign, 23, 13.65f, y: 4.5f, label: "CALIBRATION");

        // Transit Concourse
        yield return S("CON-01", PropRole.Bench, -8, -13.1f);
        yield return S("CON-02", PropRole.Bench, 1, -13.1f);
        yield return S("CON-03", PropRole.Bench, 12, -13.1f);
        yield return S("CON-04", PropRole.Planter, -2, -5.0f);
        yield return S("CON-05", PropRole.Planter, 12, -5.0f);
        yield return S("CON-06", PropRole.Planter, 16.5f, -5.0f);
        yield return S("CON-07", PropRole.Machine, 19, -13, size: V(6, 2.5f, 1.8f), label: "Parked maglev pod");
        yield return S("CON-08", PropRole.Decal, 7, -9, y: 0.01f, size: V(37, 0.02f, 0.3f), label: "Floor guide line");
        yield return S("CON-09", PropRole.Screen, -6, -4.35f, y: 3.2f, mount: "W3");
        yield return S("CON-10", PropRole.Screen, 12, -4.35f, y: 3.2f, mount: "W3");
        yield return S("CON-11", PropRole.Sign, 2, -4.35f, y: 4.8f, mount: "W3", label: "TRANSIT CONCOURSE");
        yield return S("CON-12", PropRole.Lamp, -4, -9, y: 5.6f);
        yield return S("CON-13", PropRole.Lamp, 14, -9, y: 5.6f);
        yield return S("CON-14", PropRole.Lamp, 22, -9, y: 5.6f);
        yield return S("CON-15", PropRole.Hologram, -2, -9, y: 0.2f, label: "Advert, floating");
        yield return S("CON-16", PropRole.Plant, -11.2f, -13.2f);
        yield return S("CON-17", PropRole.Plant, 25.3f, -4.8f);
        yield return S("CON-18", PropRole.WallPanel, -9, -4.35f, y: 3f, mount: "W3");
        yield return S("CON-19", PropRole.WallPanel, 6, -4.35f, y: 3f, mount: "W3");
    }

    // Places every slot and dresses it from the art set; returns them by id for wiring
    static Dictionary<string, PropSlot> MakeProps(Transform parent)
    {
        var placed = new Dictionary<string, PropSlot>();
        var rooms = new Dictionary<string, Transform>();
        foreach (var s in Slots())
        {
            string room = s.id.Split('-')[0];
            if (!rooms.TryGetValue(room, out var group)) rooms[room] = group = Group(room, parent);
            var go = new GameObject("Slot " + s.id);
            go.transform.SetParent(group, false);
            go.transform.localPosition = new Vector3(s.x, s.y, s.z);
            // Unity turns +Z towards +X for a positive yaw, so a front (-Z) facing east needs a negative one
            go.transform.localRotation = Quaternion.Euler(0f, -s.yaw, 0f);
            var slot = go.AddComponent<PropSlot>();
            slot.id = s.id; slot.role = s.role; slot.label = s.label ?? "";
            slot.footprint = s.size ?? FutureProps.DefaultFootprint(s.role);
            slot.blocks = s.blocks ?? FutureProps.DefaultBlocks(s.role);
            slot.tallCollider = s.tall;
            slot.mountTo = s.mount ?? "";
            if (!string.IsNullOrEmpty(s.mount))
            {
                var wall = WallAt(s.mount, s.x, s.z);
                if (wall != null) go.transform.SetParent(wall, true);
            }
            slot.Dress(art);
            placed[s.id] = slot;
        }
        return placed;
    }

    // ---------------------------------------------------------------- helpers

    static SteamEmitter Emitter(string name, Transform parent, Vector3 pos, Sprite sprite, float speed, float startSize, float endSize)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        var e = go.AddComponent<SteamEmitter>(); e.sprite = sprite; e.material = puffMat; e.rate = 0f; e.speed = speed; e.startSize = startSize; e.endSize = endSize;
        return e;
    }

    static void AddBuildScene()
    {
        // Play order: ... Steampunk, Future, then everything else as it was
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        int after = scenes.FindIndex(s => s.path == "Assets/Scenes/Steampunk.unity");
        if (after < 0) after = scenes.FindIndex(s => s.path == "Assets/Scenes/Medieval.unity");
        scenes.Insert(after >= 0 ? after + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
    }

    static void Folder(string parent, string name) { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }
    static GameObject Load(string path) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (p == null) throw new InvalidOperationException(path); return p; }
    static GameObject Place(string prefab, Transform parent, Vector3 pos, float yaw = 0f)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Load(Prefabs + "/" + prefab + ".prefab"));
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return go;
    }
    static Transform Group(string name, Transform parent) { var g = new GameObject(name).transform; g.SetParent(parent, false); return g; }
    static Transform Find(Transform parent, string name) { foreach (Transform child in parent) { if (child.name == name) return child; Transform result = Find(child, name); if (result != null) return result; } return null; }
    static void Save(GameObject root, string path) { PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root); }
}
