using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-shot setup for the game's look:
//  1. Moves every URP Lit material in the project onto Lit Cutout with toon shading on
//     (same properties, so colours/textures/emission carry over; also lets the see-through hole work everywhere)
//  2. Adds the Outlines and Era Palette full-screen passes to the PC and Mobile renderers
//  3. Imports the era palettes and wires them into the Persistent prefab (EraPalette)
//  4. Lowers the toon ambient (torches stand out), gives the inner walls (MedievalStone) the same top tiles
//     as the border's wall tops, darkens the floor a little, and lets the player cast a sprite shadow
//  5. Puts the player's sprite on Sprite Lit, so sun shadows and torch light affect it like the floor
// Only assets and prefabs change, no scene files. Safe to run again.
// Run via Tools > Out Of Time > Setup Look, then delete this file.
public static class SetupLook
{
    const string LitName = "Universal Render Pipeline/Lit";
    const string CutoutName = "Out Of Time/Lit Cutout";
    const string PersistentPath = "Assets/prefabs/Persistent.prefab";
    const string PalettesFolder = "Assets/Textures/Palettes";

    static readonly string[] Renderers = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };
    static readonly string[] SkipFolders = { "Assets/Undead Survivor", "Assets/TextMesh Pro" };

    static readonly (Era era, string file)[] PaletteFiles =
    {
        (Era.Caveman, "Era1_Caveman"), (Era.Medieval, "Era2_Medieval"),
        (Era.Steampunk, "Era3_Steampunk"), (Era.Future, "Era4_Future"),
    };

    [MenuItem("Tools/Out Of Time/Setup Look")]
    static void Run()
    {
        var cutout = Shader.Find(CutoutName);
        var outlineShader = Shader.Find("Out Of Time/Outlines");
        var paletteShader = Shader.Find("Out Of Time/Era Palette");
        if (cutout == null || outlineShader == null || paletteShader == null)
        {
            Debug.LogError("[SetupLook] Shaders missing. Check Assets/Shaders compiled without errors.");
            return;
        }

        ConvertMaterials(cutout);

        var outlines = GetOrCreateMaterial("Assets/Materials/Post/Outlines.mat", outlineShader);
        var palette = GetOrCreateMaterial("Assets/Materials/Post/EraPalette.mat", paletteShader);
        var holeOutlines = GetOrCreateMaterial("Assets/Materials/Post/OutlinesHole.mat", outlineShader);
        holeOutlines.SetFloat("_HoleOnly", 1f);
        EditorUtility.SetDirty(holeOutlines);
        foreach (var path in Renderers)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
            if (data == null)
            {
                Debug.LogWarning($"[SetupLook] Missing renderer {path}");
                continue;
            }
            // Outlines on the opaque scene only: sprites and tiles draw over them afterwards
            AddFullScreenPass(data, "Outlines", outlines, FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents,
                ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            // Second outline pass after transparents: redraws the cut wall's edges inside the see-through hole,
            // which the floor tiles and sprites would otherwise paint over
            AddFullScreenPass(data, "Outlines (Hole)", holeOutlines, FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing,
                ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            // Palette over everything, sprites included
            AddFullScreenPass(data, "Era Palette", palette, FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing,
                ScriptableRenderPassInput.None);
        }

        SetupPalettes();
        SetupLighting(cutout);
        SetupWallTops();
        SetupPlayerShadow(cutout);
        SetupPlayerLighting();
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupLook] Done.");
    }

    static void ConvertMaterials(Shader cutout)
    {
        int converted = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (SkipFolders.Any(f => path.StartsWith(f)))
                continue;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null)
                continue;

            if (mat.shader.name == LitName)
            {
                mat.shader = cutout;
                converted++;
            }
            if (mat.shader == cutout)
            {
                mat.SetFloat("_OOT_Toon", 1f);
                EditorUtility.SetDirty(mat);
            }
        }
        Debug.Log($"[SetupLook] Moved {converted} Lit materials to toon Lit Cutout.");
    }

    static Material GetOrCreateMaterial(string path, Shader shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        return mat;
    }

    static void AddFullScreenPass(ScriptableRendererData data, string name, Material material,
        FullScreenPassRendererFeature.InjectionPoint point, ScriptableRenderPassInput requirements)
    {
        var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == name);
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            // The renderer keeps its features in two parallel lists
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        feature.passMaterial = material;
        feature.injectionPoint = point;
        feature.requirements = requirements;
        feature.fetchColorBuffer = true;
        feature.SetActive(true);
        EditorUtility.SetDirty(feature);
        data.SetDirty();
        EditorUtility.SetDirty(data);
    }

    // Darker ambient on every toon material and a darker floor, so torch light reads clearly
    static void SetupLighting(Shader cutout)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (SkipFolders.Any(f => path.StartsWith(f)))
                continue;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader != cutout)
                continue;
            mat.SetFloat("_OOT_AmbientStrength", 0.25f);
            EditorUtility.SetDirty(mat);
        }

        var floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FloorReceiveShadows.mat");
        if (floor != null)
        {
            floor.SetFloat("_BaseBrightness", 0.8f);
            floor.SetFloat("_LightStrength", 0.6f);
            EditorUtility.SetDirty(floor);
        }
    }

    // Inner walls' top faces use the same tiles as the border's wall tops (LevelBorder Wall Tiles),
    // sampled straight from the original atlas, so no copy of the Asset Store art is made
    static void SetupWallTops()
    {
        var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Medieval/MedievalStone.mat");
        var borderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/LevelBorder.prefab");
        var border = borderPrefab != null ? borderPrefab.GetComponent<LevelBorder>() : null;
        if (stone == null || border == null || border.wallTiles == null)
        {
            Debug.LogWarning("[SetupLook] Couldn't set up wall tops (MedievalStone or LevelBorder missing)");
            return;
        }

        var sprites = border.wallTiles.OfType<UnityEngine.Tilemaps.Tile>().Select(t => t.sprite).Where(sp => sp != null)
            .Distinct().Take(6).ToList();
        if (sprites.Count == 0)
        {
            Debug.LogWarning("[SetupLook] Border wall tiles have no sprites; wall tops left as they are");
            return;
        }

        var atlas = sprites[0].texture;
        stone.SetTexture("_OOT_TopAtlas", atlas);
        stone.SetFloat("_OOT_TopCount", sprites.Count(sp => sp.texture == atlas));
        int i = 0;
        foreach (var sp in sprites.Where(sp => sp.texture == atlas))
        {
            Rect r = sp.textureRect;
            stone.SetVector("_OOT_TopRect" + i, new Vector4(r.x / atlas.width, r.y / atlas.height, r.width / atlas.width, r.height / atlas.height));
            i++;
        }
        EditorUtility.SetDirty(stone);
    }

    // A shadow-only material for SpriteShadowCaster (alpha-clipped to the sprite, both sides), added to the player
    static void SetupPlayerShadow(Shader cutout)
    {
        const string path = "Assets/Materials/SpriteShadowCaster.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(cutout);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = cutout;
        mat.SetFloat("_AlphaClip", 1f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(mat);

        const string playerPath = "Assets/prefabs/Player.prefab";
        var root = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            var billboard = root.GetComponentInChildren<Billboard>(true);
            var target = billboard != null ? billboard.gameObject : null;
            if (target == null || target.GetComponent<SpriteRenderer>() == null)
            {
                Debug.LogWarning("[SetupLook] Player has no billboard sprite; no shadow caster added");
                return;
            }
            var caster = target.GetComponent<SpriteShadowCaster>();
            if (caster == null)
                caster = target.AddComponent<SpriteShadowCaster>();
            caster.shadowMaterial = mat;
            PrefabUtility.SaveAsPrefabAsset(root, playerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The player's sprite gets the lit sprite shader, so sun shadows and torch light affect it like the floor
    static void SetupPlayerLighting()
    {
        var shader = Shader.Find("Out Of Time/Sprite Lit");
        if (shader == null)
        {
            Debug.LogWarning("[SetupLook] Sprite Lit shader missing; player lighting skipped");
            return;
        }

        const string path = "Assets/Materials/SpriteLit.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        EditorUtility.SetDirty(mat);

        const string playerPath = "Assets/prefabs/Player.prefab";
        var root = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            var billboard = root.GetComponentInChildren<Billboard>(true);
            var sprite = billboard != null ? billboard.GetComponent<SpriteRenderer>() : null;
            if (sprite == null)
            {
                Debug.LogWarning("[SetupLook] Player has no billboard sprite; player lighting skipped");
                return;
            }
            sprite.sharedMaterial = mat;
            PrefabUtility.SaveAsPrefabAsset(root, playerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetupPalettes()
    {
        var entries = new System.Collections.Generic.List<EraPalette.Entry>();
        foreach (var (era, file) in PaletteFiles)
        {
            string path = $"{PalettesFolder}/{file}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogWarning($"[SetupLook] Missing palette {path}");
                continue;
            }
            // Exact colours: no filtering, compression, mipmaps or resizing
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            entries.Add(new EraPalette.Entry { era = era, palette = AssetDatabase.LoadAssetAtPath<Texture2D>(path) });
        }

        var root = PrefabUtility.LoadPrefabContents(PersistentPath);
        try
        {
            // Unity's "missing component" placeholder breaks ??, so check explicitly
            var component = root.GetComponent<EraPalette>();
            if (component == null)
                component = root.AddComponent<EraPalette>();
            component.palettes = entries.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, PersistentPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
