using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// Applies the Medieval revamp (.claude/medieval-revamp/plan.md) to the open Medieval scene: spawn by the time machine,
// a breach in the raised wall, a trap-free Great Hall, the Treasure Vault behind it (moved traps, chest on a dais,
// vault gate and lever), towers, battlements and an arch over the gate, Caveman and mud textures on walls and floors,
// lanterns, signposts and the Slavic kit props.
// Safe to re-run: existing objects are found by name and set to fixed values, and everything it creates lives under
// Level/"Revamp (generated)", which is deleted and rebuilt each run. The whole run is one Undo step. Save the scene after.
//
// Props are a plain table (see Props()), so swapping in whatever props we end up with is a one-line edit:
// - name any prefab in the Slavic kit (or a toon copy in Assets/prefabs/Medieval/Slavic, which wins), with or without
//   "_PRE", or a family prefix ("EA03_Nature_Bush_03") to take the first match, or a full "Assets/..." prefab path
// - it gets the kit's family scale (SlavicKitLook.ScaleFor: 1 kit metre = 0.83 units, containers sized to our Barrel),
//   then is shrunk to fit any height/width/depth given
// - x/z is the footprint centre, and it is dropped onto the floor (or onto what is already there, with y: Stack)
// - "solid" picks its collider: None (rugs, decals), Low (its own box), Tall (6 high, can't be jumped onto), Trunk
// - missing prefabs are skipped and listed in the Console, so the rest still builds
// Kit materials get the toon look from "Medieval: Restyle Village Kit" (SlavicKitLook), run before or after this.
public static class MedievalRevamp
{
    const string RootName = "Revamp (generated)";
    const string Prefabs = "Assets/prefabs/Medieval";
    const string Mats = "Assets/Materials/Medieval";
    const string RevampMats = "Assets/Materials/Medieval/Revamp";
    const string KitPrefabs = "Assets/EmaceArt/Slavic World Free/Prefabs";
    const string KitTextures = "Assets/EmaceArt/Slavic World Free/Texture";
    // Toon prefab variants of kit pieces, if anyone makes them: used in preference to the raw kit
    const string ConvertedPrefabs = "Assets/prefabs/Medieval/Slavic";
    // 1 kit metre in game units (a 1.8 m villager is our 1.5-unit player); SlavicKitLook's value wins when it exists
    const float KitMetre = 0.83f;
    const string CavemanTextures = "Assets/Textures/Caveman";
    const string MudTile = "Assets/borrowed time/tiles/mud.asset";
    const string LanternPath = Prefabs + "/Lantern.prefab";

    // Taller than any jump (MedievalJump reaches 1.6), like the Caveman blockers
    const float BlockHeight = 6f;

    // y for a prop that sits on whatever is already below it (a mug on a table)
    const float Stack = float.NaN;

    enum Solid { None, Low, Tall, Trunk }

    static readonly Vector3 SpawnAt = new Vector3(-12f, 0f, -13f);

    static Material stone, wood, iron, fire, gold, trapPlate;
    static Material cliff, vaultStone, towerStone, battlement, archStone, cobble, flagstone, vaultFloor, mud, dirt;
    static float kitScale;
    static bool restyleNoted;
    static Transform level, generated;
    static List<string> propPaths;
    static readonly List<string> notes = new List<string>();
    static readonly List<Bounds> placed = new List<Bounds>();
    static readonly Dictionary<Renderer, List<Renderer>> links = new Dictionary<Renderer, List<Renderer>>();
    static readonly Dictionary<string, GameObject> propCache = new Dictionary<string, GameObject>();

    [MenuItem("Tools/Out Of Time/Medieval: Apply Revamp")]
    public static void Apply()
    {
        // Edits made in Play mode are thrown away when it stops
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Leave Play mode before applying the Medieval revamp.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        level = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Level");
        if (level == null || !scene.path.EndsWith("Medieval.unity"))
        {
            Debug.LogError("Open Assets/Scenes/Medieval.unity first: the revamp edits its \"Level\" objects.");
            return;
        }

        restyleNoted = false; notes.Clear(); placed.Clear(); links.Clear(); propCache.Clear(); propPaths = null; generated = null;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Medieval Revamp");
        try
        {
            MakeMaterials();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LanternPath) == null)
                MakeLantern();
            kitScale = MeasureKitScale();

            var old = level.Find(RootName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            generated = new GameObject(RootName).transform;
            generated.SetParent(level, false);
            // Registered before anything is recorded on its children, so Undo removes those edits before the root
            Undo.RegisterCreatedObjectUndo(generated.gameObject, "Medieval Revamp");

            // Never touched by the builder: hand-placed props go here so they survive re-runs (see Fit Selected Kit Props)
            Section("9 Props (hand-placed)");

            PlayerSpawn(scene);
            Breach();
            Vault();
            Texts(scene);
            Castle(scene);
            Floors();
            Lights();
            Signs();
            Props();
            ApplyLinks();
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        foreach (var note in notes)
            Debug.LogWarning("Medieval revamp: " + note);
        Debug.Log($"Medieval revamp applied ({notes.Count} notes). Save the scene to keep it.");
    }

    // Rebuilds the mini lantern from code. Apply Revamp only builds it when it is missing, so hand edits to the prefab stay
    [MenuItem("Tools/Out Of Time/Medieval: Rebuild Lantern Prefab")]
    public static void RebuildLantern()
    {
        MakeMaterials();
        MakeLantern();
        AssetDatabase.SaveAssets();
        Debug.Log("Rebuilt " + LanternPath + ".");
    }

    // For props placed by hand: drag any Slavic kit prefab into the scene (e.g. under Level/9 Props (hand-placed)),
    // select it and run this. It gets what the table props get: family scale, LOD0 only, a box collider and
    // its bottom on the floor (or left where it is if it was set on top of something)
    [MenuItem("Tools/Out Of Time/Medieval: Fit Selected Kit Props")]
    public static void FitSelected()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Leave Play mode before fitting kit props: Play mode edits are thrown away.");
            return;
        }
        restyleNoted = false; notes.Clear(); propCache.Clear(); propPaths = null; generated = null;
        kitScale = MeasureKitScale();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Fit Kit Props");
        int fitted = 0;
        foreach (var root in Selection.gameObjects.Select(InstanceRoot).Distinct())
        {
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(root);
            string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : "";
            if (!path.StartsWith(KitPrefabs) && !path.StartsWith(ConvertedPrefabs)) { notes.Add(root.name + " is not a Slavic kit prefab instance, skipped."); continue; }
            Undo.RegisterFullObjectHierarchyUndo(root, "Fit Kit Props");
            Transform t = root.transform;
            float before = MeshBounds(t, null).min.y;
            Tidy(root);
            t.localScale = prefab.transform.localScale * KitScale(prefab);
            float floor = before > 0.3f ? before : 0f;
            t.position += Vector3.up * (floor - MeshBounds(t, null).min.y);
            Mark(t);
            if (!root.GetComponents<Collider>().Any(c => c.enabled)) AddSolid(root, Solid.Low);
            if (root.scene.IsValid()) EditorSceneManager.MarkSceneDirty(root.scene);
            fitted++;
        }
        Undo.CollapseUndoOperations(undoGroup);
        foreach (var note in notes)
            Debug.LogWarning("Fit kit props: " + note);
        Debug.Log($"Fitted {fitted} kit prop(s).");
    }

    // ---------------------------------------------------------------- materials

    static void MakeMaterials()
    {
        Folder("Assets/Materials", "Medieval");
        Folder(Mats, "Revamp");
        stone = Load<Material>(Mats + "/MedievalStone.mat");
        wood = Load<Material>(Mats + "/MedievalWood.mat");
        iron = Load<Material>(Mats + "/MedievalIron.mat");
        fire = Load<Material>(Mats + "/MedievalFire.mat");
        gold = Load<Material>(Mats + "/MedievalGold.mat");
        trapPlate = Load<Material>(Mats + "/MedievalTrapPlate.mat");
        if (stone == null)
            throw new InvalidOperationException("Missing " + Mats + "/MedievalStone.mat.");

        Texture2D cliffTex = Load<Texture2D>(CavemanTextures + "/Cliff32.png");
        Texture2D cobbleTex = Load<Texture2D>(CavemanTextures + "/Cobble32.png");
        Texture2D caveFloorTex = Load<Texture2D>(CavemanTextures + "/CaveFloor32.png");
        Texture2D stoneTex = Load<Texture2D>(CavemanTextures + "/Stone32.png");
        if (cliffTex == null || cobbleTex == null || caveFloorTex == null || stoneTex == null)
            notes.Add("Some Caveman textures are missing (run Build Caveman Era): those materials keep the stone bricks.");

        // The mud tile and the kit's pebbly dirt, copied to small point-filtered tiling textures (16-ish px per unit)
        var tile = Load<Tile>(MudTile);
        Texture2D mudTex = tile != null && tile.sprite != null ? PixelCopy("MudTile", tile.sprite.texture, tile.sprite.textureRect, Mathf.RoundToInt(tile.sprite.textureRect.width)) : null;
        var pebbles = Load<Texture2D>(KitTextures + "/Pobbles_Sand_Dirt.png");
        Texture2D dirtTex = pebbles != null ? PixelCopy("PebbleDirt64", pebbles, new Rect(0, 0, pebbles.width, pebbles.height), 64) : null;
        if (mudTex == null || dirtTex == null)
            notes.Add("Mud tile or kit Pobbles texture missing: mud/dirt use the tinted cave floor instead.");

        // Tiling 0.5 on a 32px texture = 16px per world unit, the project's texel size
        cliff = Variant("RevampCliff", cliffTex, new Color(0.9f, 0.86f, 0.8f), 0.5f);
        vaultStone = Variant("RevampVaultStone", cliffTex, new Color(0.62f, 0.6f, 0.68f), 0.5f);
        towerStone = Variant("RevampTowerStone", stoneTex, new Color(0.82f, 0.8f, 0.78f), 0.5f);
        battlement = Variant("RevampBattlement", null, stone.HasProperty("_BaseColor") ? stone.GetColor("_BaseColor") : Color.white, 0.5f);
        archStone = Variant("RevampArchStone", cobbleTex, new Color(0.95f, 0.92f, 0.86f), 0.5f);
        cobble = Variant("RevampCobble", cobbleTex, new Color(0.86f, 0.82f, 0.76f), 0.5f);
        flagstone = Variant("RevampFlagstone", cobbleTex, new Color(0.68f, 0.66f, 0.72f), 0.5f);
        vaultFloor = Variant("RevampVaultFloor", caveFloorTex, new Color(0.72f, 0.68f, 0.68f), 0.5f);
        mud = mudTex != null ? Variant("RevampMud", mudTex, Color.white, 16f / Mathf.Max(1, mudTex.width)) : Variant("RevampMud", caveFloorTex, new Color(0.62f, 0.45f, 0.3f), 0.5f);
        dirt = dirtTex != null ? Variant("RevampDirt", dirtTex, Color.white, 0.25f) : Variant("RevampDirt", caveFloorTex, new Color(0.78f, 0.62f, 0.45f), 0.5f);
    }

    // A copy of MedievalStone (toon Lit Cutout) with its own texture, mapped in world space so every wall and floor shows
    // the same pixel size whatever its scale. No top tiles: those read object extents, which static batching breaks
    static Material Variant(string name, Texture texture, Color tint, float tiling)
    {
        string path = RevampMats + "/" + name + ".mat";
        var m = Load<Material>(path);
        if (m == null) { m = new Material(stone); AssetDatabase.CreateAsset(m, path); }
        else { m.shader = stone.shader; m.CopyPropertiesFromMaterial(stone); }
        // Always the cutout shader: OccluderFader only cuts materials on it
        var cutout = Shader.Find("Out Of Time/Lit Cutout");
        if (cutout != null && m.shader != cutout) m.shader = cutout;
        if (texture != null) m.SetTexture("_BaseMap", texture);
        m.SetTextureScale("_BaseMap", Vector2.one * tiling);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
        m.SetFloat("_OOT_WorldUV", 1f);
        if (m.HasProperty("_OOT_TopCount")) m.SetFloat("_OOT_TopCount", 0f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Copies part of a texture into a small PNG next to the materials: point filter, repeat, no mips. Big sources are
    // halved step by step (each bilinear blit averages 2x2 texels) for a clean downsample. Drawn once: delete it to redo
    static Texture2D PixelCopy(string name, Texture source, Rect area, int size)
    {
        string path = RevampMats + "/" + name + ".png";
        var existing = Load<Texture2D>(path);
        if (existing != null)
            return existing;
        if (source == null || size < 1)
            return null;

        int w = Mathf.Max(size, Mathf.RoundToInt(area.width));
        var scale = new Vector2(area.width / source.width, area.height / source.height);
        var offset = new Vector2(area.x / source.width, area.y / source.height);
        var current = RenderTexture.GetTemporary(w, w, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, current, scale, offset);
        while (w / 2 >= size)
        {
            w /= 2;
            var next = RenderTexture.GetTemporary(w, w, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(current, next);
            RenderTexture.ReleaseTemporary(current);
            current = next;
        }
        if (w != size)
        {
            var next = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(current, next);
            RenderTexture.ReleaseTemporary(current);
            current = next;
        }

        var previous = RenderTexture.active;
        RenderTexture.active = current;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        t.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(current);
        File.WriteAllBytes(Path.GetFullPath(path), t.EncodeToPNG());
        Object.DestroyImmediate(t);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = false; importer.SaveAndReimport();
        return Load<Texture2D>(path);
    }

    // ---------------------------------------------------------------- lantern prefab

    // A small post lantern built like the Medieval Torch: a few primitives, a small point light (no shadows) and TorchFlicker.
    // The lamp hangs off an arm along local +X, so turn it to hang over the path
    static void MakeLantern()
    {
        var root = new GameObject("Lantern");
        var visual = Group(root.transform, "Visual");
        Part(PrimitiveType.Cube, "Foot", visual, new Vector3(0f, 0.05f, 0f), new Vector3(0.26f, 0.1f, 0.26f), stone);
        Part(PrimitiveType.Cube, "Post", visual, new Vector3(0f, 0.65f, 0f), new Vector3(0.1f, 1.2f, 0.1f), wood);
        Part(PrimitiveType.Cube, "Arm", visual, new Vector3(0.17f, 1.2f, 0f), new Vector3(0.4f, 0.06f, 0.06f), iron);
        Part(PrimitiveType.Cube, "Hook", visual, new Vector3(0.33f, 1.13f, 0f), new Vector3(0.03f, 0.1f, 0.03f), iron);
        Part(PrimitiveType.Cube, "Roof", visual, new Vector3(0.33f, 1.07f, 0f), new Vector3(0.14f, 0.04f, 0.14f), iron);
        Part(PrimitiveType.Cube, "Cap", visual, new Vector3(0.33f, 1.03f, 0f), new Vector3(0.24f, 0.05f, 0.24f), iron);
        var glow = Part(PrimitiveType.Cube, "Glow", visual, new Vector3(0.33f, 0.9f, 0f), new Vector3(0.15f, 0.21f, 0.15f), fire);
        glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Part(PrimitiveType.Cube, "Base", visual, new Vector3(0.33f, 0.77f, 0f), new Vector3(0.22f, 0.05f, 0.22f), iron);
        for (int i = 0; i < 4; i++)
        {
            float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
            Part(PrimitiveType.Cube, "Bar", visual, new Vector3(0.33f + sx * 0.095f, 0.9f, sz * 0.095f), new Vector3(0.03f, 0.23f, 0.03f), iron);
        }

        var lightObject = new GameObject("Light");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = new Vector3(0.33f, 0.9f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(1f, 0.72f, 0.4f); light.intensity = 1.6f; light.range = 4.5f; light.shadows = LightShadows.None;
        var flicker = root.AddComponent<TorchFlicker>();
        flicker.flame = light; flicker.flickerAmount = 0.18f; flicker.flickerSpeed = 6f;

        PrefabUtility.SaveAsPrefabAsset(root, LanternPath);
        Object.DestroyImmediate(root);
    }

    // ---------------------------------------------------------------- 0a. spawn

    // A PlayerSpawnPoint beside the time machine moves the player there as the scene loads. The MedievalPlayer
    // instance is moved too, so the Scene view matches play and the level works even without the spawn point
    static void PlayerSpawn(Scene scene)
    {
        var point = new GameObject("Player Spawn");
        point.transform.SetParent(generated, false);
        point.transform.position = SpawnAt;
        var spawnType = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(t => t.Name == "PlayerSpawnPoint");
        if (spawnType != null) point.AddComponent(spawnType);
        else notes.Add("TODO: no PlayerSpawnPoint component in the project; only the MedievalPlayer instance was moved.");

        var essentials = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MedievalEssentials");
        Transform player = null;
        if (essentials != null)
            player = FindChild(essentials.transform, "MedievalPlayer") ?? essentials.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.CompareTag("Player"));
        if (player != null) Pose(player, SpawnAt + Vector3.up * 0.1f);
        else notes.Add("No MedievalPlayer under MedievalEssentials: the spawn point alone places the player.");
    }

    // ---------------------------------------------------------------- 1. breach, steps, skeletons

    static void Breach()
    {
        var breach = Group(generated, "Breach");
        // A 3-wide walk-through gap at z -9.5..-6.5 replaces the jump stack
        var raised = FindNamed("Raised Wall (walk on top)");
        if (raised != null) { Pose(raised, new Vector3(-4f, 0f, -14.75f), new Vector3(2f, 2f, 10.5f)); Skin(raised, cliff); }
        else notes.Add("Missing \"Raised Wall (walk on top)\": the breach wasn't cut.");
        // Overlaps Castle Wall W by 0.25 so no gap opens at the top
        Block(breach, "Raised Wall N", new Vector3(-4f, 0f, -1.5f), new Vector3(2f, 2f, 10f), cliff);

        var vault = Section("8 Treasure Vault");
        MoveNamed("Step Crate W", new Vector3(12.4f, 0f, 17f), vault);
        MoveNamed("Step Stone W", new Vector3(14.2f, 0f, 17f), vault);
        // A low perch that makes the skeleton stomp easy to learn
        MoveNamed("Step Crate E", new Vector3(10.5f, 0f, -10.4f), null);
        // Fallen masonry at the breach's north-east lip
        var rubble = MoveNamed("Step Stone E", new Vector3(-2.4f, 0f, -5.8f), null);
        if (rubble != null) { Pose(rubble, rubble.position, new Vector3(1.2f, 0.9f, 1.2f)); Skin(rubble, cliff); }

        // Pulled away from the lever and gate, and gentler
        Skeleton(FindNamed("Skeleton (gate)"), new Vector3(10f, 0f, -5f), new Vector3(5f, 0f, -5f), new Vector3(15f, 0f, -5f), 4f, 7f);
        Skeleton(FindNamed("Skeleton (yard)"), new Vector3(10.5f, 0f, -12f), new Vector3(5f, 0f, -12f), new Vector3(16f, 0f, -12f), 6f, 9f);
    }

    static void Skeleton(Transform t, Vector3 at, Vector3 a, Vector3 b, float aggro, float leash)
    {
        if (t == null) { notes.Add("Missing a skeleton for " + at + "."); return; }
        Pose(t, at);
        var enemy = t.GetComponent<SkeletonEnemy>();
        if (enemy == null) return;
        // Patrol points are world positions, so they move with it
        Set(enemy, "patrolA", p => p.vector3Value = a);
        Set(enemy, "patrolB", p => p.vector3Value = b);
        Set(enemy, "aggroRadius", p => p.floatValue = aggro);
        Set(enemy, "leashRadius", p => p.floatValue = leash);
        Set(enemy, "killTimeReward", p => p.floatValue = MedievalStory.SkeletonKillReward);
    }

    // ---------------------------------------------------------------- 2-3. Great Hall, Treasure Vault, chest

    static void Vault()
    {
        var hall = level.Find("6 Trap Hall") ?? level.Find("6 Great Hall");
        if (hall != null) Rename(hall.gameObject, "6 Great Hall");
        var vault = Section("8 Treasure Vault");
        var g = Group(generated, "Vault");

        // All 9 traps move 8.25 north into the vault (z 13..18.75 is the old hall shifted), keeping x and phase
        var traps = level.GetComponentsInChildren<SpikeTrap>(true).Select(c => (Component)c)
            .Concat(level.GetComponentsInChildren<SwingingAxe>(true))
            .Select(c => InstanceRoot(c.gameObject).transform)
            .Where(t => !t.IsChildOf(generated)).Distinct().ToList();
        foreach (var trap in traps)
        {
            Vector3 p = trap.position;
            if (p.z < 12f) p.z += 8.25f;
            Reparent(trap, vault);
            Pose(trap, p);
        }

        // Hall Wall N becomes a low battlement (still above jump height) with a west door, a tall west tower and an east door
        var hallWall = FindNamed("Hall Wall N");
        if (hallWall != null) Pose(hallWall, new Vector3(-0.25f, 0f, 11.75f), new Vector3(27.5f, 2f, 2.5f));
        else notes.Add("Missing \"Hall Wall N\": the vault doors weren't opened.");
        Block(g, "Hall Wall N (W tower)", new Vector3(-18.5f, 0f, 11.75f), new Vector3(3f, 6f, 2.5f), null);
        // Low, so it doesn't hide the chest dais from the hall
        Block(g, "Hall Wall N (E)", new Vector3(18.5f, 0f, 11.75f), new Vector3(3f, 2f, 2.5f), null);
        Block(g, "Vault Wall N", new Vector3(0f, 0f, 19.375f), new Vector3(40f, 6f, 1.25f), vaultStone);
        // Top at 2.0: only reachable by the crate and stone stair
        Block(g, "Chest Dais", new Vector3(18.25f, 0f, 17f), new Vector3(3.5f, 2f, 3f), archStone);

        // Torches that sat in the new doorways
        MoveTorch(new Vector3(14f, 0f, 10.2f), new Vector3(12.6f, 0f, 10.2f));
        MoveTorch(new Vector3(-14f, 0f, 10.2f), new Vector3(-13.4f, 0f, 10.2f));

        var chest = level.GetComponentsInChildren<TreasureChest>(true).FirstOrDefault(c => !c.transform.IsChildOf(generated));
        if (chest != null)
        {
            Transform t = InstanceRoot(chest.gameObject).transform;
            Reparent(t, vault);
            Pose(t, new Vector3(18.4f, 2f, 17.2f));
            Set(chest, "timeBonus", p => p.floatValue = MedievalStory.ChestTimeBonus);
            SetStrings(chest, "openedLines", MedievalStory.ChestOpenedLines);
        }
        else notes.Add("No TreasureChest found to move into the vault.");

        // One-way vault gate: its lever is inside, out of reach from the hall, so the chest can't skip the traps
        var gateObject = Spawn(Prefabs + "/Gate.prefab", g, "Vault Gate", new Vector3(15.25f, 0f, 11.75f));
        var leverObject = Spawn(Prefabs + "/Lever.prefab", g, "Lever (vault)", new Vector3(12.6f, 0f, 13.4f));
        var gate = gateObject != null ? gateObject.GetComponent<Gate>() : null;
        var lever = leverObject != null ? leverObject.GetComponent<Lever>() : null;
        if (lever != null)
        {
            if (gate != null) Set(lever, "gate", p => p.objectReferenceValue = gate);
            Set(lever, "opensName", p => p.stringValue = MedievalStory.VaultLeverName);
            SetStrings(lever, "pulledLines", MedievalStory.VaultPulledLines);
        }

        var skeleton = Spawn(Prefabs + "/Skeleton.prefab", g, "Skeleton (vault)", new Vector3(12f, 0f, 14.6f));
        // Patrols under the stair; the leash keeps it out of the door tunnels
        if (skeleton != null) Skeleton(skeleton.transform, new Vector3(12f, 0f, 14.6f), new Vector3(8.5f, 0f, 14.6f), new Vector3(15.5f, 0f, 14.6f), 5f, 6f);

        Trigger(g, "Vault Hint", new Vector3(-15.5f, 1.5f, 11.75f), new Vector3(3f, 3f, 2.5f), MedievalStory.VaultHintLines);
        var doorHint = Trigger(g, "Vault Door Hint", new Vector3(15.25f, 1.5f, 9.5f), new Vector3(3.5f, 3f, 1.5f), MedievalStory.VaultDoorHintLines);
        // Leaving through the opened vault gate crosses this zone, so the "barred" hint switches off once the lever is pulled
        if (lever != null && doorHint != null)
        {
            UnityEventTools.AddBoolPersistentListener(lever.onPulled, new UnityAction<bool>(doorHint.gameObject.SetActive), false);
            Mark(lever);
        }

        // Posts on the battlement under each axe beam's south end (the beam's north end sits in Vault Wall N)
        foreach (var axe in level.GetComponentsInChildren<SwingingAxe>(true).Where(a => !a.transform.IsChildOf(generated)))
            Part(PrimitiveType.Cube, "Axe Beam Post", g, new Vector3(axe.transform.position.x, 3.05f, 12.8f), new Vector3(0.25f, 2.1f, 0.25f), wood);
        // Dark grates mark every spike pad, even while the spikes are down
        foreach (var spikes in level.GetComponentsInChildren<SpikeTrap>(true).Where(s => !s.transform.IsChildOf(generated)))
        {
            Vector3 p = spikes.transform.position;
            var grate = Part(PrimitiveType.Cube, "Spike Grate", g, new Vector3(p.x, 0.165f, p.z), new Vector3(1.5f, 0.02f, 1.5f), trapPlate);
            grate.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        MakeStatic(g, skip: new[] { gateObject, leverObject, skeleton });
    }

    static StoryTrigger Trigger(Transform parent, string name, Vector3 centre, Vector3 size, string[] lines)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = size;
        var story = go.AddComponent<StoryTrigger>();
        SetStrings(story, "lines", lines);
        Set(story, "objective", p => p.stringValue = "");
        // The vault is optional and reachable after the crystal; its lines queue behind whatever is being said
        Set(story, "fireAfterCrystal", p => p.boolValue = true);
        Set(story, "waitForCurrentDialogue", p => p.boolValue = true);
        return story;
    }

    // ---------------------------------------------------------------- story text

    static void Texts(Scene scene)
    {
        var courtyard = FindNamed("Enter Courtyard");
        if (courtyard != null && courtyard.TryGetComponent(out StoryTrigger enter))
        {
            SetStrings(enter, "lines", MedievalStory.EnterCourtyardLines);
            Set(enter, "objective", p => p.stringValue = MedievalStory.EnterCourtyardObjective);
            // Reached a few seconds after the intro, so it waits instead of cutting it off
            Set(enter, "waitForCurrentDialogue", p => p.boolValue = true);
        }
        else notes.Add("Missing the \"Enter Courtyard\" StoryTrigger.");

        var gateHint = FindNamed("Gate Hint");
        if (gateHint != null && gateHint.TryGetComponent(out StoryTrigger hint))
            SetStrings(hint, "lines", MedievalStory.GateHintLines);

        var hallTrigger = FindNamed("Enter Trap Hall") ?? FindNamed("Enter Great Hall");
        if (hallTrigger != null && hallTrigger.TryGetComponent(out StoryTrigger hallStory))
        {
            Rename(hallTrigger.gameObject, "Enter Great Hall");
            SetStrings(hallStory, "lines", MedievalStory.EnterGreatHallLines);
            Set(hallStory, "objective", p => p.stringValue = MedievalStory.EnterGreatHallObjective);
        }
        else notes.Add("Missing the \"Enter Trap Hall\" StoryTrigger.");

        var mainLever = level.GetComponentsInChildren<Lever>(true).FirstOrDefault(l => !l.transform.IsChildOf(generated));
        if (mainLever != null) SetStrings(mainLever, "pulledLines", MedievalStory.GatePulledLines);

        var director = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MedievalLevel>(true)).FirstOrDefault();
        if (director != null)
        {
            SetStrings(director, "crystalLines", MedievalStory.CrystalLines);
            SetStrings(director, "repairedLines", MedievalStory.RepairedLines);
        }
        else notes.Add("No LevelDirector (MedievalLevel) found for the crystal lines.");
    }

    // ---------------------------------------------------------------- 11. towers, battlements, gate arch

    static void Castle(Scene scene)
    {
        var castle = Group(generated, "Castle");
        Renderer wallW = MeshOf(FindNamed("Castle Wall W")), wallE = MeshOf(FindNamed("Castle Wall E"));

        // Towers on the curtain wall (z 3.25..4.75), a hair bigger than the walls they meet so no faces z-fight
        const float z0 = 3.13f, z1 = 4.87f, towerHeight = 7.5f, hair = 0.06f;
        Tower(castle, "Tower (west end)", -20f - hair, -18f + hair, z0, z1, towerHeight);
        Tower(castle, "Tower (rampart)", -5f - hair, -3f + hair, z0, z1, towerHeight);
        Tower(castle, "Gate Tower W", 8f - hair, 9.5f + hair, z0, z1, towerHeight);
        Tower(castle, "Gate Tower E", 13f - hair, 14.5f + hair, z0, z1, towerHeight);
        Tower(castle, "Tower (east end)", 18f - hair, 20f + hair, z0, z1, towerHeight);
        // The gate towers' faces sit where two torches stood; nudge those out in front
        MoveTorch(new Vector3(8.8f, 0f, 3f), new Vector3(8.8f, 0f, 2.85f));
        MoveTorch(new Vector3(13.7f, 0f, 3f), new Vector3(13.7f, 0f, 2.85f));

        // Square-wave merlons along both edges of the curtain wall top, between the towers
        foreach (var (start, to, wall) in new[] { (-18f, -5f, wallW), (-3f, 8f, wallW), (14.5f, 18f, wallE) })
        {
            Merlons(castle, "Battlement", new Vector3(start, 6f, 3.45f), new Vector3(to, 6f, 3.45f), 0.4f, 0.6f, 0.75f, 0.75f, wall);
            Merlons(castle, "Battlement", new Vector3(start, 6f, 4.55f), new Vector3(to, 6f, 4.55f), 0.4f, 0.6f, 0.75f, 0.75f, wall);
        }

        GateArch(castle);

        // The low hall battlement: short merlons every other unit on its hall edge, kept under 2.6 so the vault stays readable
        Renderer hallWall = MeshOf(FindNamed("Hall Wall N"));
        Merlons(castle, "Hall Battlement", new Vector3(-14f, 2f, 10.7f), new Vector3(13.5f, 2f, 10.7f), 0.4f, 0.5f, 1f, 1f, hallWall);
        var east = FindChild(generated, "Hall Wall N (E)");
        Merlons(castle, "Hall Battlement", new Vector3(17f, 2f, 10.7f), new Vector3(20f, 2f, 10.7f), 0.4f, 0.5f, 1f, 1f, MeshOf(east));
        var westTower = FindChild(generated, "Hall Wall N (W tower)");
        Ring(castle, "Tower Battlement", -20f, -17f, 10.5f, 13f, 6f, 0.6f, 0.5f, 0.5f, 0.4f, MeshOf(westTower));
        Merlons(castle, "Vault Battlement", new Vector3(-20f, 6f, 18.95f), new Vector3(20f, 6f, 18.95f), 0.4f, 0.6f, 0.75f, 0.75f, null);

        Border(scene, castle);
        MakeStatic(castle);
    }

    // A square tower: body, a corbelled ledge, a ring of merlons and an arrow slit. One box on the root covers the body,
    // so OccluderFader cuts the whole tower when it hides the player
    static Transform Tower(Transform parent, string name, float x0, float x1, float z0, float z1, float height)
    {
        float w = x1 - x0, d = z1 - z0;
        const float ledge = 0.18f;
        var root = Group(parent, name);
        root.localPosition = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
        var box = root.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(w, height, d); box.center = new Vector3(0f, height * 0.5f, 0f);
        Part(PrimitiveType.Cube, "Body", root, new Vector3(0f, height * 0.5f, 0f), new Vector3(w, height, d), towerStone);
        Part(PrimitiveType.Cube, "Corbels", root, new Vector3(0f, height - 0.2f, 0f), new Vector3(w + 2f * ledge, 0.4f, d + 2f * ledge), battlement);
        Ring(root, "Merlons", -w * 0.5f - ledge, w * 0.5f + ledge, -d * 0.5f - ledge, d * 0.5f + ledge, height, 0.6f, 0.5f, 0.5f, 0.4f, null);
        Part(PrimitiveType.Cube, "Arrow Slit", root, new Vector3(0f, height * 0.6f, -d * 0.5f - 0.01f), new Vector3(0.14f, 0.7f, 0.04f), iron);
        return root;
    }

    // A row of merlons on a wall top (the "square wave"), evenly centred so both ends are merlons. One box covers the row,
    // so when it hides the player OccluderFader cuts the merlons; "wall" also cuts them whenever that wall is cut
    static Transform Merlons(Transform parent, string name, Vector3 start, Vector3 to, float depth, float height, float merlon, float gap, Renderer wall)
    {
        bool alongX = Mathf.Abs(to.x - start.x) >= Mathf.Abs(to.z - start.z);
        float length = alongX ? Mathf.Abs(to.x - start.x) : Mathf.Abs(to.z - start.z);
        if (length < merlon * 0.5f)
            return null;
        var row = Group(parent, name);
        row.localPosition = (start + to) * 0.5f + Vector3.up * (height * 0.5f);
        var box = row.gameObject.AddComponent<BoxCollider>();
        box.size = alongX ? new Vector3(length, height, depth) : new Vector3(depth, height, length);

        Vector3 axis = alongX ? Vector3.right : Vector3.forward;
        int count = Mathf.Max(1, Mathf.FloorToInt((length + gap) / (merlon + gap)));
        float piece = Mathf.Min(merlon, length);
        float used = count * piece + (count - 1) * gap;
        float first = -used * 0.5f + piece * 0.5f;
        for (int i = 0; i < count; i++)
        {
            var m = Part(PrimitiveType.Cube, "Merlon", row, axis * (first + i * (piece + gap)), alongX ? new Vector3(piece, height, depth) : new Vector3(depth, height, piece), battlement);
            if (wall != null) Link(wall, m.GetComponent<Renderer>());
        }
        return row;
    }

    // Merlons round all four edges of a rectangle (tower tops), corners included
    static void Ring(Transform parent, string name, float x0, float x1, float z0, float z1, float y, float height, float merlon, float gap, float depth, Renderer wall)
    {
        var ring = Group(parent, name);
        Merlons(ring, "South", new Vector3(x0, y, z0 + depth * 0.5f), new Vector3(x1, y, z0 + depth * 0.5f), depth, height, merlon, gap, wall);
        Merlons(ring, "North", new Vector3(x0, y, z1 - depth * 0.5f), new Vector3(x1, y, z1 - depth * 0.5f), depth, height, merlon, gap, wall);
        Merlons(ring, "West", new Vector3(x0 + depth * 0.5f, y, z0 + depth + gap), new Vector3(x0 + depth * 0.5f, y, z1 - depth - gap), depth, height, merlon, gap, wall);
        Merlons(ring, "East", new Vector3(x1 - depth * 0.5f, y, z0 + depth + gap), new Vector3(x1 - depth * 0.5f, y, z1 - depth - gap), depth, height, merlon, gap, wall);
    }

    // A stone arch filling the wall above the main gate. The portcullis (2.5 high) slides down beneath it; the arch's
    // underside steps up from 2.6 at the sides to 3.8 at the keystone. Its box starts at 2.9, above any jump, and parents
    // every piece so OccluderFader cuts the arch with the wall when the player is in the hall behind it
    static void GateArch(Transform castle)
    {
        var mainGate = level.GetComponentsInChildren<Gate>(true).FirstOrDefault(gt => !gt.transform.IsChildOf(generated));
        float cx = mainGate != null ? mainGate.transform.position.x : 11.25f;
        const float half = 1.75f, depth = 1.5f, top = 6f, spring = 2.6f, rise = 1.2f;
        var arch = Group(castle, "Main Gate Arch");
        arch.localPosition = new Vector3(cx, 0f, 4f);
        var box = arch.gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0f, (2.9f + top) * 0.5f, 0f); box.size = new Vector3(half * 2f, top - 2.9f, depth);

        float Under(float x) => Mathf.Round((spring + rise * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / half) * (x / half)))) * 16f) / 16f;
        for (int i = 0; i < 7; i++)
        {
            float x = -1.5f + i * 0.5f, under = Under(x);
            Part(PrimitiveType.Cube, "Arch Fill", arch, new Vector3(x, (under + top) * 0.5f, 0f), new Vector3(0.5f, top - under, depth), battlement);
            // Voussoirs stand slightly proud of both faces so the curve reads from the camera
            Part(PrimitiveType.Cube, "Voussoir", arch, new Vector3(x, under + 0.22f, -depth * 0.5f - 0.06f), new Vector3(0.46f, 0.44f, 0.14f), archStone);
            Part(PrimitiveType.Cube, "Voussoir", arch, new Vector3(x, under + 0.22f, depth * 0.5f + 0.06f), new Vector3(0.46f, 0.44f, 0.14f), archStone);
        }
        Part(PrimitiveType.Cube, "Keystone", arch, new Vector3(0f, Under(0f) + 0.32f, -depth * 0.5f - 0.1f), new Vector3(0.42f, 0.64f, 0.22f), archStone);
        Part(PrimitiveType.Cube, "Springer W", arch, new Vector3(-half - 0.1f, spring - 0.05f, 0f), new Vector3(0.5f, 0.2f, depth + 0.3f), archStone);
        Part(PrimitiveType.Cube, "Springer E", arch, new Vector3(half + 0.1f, spring - 0.05f, 0f), new Vector3(0.5f, 0.2f, depth + 0.3f), archStone);
        Merlons(arch, "Battlement", new Vector3(-half, top, -0.55f), new Vector3(half, top, -0.55f), 0.4f, 0.6f, 0.75f, 0.75f, null);
        Merlons(arch, "Battlement", new Vector3(-half, top, 0.55f), new Vector3(half, top, 0.55f), 0.4f, 0.6f, 0.75f, 0.75f, null);
    }

    // The level's outer wall is LevelBorder's (built at load, not saved), so its battlements and corner towers are placed
    // from the floor's painted bounds and the border's thickness and height. The rows' boxes let OccluderFader cut them
    static void Border(Scene scene, Transform castle)
    {
        float xMin = -20f, xMax = 20f, zMin = -20f, zMax = 20f, t = 3f, h = 6f, baseY = 0.1f;
        var border = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelBorder>(true)).FirstOrDefault();
        if (border != null)
        {
            t = border.wallThickness;
            h = border.wallHeight;
            // Same floor LevelBorder uses: its own field, else the scene's first tilemap that isn't generated
            var map = border.floor != null ? border.floor : scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Tilemap>(true))
                .FirstOrDefault(m => (m.gameObject.hideFlags & HideFlags.DontSave) == 0);
            if (map != null && map.cellBounds.size.x > 0 && map.cellBounds.size.x < 200 && map.cellBounds.size.y < 200)
            {
                BoundsInt b = map.cellBounds;
                Vector3 a = map.CellToWorld(new Vector3Int(b.xMin, b.yMin, 0)), c = map.CellToWorld(new Vector3Int(b.xMax, b.yMax, 0));
                xMin = Mathf.Min(a.x, c.x); xMax = Mathf.Max(a.x, c.x); zMin = Mathf.Min(a.z, c.z); zMax = Mathf.Max(a.z, c.z);
                baseY = map.transform.position.y;
            }
        }
        else notes.Add("No LevelBorder found: border battlements use the default 40x40 bounds.");

        var ring = Group(castle, "Border Battlements");
        float top = baseY + h;
        Merlons(ring, "North", new Vector3(xMin, top, zMax + 0.2f), new Vector3(xMax, top, zMax + 0.2f), 0.4f, 0.6f, 0.75f, 0.75f, null);
        Merlons(ring, "North Outer", new Vector3(xMin, top, zMax + t - 0.2f), new Vector3(xMax, top, zMax + t - 0.2f), 0.4f, 0.6f, 0.75f, 0.75f, null);
        Merlons(ring, "South", new Vector3(xMin, top, zMin - 0.2f), new Vector3(xMax, top, zMin - 0.2f), 0.4f, 0.6f, 0.75f, 0.75f, null);
        Merlons(ring, "West", new Vector3(xMin - 0.2f, top, zMin), new Vector3(xMin - 0.2f, top, zMax), 0.4f, 0.6f, 0.75f, 0.75f, null);
        Merlons(ring, "East", new Vector3(xMax + 0.2f, top, zMin), new Vector3(xMax + 0.2f, top, zMax), 0.4f, 0.6f, 0.75f, 0.75f, null);
        // Corner towers, a hair wider than the border blocks so their faces don't z-fight
        Tower(castle, "Corner Tower NW", xMin - t - 0.1f, xMin + 0.1f, zMax - 0.1f, zMax + t + 0.1f, h + 2.5f);
        Tower(castle, "Corner Tower NE", xMax - 0.1f, xMax + t + 0.1f, zMax - 0.1f, zMax + t + 0.1f, h + 2.5f);
        Tower(castle, "Corner Tower SW", xMin - t - 0.1f, xMin + 0.1f, zMin - t - 0.1f, zMin + 0.1f, h + 2.5f);
        Tower(castle, "Corner Tower SE", xMax - 0.1f, xMax + t + 0.1f, zMin - t - 0.1f, zMin + 0.1f, h + 2.5f);
    }

    // ---------------------------------------------------------------- 7/12. floors: cobble roads, mud, dirt, flagstones

    static void Floors()
    {
        var floor = Group(generated, "Floor");
        var halls = new FloorPaint(); var vaultPaint = new FloorPaint(); var dirtPaint = new FloorPaint(); var mudPaint = new FloorPaint(); var road = new FloorPaint();

        // Great Hall flagstones, through both vault doors
        halls.Rect(-20f, 4.75f, 20f, 10.5f);
        halls.Rect(-17f, 10.5f, -14f, 13f);
        halls.Rect(13.5f, 10.5f, 17f, 13f);
        // Dungeon floor in the vault
        vaultPaint.Rect(-20f, 13f, 20f, 18.75f);

        // Trodden dirt round the village buildings, the stable, the training yard and the guard fire
        dirtPaint.Blob(-15f, -3.2f, 3f, 1); dirtPaint.Blob(-10f, -3.5f, 2.2f, 2); dirtPaint.Blob(-17.5f, -7f, 2.4f, 3);
        dirtPaint.Blob(-9.5f, -15f, 1.7f, 4); dirtPaint.Blob(9.5f, -16.5f, 3.2f, 5); dirtPaint.Blob(16.6f, -9.6f, 2.4f, 6); dirtPaint.Blob(14.4f, -7.6f, 1.6f, 7);
        // Mud puddles, kept beside the road rather than on it
        mudPaint.Blob(-13.8f, -9.6f, 1.4f, 11); mudPaint.Blob(-7.6f, -16.4f, 1.2f, 12); mudPaint.Blob(-16.4f, -12.4f, 1f, 13);
        mudPaint.Blob(7.6f, -8.6f, 1.8f, 14); mudPaint.Blob(2.6f, -13.2f, 1.5f, 15); mudPaint.Blob(15.6f, -14.6f, 1.9f, 16); mudPaint.Blob(-17.8f, -6.8f, 1.1f, 17);

        // The cobble road reads as the way to go: time machine, breach, lever, gate
        road.Blob(-15f, -16.5f, 2.8f, 21);
        road.Path(1.6f, new Vector2(-13.4f, -14.8f), new Vector2(-12f, -13f), new Vector2(-11f, -12.4f), new Vector2(-9f, -11f), new Vector2(-7f, -9.5f),
            new Vector2(-5.5f, -8f), new Vector2(-2.5f, -8f), new Vector2(-1f, -6f), new Vector2(2f, -3f), new Vector2(4.5f, 0f),
            new Vector2(6.5f, 1.2f), new Vector2(11.25f, 1.4f), new Vector2(11.25f, 3.25f));
        road.Rect(9.5f, 3.25f, 13f, 4.75f);

        // Stacked a little apart so they never z-fight with the tilemap (y 0.11) or each other
        halls.Build(floor, "Hall Flagstones", flagstone, 0.12f);
        vaultPaint.Build(floor, "Vault Floor", vaultFloor, 0.12f);
        dirtPaint.Build(floor, "Dirt", dirt, 0.13f);
        mudPaint.Build(floor, "Mud", mud, 0.14f);
        road.Build(floor, "Cobble Road", cobble, 0.15f);
        MakeStatic(floor);
    }

    // Cells (half a unit) painted onto a flat mesh, so patches have stepped pixel edges. Merged into one quad per row run
    class FloorPaint
    {
        const float Cell = 0.5f;
        readonly HashSet<Vector2Int> cells = new HashSet<Vector2Int>();

        public void Rect(float x0, float z0, float x1, float z1)
        {
            for (int i = Mathf.FloorToInt(x0 / Cell); i < Mathf.CeilToInt(x1 / Cell); i++)
                for (int j = Mathf.FloorToInt(z0 / Cell); j < Mathf.CeilToInt(z1 / Cell); j++)
                    cells.Add(new Vector2Int(i, j));
        }

        // A rough round patch: the edge wobbles in 2x2-cell chunks, never single cells
        public void Blob(float cx, float cz, float r, int seed)
        {
            for (int i = Mathf.FloorToInt((cx - r * 1.3f) / Cell); i <= Mathf.CeilToInt((cx + r * 1.3f) / Cell); i++)
                for (int j = Mathf.FloorToInt((cz - r * 1.3f) / Cell); j <= Mathf.CeilToInt((cz + r * 1.3f) / Cell); j++)
                {
                    float x = (i + 0.5f) * Cell - cx, z = (j + 0.5f) * Cell - cz;
                    float edge = r * (0.8f + 0.4f * Hash(i >> 1, j >> 1, seed));
                    if (x * x + z * z <= edge * edge) cells.Add(new Vector2Int(i, j));
                }
        }

        public void Path(float width, params Vector2[] points)
        {
            for (int k = 0; k + 1 < points.Length; k++)
            {
                Vector2 a = points[k], b = points[k + 1];
                float reach = width * 0.5f + 0.3f;
                for (int i = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach) / Cell); i <= Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach) / Cell); i++)
                    for (int j = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach) / Cell); j <= Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach) / Cell); j++)
                    {
                        var p = new Vector2((i + 0.5f) * Cell, (j + 0.5f) * Cell);
                        Vector2 ab = b - a;
                        float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                        if (Vector2.Distance(p, a + ab * t) <= width * 0.5f + 0.3f * Hash(i >> 1, j >> 1, 31)) cells.Add(new Vector2Int(i, j));
                    }
            }
        }

        public void Build(Transform parent, string name, Material material, float y)
        {
            if (cells.Count == 0)
                return;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            foreach (var row in cells.GroupBy(c => c.y))
            {
                var xs = row.Select(c => c.x).OrderBy(x => x).ToList();
                int runStart = xs[0];
                for (int k = 1; k <= xs.Count; k++)
                {
                    if (k < xs.Count && xs[k] == xs[k - 1] + 1)
                        continue;
                    float x0 = runStart * Cell, x1 = (xs[k - 1] + 1) * Cell, z0 = row.Key * Cell, z1 = (row.Key + 1) * Cell;
                    int v = vertices.Count;
                    vertices.Add(new Vector3(x0, y, z0)); vertices.Add(new Vector3(x0, y, z1)); vertices.Add(new Vector3(x1, y, z1)); vertices.Add(new Vector3(x1, y, z0));
                    for (int n = 0; n < 4; n++) { normals.Add(Vector3.up); uvs.Add(new Vector2(vertices[v + n].x, vertices[v + n].z)); }
                    // Clockwise seen from above, so the face points up
                    triangles.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
                    if (k < xs.Count) runStart = xs[k];
                }
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    // ---------------------------------------------------------------- 10. lanterns

    static void Lights()
    {
        var lights = Group(generated, "Lanterns");
        // Along the route (each arm hangs toward the road)
        Lantern(lights, -9.6f, -13.6f, -10.6f, -12.2f);
        Lantern(lights, -8.6f, -8.8f, -7.8f, -10f);
        Lantern(lights, -0.6f, -3.4f, 0.6f, -4.4f);
        Lantern(lights, 2.9f, 1.2f, 4.4f, 0.4f);
        // Village and courtyard
        Lantern(lights, -12.8f, -5.8f, -12.8f, -7f);
        Lantern(lights, -6.4f, -1.6f, -7.4f, -1.6f);
        Lantern(lights, 16.2f, -1.8f, 15.2f, -1.8f);
        // Treasure vault: the run along the back wall, and the vault lever
        Lantern(lights, -14f, 18.4f, -14f, 17f);
        Lantern(lights, -4f, 18.4f, -4f, 17f);
        Lantern(lights, 12f, 18.4f, 12f, 17.2f);
        Lantern(lights, 11.3f, 13.3f, 12.6f, 13.4f);
    }

    static void Lantern(Transform parent, float x, float z, float towardX, float towardZ)
    {
        var go = Spawn(LanternPath, parent, "Lantern", new Vector3(x, 0f, z));
        if (go == null) return;
        // The lamp hangs off local +X
        float yaw = Mathf.Atan2(towardX - x, towardZ - z) * Mathf.Rad2Deg - 90f;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Mark(go.transform);
    }

    // ---------------------------------------------------------------- signposting

    static void Signs()
    {
        var signs = Group(generated, "Signposts");
        // Route: spawn to the breach, the breach to the lever, then the hall to the crystal
        Signpost(signs, "Sign to Breach", -13f, -11.9f, -4f, -8f, false);
        Signpost(signs, "Sign to Lever", -0.6f, -9.4f, 6.5f, 2.7f, false);
        Signpost(signs, "Sign to Crystal", 9.2f, 9.4f, -17f, 7.5f, false);
        // Optional vault: gold-tipped, pointing into the west door, and inside to the shortcut out
        Signpost(signs, "Sign to Vault", -12.6f, 9.2f, -15.5f, 11.75f, true);
        Signpost(signs, "Sign to Vault Exit", 14.6f, 13.5f, 15.25f, 11.75f, true);
        MakeStatic(signs);
    }

    // A post with a flat arrow board, pointing along the ground so it reads from the high camera
    static void Signpost(Transform parent, string name, float x, float z, float towardX, float towardZ, bool vault)
    {
        var root = Group(parent, name);
        root.localPosition = new Vector3(x, 0f, z);
        root.localRotation = Quaternion.Euler(0f, Mathf.Atan2(towardX - x, towardZ - z) * Mathf.Rad2Deg, 0f);
        var box = root.gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0f, 0.7f, 0f); box.size = new Vector3(0.3f, 1.4f, 0.3f);
        Part(PrimitiveType.Cube, "Post", root, new Vector3(0f, 0.65f, 0f), new Vector3(0.12f, 1.3f, 0.12f), wood);
        Part(PrimitiveType.Cube, "Board", root, new Vector3(0f, 1.22f, 0.3f), new Vector3(0.26f, 0.07f, 0.66f), wood);
        var tip = Part(PrimitiveType.Cube, "Arrow Tip", root, new Vector3(0f, 1.22f, 0.66f), new Vector3(0.28f, 0.07f, 0.28f), vault ? gold : iron);
        tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
    }

    // ---------------------------------------------------------------- 5. props (Slavic kit)

    static void Props()
    {
        var props = Group(generated, "Props");

        // A. Arrival village (west yard). Tall pieces only north of or beside the paths
        var a = Group(props, "A Arrival Village");
        Prop(a, "EA03_Town_House_Comp_01a_PRE", -15.5f, -0.5f, solid: Solid.Tall, height: 6f, width: 6f, depth: 5f);
        Prop(a, "EA03_Village_OutBuilding_Shed_03b_PRE", -8.5f, 0.5f, solid: Solid.Tall, height: 4f, width: 3.5f, depth: 3f);
        Prop(a, "EA03_Village_AnimalBreeding_Hovel_01a_PRE", -18.5f, -7f, solid: Solid.Tall, height: 3.5f, width: 3f, depth: 4f);
        // Paddock east of the hovel
        Prop(a, "EA03_Fence_Plank_02a_PRE", -16f, -9f, length: 2f, height: 1.2f);
        Prop(a, "EA03_Fence_Plank_02b_PRE", -15f, -8f, length: 2f, alongZ: true, height: 1.2f);
        Prop(a, "EA03_Fence_Plank_02a_PRE", -15f, -6f, length: 2f, alongZ: true, height: 1.2f);
        Prop(a, "EA03_Fence_Plank_02b_PRE", -16f, -5f, length: 2f, height: 1.2f);
        // Vegetable patch
        Prop(a, "EA03_Terrain_Field_2x2_Seam_01a_PRE", -10.9f, -3.5f, solid: Solid.None, height: 0.4f, width: 1.8f, depth: 2f);
        Prop(a, "EA03_Terrain_Field_2x2_Seam_01a_PRE", -9.1f, -3.5f, solid: Solid.None, height: 0.4f, width: 1.8f, depth: 2f);
        Prop(a, "EA03_Fence_VegetablePatch_02a_PRE", -10f, -3.5f, height: 0.8f, width: 4.2f, depth: 2.6f);
        // Chopping corner and bales, low and south of the route
        Prop(a, "EA03_Prop_Tool_Stump_02a_PRE", -9.8f, -15f, height: 0.7f);
        Prop(a, "EA03_Prop_Brushwood_03a_PRE", -9f, -15.5f, height: 0.6f, width: 1.4f);
        Prop(a, "EA03_Prop_Container_Bale_01a_PRE", -10.5f, -17.6f, height: 0.8f);
        Prop(a, "EA03_Prop_Container_Bale_01a_PRE", -11.8f, -18.4f, turn: 30f, height: 0.8f);
        // Points the way to the breach, north of the road
        Prop(a, "EA03_Prop_Sign_Chapel_01_PRE", -7.8f, -6.6f, height: 2f);
        // Trees on the west edge only, never in front of the spawn
        Prop(a, "EA03_Nature_Tree_01b_PRE", -19.3f, -13f, solid: Solid.Trunk, height: 5f, width: 2.6f, depth: 2.6f);
        Prop(a, "EA03_Nature_Tree_02b_PRE", -19.3f, -18.8f, solid: Solid.Trunk, height: 4f, width: 2.4f, depth: 2.4f);
        Prop(a, "EA03_Nature_Bush_01a_PRE", -16f, -19.3f, height: 0.7f, width: 1.4f);
        Prop(a, "EA03_Nature_Bush_02a_PRE", -8f, -19.3f, height: 0.7f, width: 1.4f);
        Prop(a, "EA03_Nature_Bush_03a_PRE", -6f, -19.3f, height: 0.7f, width: 1.4f);
        Prop(a, "EA03_Nature_Bush_01c_PRE", -12.7f, -17.7f, height: 0.7f, width: 1.2f);

        // B. Breach: low rubble on the west lip, moss on the wall ends
        var b = Group(props, "B Breach");
        Prop(b, "EA03_Environment_Rock_Mini_CatHead_01a_PRE", -5.6f, -9.9f, height: 0.6f);
        Prop(b, "EA03_Environment_Rock_CatHead_01b_PRE", -5.6f, -6.1f, height: 0.8f);
        // A hair above the wall top so a flat moss mesh doesn't z-fight with it
        Prop(b, "EA03_Environment_Moss_01_PRE", -4f, -9.9f, solid: Solid.None, width: 2f, depth: 0.8f, y: 2.02f);
        Prop(b, "EA03_Environment_Moss_01_PRE", -4f, -6.1f, solid: Solid.None, width: 2f, depth: 0.8f, y: 2.02f);

        // C. Courtyard (bailey). Nothing in the lever and gate zone (x 4..14, z -1..3.25); only low pieces in the south
        var c = Group(props, "C Courtyard");
        Prop(c, "EA03_Prop_Stand_Sheet_01a_PRE", -1.3f, 2.1f, height: 2.6f, width: 2.2f, depth: 1.6f);
        Prop(c, "EA03_Prop_Container_Basket_01a_PRE", -0.6f, 0.8f, height: 0.5f);
        Prop(c, "EA03_Prop_Vegetable_Basket_01a_PRE", 0f, 0.95f, height: 0.5f);
        Prop(c, "EA03_Prop_Container_Bag_02a_PRE", -0.25f, 0.3f, height: 0.6f);
        Prop(c, "EA03_Items_House_Firewood_01a_PRE", 15.6f, 2.5f, height: 1f, width: 1.6f);
        Prop(c, "EA03_Prop_Village_Ladder_02a_PRE", 17f, 2.7f, solid: Solid.None, height: 3f);
        Prop(c, "EA03_Prop_Town_Bench_01a_PRE", 15.2f, 0.9f, height: 0.8f, width: 1.8f);
        Prop(c, "EA03_Village_OutBuilding_Shed_01a_PRE", 18.5f, -10f, solid: Solid.Tall, height: 4f, width: 3f, depth: 3.5f);
        Prop(c, "EA03_Prop_Container_Bale_01a_PRE", 16.4f, -9.3f, height: 0.8f);
        Prop(c, "EA_Items_House_cauldron_01a_PRE", 14.5f, -7.5f, height: 0.9f);
        Prop(c, "EA03_Items_House_Firewood_01a_PRE", 13.6f, -7.9f, height: 0.6f, width: 1.2f);
        Prop(c, "EA03_Wooden_Pin_01a_PRE", 6f, -16.5f, solid: Solid.Trunk, height: 2f);
        Prop(c, "EA03_Wooden_Pin_01b_PRE", 13f, -16.5f, solid: Solid.Trunk, height: 2f);
        Prop(c, "EA03_Prop_Village_Whell_01d_PRE", 3.3f, -16.9f, height: 0.9f);
        Prop(c, "EA03_Nature_Bush_02b_PRE", 4.5f, -19.3f, height: 0.7f, width: 1.4f);
        Prop(c, "EA03_Nature_Bush_03b_PRE", 9f, -19.3f, height: 0.7f, width: 1.4f);
        Prop(c, "EA03_Nature_Bush_01b_PRE", 15.5f, -19.3f, height: 0.7f, width: 1.4f);

        // D. Great Hall (feast hall): the north strip only, so the walkway z 5..8.4 stays clear. Nothing above the 2.0 battlement
        var d = Group(props, "D Great Hall");
        foreach (float x in new[] { -8f, 0f, 7f })
        {
            Prop(d, "EA03_Prop_Tabble_02a_PRE", x, 9.65f, height: 1.1f, width: 2.6f, depth: 1.1f);
            Prop(d, "EA03_Prop_Town_Bench_01b_PRE", x, 8.75f, height: 0.7f, width: 2.4f, depth: 0.55f);
        }
        Prop(d, "EA03_Prop_House_Stove_01b_PRE", -11.5f, 9.7f, height: 1.5f, width: 1.4f, depth: 1.2f);
        // Crockery on the tables
        Prop(d, "EA_Items_House_mug_01a_PRE", -8.6f, 9.6f, solid: Solid.None, height: 0.25f, y: Stack);
        Prop(d, "EA_Items_House_bowl_wod_PRE", -7.4f, 9.7f, solid: Solid.None, height: 0.2f, y: Stack);
        Prop(d, "EA_Items_House_mug_02a_PRE", 0.4f, 9.55f, solid: Solid.None, height: 0.25f, y: Stack);
        Prop(d, "EA_Items_House_bowl_metal_PRE", -0.5f, 9.75f, solid: Solid.None, height: 0.2f, y: Stack);
        Prop(d, "EA_Items_House_kettle_01a_PRE", 6.6f, 9.7f, solid: Solid.None, height: 0.35f, y: Stack);
        Prop(d, "EA_Items_House_mug_01b_PRE", 7.6f, 9.55f, solid: Solid.None, height: 0.25f, y: Stack);
        // Animal-skin rugs on the walkway lead the eye west to the crystal
        Prop(d, "EA03_Prop_Town_AnimalSkin_01b_PRE", 4f, 7.5f, solid: Solid.None, width: 2.2f, depth: 1.6f, y: 0.16f);
        Prop(d, "EA03_Prop_Town_AnimalSkin_01c_PRE", -4f, 7.5f, solid: Solid.None, width: 2.2f, depth: 1.6f, y: 0.16f);

        // E. Crystal room
        var e = Group(props, "E Crystal Room");
        Prop(e, "EA03_Environment_Rock_Flat_04c_Flat_PRE", -17f, 7.5f, solid: Solid.None, height: 0.2f, width: 1.6f, depth: 1.6f, y: 0.12f);
        // Above the hall flagstones (y 0.12) so it doesn't z-fight with them
        Prop(e, "EA03_Environment_Moss_01_PRE", -19.4f, 10f, solid: Solid.None, width: 1.2f, y: 0.165f);

        // F. Treasure Vault: the hoard on the dais (seen from the hall over the battlement), barrels between the trap columns
        var f = Group(props, "F Treasure Vault");
        Prop(f, "EA_Artefact_PRE", 19.45f, 18.05f, height: 1.2f, width: 0.9f, depth: 0.9f, y: 2f);
        Prop(f, "EA03_Prop_Container_Crate_02a_PRE", 17.1f, 18.05f, height: 0.6f, width: 0.6f, depth: 0.6f, y: 2f);
        Prop(f, "EA03_Prop_Container_Bag_02a_PRE", 17.75f, 18.15f, height: 0.5f, y: 2f);
        foreach (float x in new[] { -8f, 0f, 4f })
            Prop(f, Prefabs + "/Barrel.prefab", x, 18.2f);
        Prop(f, "EA_Prop_Hunter_Animal_Trophy_01a_PRE", -6f, 18.55f, solid: Solid.None, height: 1f, depth: 0.4f, y: 2.5f);
        Prop(f, "EA_Prop_Hunter_Animal_Trophy_01a_PRE", 10f, 18.55f, solid: Solid.None, height: 1f, depth: 0.4f, y: 2.5f);
    }

    // Places one prop (see the notes at the top). turn = degrees about Y; height/width/depth = largest allowed size in world
    // units (0 = no limit); length = exact size along X (or Z with alongZ), turning it 90 degrees if its long side is the other way
    static GameObject Prop(Transform parent, string name, float x, float z, float turn = 0f, Solid solid = Solid.Low,
        float height = 0f, float width = 0f, float depth = 0f, float y = 0f, float length = 0f, bool alongZ = false)
    {
        var prefab = FindProp(name);
        if (prefab == null) { notes.Add("Prop not found, skipped: " + name); return null; }
        string path = AssetDatabase.GetAssetPath(prefab);
        bool kit = path.StartsWith(KitPrefabs) || path.StartsWith(ConvertedPrefabs);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = prefab.name.EndsWith("_PRE") ? prefab.name.Substring(0, prefab.name.Length - 4) : prefab.name;
        if (kit) Tidy(go);
        Transform t = go.transform;
        t.rotation = Quaternion.Euler(0f, turn, 0f) * prefab.transform.localRotation;
        t.localScale = prefab.transform.localScale * (kit ? KitScale(prefab) : 1f);
        t.position = new Vector3(x, 0f, z);

        Bounds bounds = MeshBounds(t, null);
        if (length > 0f && bounds.size != Vector3.zero)
        {
            if ((bounds.size.x >= bounds.size.z) == alongZ)
            {
                t.rotation = Quaternion.Euler(0f, 90f, 0f) * t.rotation;
                bounds = MeshBounds(t, null);
            }
            float along = alongZ ? bounds.size.z : bounds.size.x;
            if (along > 0.001f) { t.localScale *= length / along; bounds = MeshBounds(t, null); }
        }
        float fit = 1f;
        if (height > 0f && bounds.size.y > height) fit = Mathf.Min(fit, height / bounds.size.y);
        if (width > 0f && bounds.size.x > width) fit = Mathf.Min(fit, width / bounds.size.x);
        if (depth > 0f && bounds.size.z > depth) fit = Mathf.Min(fit, depth / bounds.size.z);
        if (fit < 1f) { t.localScale *= fit; bounds = MeshBounds(t, null); }

        // Footprint centre on x/z, bottom on the floor (or on whatever was placed there before)
        float floor = float.IsNaN(y) ? StackHeight(x, z) : y;
        t.position += new Vector3(x - bounds.center.x, floor - bounds.min.y, z - bounds.center.z);
        Mark(t); Mark(go);
        if (kit) AddSolid(go, solid);
        placed.Add(MeshBounds(t, null));
        return go;
    }

    // Kit prefabs: LOD0 only (InteractOutline and OccluderFader draw every LOD otherwise; Restyle Village Kit strips
    // them from the prefabs too), their own mesh colliders off (we add simpler ones), and static so they batch
    static void Tidy(GameObject go)
    {
        var hidden = HiddenLods(go.transform);
        foreach (var group in go.GetComponentsInChildren<LODGroup>(true))
        {
            var lods = group.GetLODs();
            if (lods.Length > 1)
            {
                group.SetLODs(new[] { new LOD(0.0001f, lods[0].renderers) });
                Mark(group);
            }
        }
        foreach (var r in hidden) { r.enabled = false; Mark(r); }
        // Only the kit's own colliders: ones we added (AddSolid) stay, so fitting a prop twice doesn't pile them up
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            if (!PrefabUtility.IsAddedComponentOverride(col)) { col.enabled = false; Mark(col); }
        // The toon look is the kit materials' own (Restyle Village Kit). Until that has run they are glossy URP Lit,
        // which OccluderFader still cuts, so just say so once
        if (!restyleNoted && go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Any(m => m != null && m.shader != null && !m.shader.name.StartsWith("Out Of Time/")))
        {
            restyleNoted = true;
            notes.Add("Some kit props still use URP Lit materials: run Tools > Out Of Time > Medieval: Restyle Village Kit for the toon look.");
        }
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            Mark(t.gameObject);
        }
    }

    // Colliders sit on the prop's root, which parents its renderers, so OccluderFader can cut it
    static void AddSolid(GameObject go, Solid solid)
    {
        if (solid == Solid.None)
            return;
        Bounds local = MeshBounds(go.transform, go.transform);
        if (local.size == Vector3.zero)
            return;
        float s = Mathf.Max(0.0001f, go.transform.lossyScale.y);
        if (solid == Solid.Trunk)
        {
            // Undo.AddComponent so Fit Selected Kit Props can be undone too
            var capsule = Undo.AddComponent<CapsuleCollider>(go);
            capsule.radius = 0.35f / s; capsule.height = BlockHeight / s;
            capsule.center = new Vector3(local.center.x, local.min.y + capsule.height * 0.5f, local.center.z);
            return;
        }
        var box = Undo.AddComponent<BoxCollider>(go);
        Vector3 size = local.size, centre = local.center;
        // Tall: as high as the walls, so roofs can't be reached
        if (solid == Solid.Tall) { size.y = Mathf.Max(size.y, BlockHeight / s); centre.y = local.min.y + size.y * 0.5f; }
        box.size = size; box.center = centre;
    }

    static float StackHeight(float x, float z)
    {
        float top = 0f;
        foreach (var b in placed)
            if (x >= b.min.x && x <= b.max.x && z >= b.min.z && z <= b.max.z)
                top = Mathf.Max(top, b.max.y);
        return top;
    }

    // The kit's family scale from SlavicKitLook (found by name, so this still compiles if it changes or goes).
    // Without it: containers are sized so the kit barrel matches our 1-unit Barrel, everything else 1 metre = 0.83
    static float KitScale(GameObject prefab)
    {
        var look = typeof(MedievalRevamp).Assembly.GetType("SlavicKitLook");
        var scaleFor = look?.GetMethod("ScaleFor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { typeof(GameObject) }, null);
        if (scaleFor != null && scaleFor.ReturnType == typeof(float))
            return (float)scaleFor.Invoke(null, new object[] { prefab });
        return AssetDatabase.GetAssetPath(prefab).Contains("/Prop/Container/") ? kitScale : KitMetre;
    }

    // Container scale from the kit barrel against our 1-unit Barrel (the fallback when SlavicKitLook is missing)
    static float MeasureKitScale()
    {
        var barrel = FindProp("EA03_Prop_Container_Barrel_01d_PRE");
        if (barrel == null) { notes.Add("Kit barrel not found: kit scale defaults to 0.83."); return 0.83f; }
        float h = MeshBounds(barrel.transform, barrel.transform).size.y * barrel.transform.localScale.y;
        return h > 0.05f && h < 50f ? 1f / h : 0.83f;
    }

    // Exact file name first (with or without "_PRE"), toon copies before the raw kit, then the first name starting with it
    static GameObject FindProp(string name)
    {
        if (propCache.TryGetValue(name, out var cached))
            return cached;
        GameObject found = null;
        if (name.StartsWith("Assets/"))
            found = Load<GameObject>(name);
        else
        {
            if (propPaths == null)
            {
                propPaths = new List<string>();
                foreach (string folder in new[] { ConvertedPrefabs, KitPrefabs })
                    if (AssetDatabase.IsValidFolder(folder))
                        propPaths.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(guid => AssetDatabase.GUIDToAssetPath(guid)));
            }
            string bare = name.EndsWith("_PRE") ? name.Substring(0, name.Length - 4) : name;
            string hit = propPaths.FirstOrDefault(p => { string f = Path.GetFileNameWithoutExtension(p); return f == name || f == bare || f == bare + "_PRE"; })
                ?? propPaths.Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(bare)).OrderBy(p => p.StartsWith(ConvertedPrefabs) ? 0 : 1).ThenBy(p => p).FirstOrDefault();
            if (hit != null) found = Load<GameObject>(hit);
        }
        propCache[name] = found;
        return found;
    }

    static HashSet<Renderer> HiddenLods(Transform root)
    {
        var hidden = new HashSet<Renderer>();
        foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
        {
            var lods = group.GetLODs();
            if (lods.Length < 2) continue;
            var keep = new HashSet<Renderer>(lods[0].renderers.Where(r => r != null));
            for (int i = 1; i < lods.Length; i++)
                foreach (var r in lods[i].renderers)
                    if (r != null && !keep.Contains(r)) hidden.Add(r);
        }
        return hidden;
    }

    // Box round the visible meshes: in world space, or in "space"'s local space. Works on prefab assets too
    static Bounds MeshBounds(Transform root, Transform space)
    {
        var hidden = HiddenLods(root);
        bool any = false;
        var result = new Bounds();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!r.enabled || hidden.Contains(r) || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly || !ActiveUnder(r.transform, root)) continue;
            var filter = r.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            Matrix4x4 m = space != null ? space.worldToLocalMatrix * r.transform.localToWorldMatrix : r.transform.localToWorldMatrix;
            Bounds mb = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                else result.Encapsulate(p);
            }
        }
        return result;
    }

    static bool ActiveUnder(Transform t, Transform root)
    {
        for (; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf) return false;
            if (t == root) return true;
        }
        return true;
    }

    // ---------------------------------------------------------------- occluder links

    static void Link(Renderer wall, Renderer extra)
    {
        if (!links.TryGetValue(wall, out var list)) links[wall] = list = new List<Renderer>();
        list.Add(extra);
    }

    // Merlons on a wall are cut with it (OccluderLink). Keeps the wall's other links, drops ones from the previous run
    static void ApplyLinks()
    {
        foreach (var pair in links)
        {
            var link = pair.Key.GetComponent<OccluderLink>();
            if (link == null) link = Undo.AddComponent<OccluderLink>(pair.Key.gameObject);
            Undo.RecordObject(link, "Medieval Revamp");
            link.alsoCut = (link.alsoCut ?? new Renderer[0]).Where(r => r != null && !pair.Value.Contains(r)).Concat(pair.Value).ToArray();
            Mark(link);
        }
    }

    // ---------------------------------------------------------------- scene helpers

    static Transform FindNamed(string name) =>
        level.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name && (generated == null || !t.IsChildOf(generated)));

    static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static GameObject InstanceRoot(GameObject go) => PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? go;

    static Renderer MeshOf(Transform t) => t != null ? t.GetComponentInChildren<MeshRenderer>(true) : null;

    // A level section ("Level/8 Treasure Vault"): kept between runs, because it holds the moved traps and chest
    static Transform Section(string name)
    {
        var t = level.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(level, false);
        Undo.RegisterCreatedObjectUndo(t.gameObject, "Medieval Revamp");
        return t;
    }

    static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static Transform MoveNamed(string name, Vector3 to, Transform newParent)
    {
        var t = FindNamed(name);
        if (t == null) { notes.Add("Missing \"" + name + "\"."); return null; }
        if (newParent != null) Reparent(t, newParent);
        Pose(t, to);
        return t;
    }

    // Finds the torch at "from" (or already at "to", so re-runs leave it alone)
    static void MoveTorch(Vector3 start, Vector3 to)
    {
        var torches = level.GetComponentsInChildren<TorchFlicker>(true).Select(f => InstanceRoot(f.gameObject).transform)
            .Where(t => !t.IsChildOf(generated)).ToList();
        if (torches.Any(t => FlatDistance(t.position, to) < 0.02f)) return;
        var torch = torches.FirstOrDefault(t => FlatDistance(t.position, start) < 0.3f);
        if (torch != null) Pose(torch, new Vector3(to.x, torch.position.y, to.z));
        else notes.Add("No torch found at " + start + ".");
    }

    static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    static void Pose(Transform t, Vector3 position, Vector3? scale = null)
    {
        Undo.RecordObject(t, "Medieval Revamp");
        t.position = position;
        if (scale.HasValue) t.localScale = scale.Value;
        Mark(t);
    }

    static void Reparent(Transform t, Transform parent)
    {
        if (t.parent != parent) Undo.SetTransformParent(t, parent, "Medieval Revamp");
    }

    static void Rename(GameObject go, string name)
    {
        if (go.name == name) return;
        Undo.RecordObject(go, "Medieval Revamp");
        go.name = name;
        Mark(go);
    }

    static void Skin(Transform block, Material material)
    {
        var r = MeshOf(block);
        if (r == null || material == null) return;
        Undo.RecordObject(r, "Medieval Revamp");
        r.sharedMaterial = material;
        Mark(r);
    }

    // A StoneBlock (pivot bottom-centre, so y 0 and scale.y = height), optionally re-skinned
    static Transform Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = Spawn(Prefabs + "/StoneBlock.prefab", parent, name, position);
        if (go == null) return null;
        go.transform.localScale = scale;
        Mark(go.transform);
        if (material != null)
        {
            var r = MeshOf(go.transform);
            if (r != null) { r.sharedMaterial = material; Mark(r); }
        }
        return go.transform;
    }

    static GameObject Spawn(string path, Transform parent, string name, Vector3 position)
    {
        var prefab = Load<GameObject>(path);
        if (prefab == null) { notes.Add("Missing " + path + "."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = position;
        Mark(go.transform); Mark(go);
        return go;
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    // Static so the scenery batches. Interactive or moving things (gates, levers, skeletons) are left alone
    static void MakeStatic(Transform root, GameObject[] skip = null)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (skip != null && skip.Any(s => s != null && t.IsChildOf(s.transform))) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(t)) continue;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
    }

    // Prefab instance edits made outside SerializedObject must be recorded, or they revert when the scene reloads
    static void Mark(Object o)
    {
        if (o != null && PrefabUtility.IsPartOfPrefabInstance(o))
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    // Fields are set by name, so a renamed or missing field is a Console note rather than a compile error
    static void Set(Object target, string field, Action<SerializedProperty> write)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { notes.Add($"TODO: {target.GetType().Name} has no field \"{field}\"."); return; }
        write(p);
        so.ApplyModifiedProperties();
    }

    static void SetStrings(Object target, string field, string[] values) => Set(target, field, p =>
    {
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
    });

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
