using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Makes the Slavic village kit (Assets/EmaceArt/Slavic World Free) match the game's look, so any of its prefabs
// can be dropped into a level as they are:
//  1. Every material the kit's prefabs use (plus the kit's Materials folder) moves to toon Lit Cutout, keeping the
//     colour sheet: matte (smoothness 0.1, no specular or reflections), no normal/occlusion maps, transparent cards
//     become alpha-clipped, and the opt-in palette snap + pixel grain in LitCutout give flat faces a pixel-art feel.
//     Lit Cutout is also what OccluderFader needs to cut a see-through hole in a house.
//  2. Textures: point filtered, no mipmaps, uncompressed (the 32px colour sheet stays crisp); big tiling textures
//     (mud, grass) are shrunk to 64px and mapped in world space at 16 px per unit, like the Caveman floors.
//  3. FBX import: flat-ish normals (one toon tone per face), no cameras/lights/animation/blend shapes/tangents,
//     and the FBX's own embedded materials remapped to the converted colour sheet, in case a model is used directly.
//  4. LOD groups stripped from the kit prefabs (LOD0 kept). From our camera (40 units away, FOV 20) a barrel is
//     about 6% of the screen, far below the kit's last LOD (18%), so Unity would cull it completely.
// Scale is NOT changed on the FBX: composite prefabs (houses, rooms) place their pieces in metres, so scaling the
// meshes alone would pull them apart. Scale the placed instance instead: ScaleFor(path) or Spawn(...) below.
// Measured (kit units are metres): barrel 0.82 tall, crate 0.65, door 2.53, house wall 3.0-3.1, trees 9-17.
// Run via Tools > Out Of Time > Medieval: Restyle Village Kit. Safe to run again.
public static class SlavicKitLook
{
    public const string KitFolder = "Assets/EmaceArt/Slavic World Free";
    const string CutoutName = "Out Of Time/Lit Cutout";
    const string ColourSheet = KitFolder + "/Materials/EA03_LPNP-Slavika_Colorsheet_v1.mat";
    const string TreeSheet = KitFolder + "/Materials/EA03_LPNP-Slavika_Colorsheet_wind_tree.mat";

    // A 1.8 m villager is our 1.5-unit player, so 1 kit metre = 0.83 units
    public const float WorldScale = 0.83f;

    // Per family (first match wins). Small props are bigger than "true" scale so they read at 16 px per unit:
    // containers x1.22 makes the kit barrel exactly as tall as our 1-unit Barrel
    static readonly (string folder, float scale)[] FamilyScales =
    {
        ("/Prefabs/Items/Crockery/", 1.5f),
        ("/Prefabs/Items/Food/", 1.5f),
        ("/Prefabs/Prop/EA03_Items_House_", 1.5f),
        ("/Prefabs/Prop/Container/", 1.22f),
        ("/Prefabs/Items/", 1f),
        ("/Prefabs/Prop/Furniture/", 1f),
        ("/Prefabs/Prop/Carpentry/", 1f),
        ("/Prefabs/Prop/wooden/", 1f),
        ("/Prefabs/Prop/Hunter/", 1f),
        ("/Prefabs/Prop/EA_Artefact", 1f),
        ("/Prefabs/Prop/EA03_Prop_Composter", 1f),
    };

    // Looks applied to every restyled material (LitCutout's opt-in extras; 0 turns them off)
    const float PaletteSnap = 0.5f;
    const float PixelGrain = 0.06f;
    const float Smoothness = 0.1f;

    // Big tiling textures: mapped in world space instead of by the mesh UVs, 64px over 4 units = 16 px per unit
    static readonly string[] TilingMaterials = { "Mud", "Grass" };
    const int TilingSize = 64;
    const float TilingRepeat = 0.25f;

    // FBX normals: hard edges above this angle, so each face gets one flat toon tone. Round things (barrels, logs,
    // pots) keep softer normals so the outline crease pass doesn't stripe every facet; foliage keeps its own normals
    const float FlatAngle = 20f;
    const float RoundAngle = 50f;
    static readonly string[] RoundMeshes = { "Barrel", "Log", "Stump", "Trunk", "Pin", "bowl", "mug", "cauldron", "kettle", "Bottle", "Cup", "Apple", "Basket", "Bag", "Wheel", "Whell" };
    static readonly string[] FoliageMeshes = { "Tree", "Bush", "Grass", "Plant", "SweetFlag", "Moss", "Flower", "Leaf" };

    // Demo content and terrain we never use (top-level kit folders only, so Prefabs/Terrain's fields still count);
    // their materials are left alone
    static readonly string[] SkipFolders = { "/SourceFiles/", "/Skybox/", "/Terrain/", "/Scene/", "/Post Processing_profile/", "/URP_Support/" };

    static bool Skipped(string path) => SkipFolders.Any(f => path.StartsWith(KitFolder + f));

    [MenuItem("Tools/Out Of Time/Medieval: Restyle Village Kit")]
    static void Run()
    {
        var cutout = Shader.Find(CutoutName);
        if (cutout == null)
        {
            Debug.LogError("[SlavicKitLook] Lit Cutout shader missing. Check Assets/Shaders compiled without errors.");
            return;
        }
        if (!AssetDatabase.IsValidFolder(KitFolder))
        {
            Debug.LogError($"[SlavicKitLook] Kit not found at {KitFolder}.");
            return;
        }

        int materials = RestyleKitMaterials(cutout, out var skipped);
        int models = FixModelImports();
        int prefabs = StripKitLods(out int lodGroups);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SlavicKitLook] Done: {materials} materials restyled, {models} FBX imports updated, " +
                  $"{lodGroups} LOD groups stripped from {prefabs} prefabs." +
                  (skipped.Count > 0 ? "\nLeft alone (shader not convertible, e.g. the kit's wind grass): " + string.Join(", ", skipped) : ""));
    }

    // For props added later (another pack, a new kit folder): restyles the selected materials, or the materials
    // used by the selected prefabs/objects, the same way
    [MenuItem("Tools/Out Of Time/Medieval: Restyle Selected Materials")]
    static void RestyleSelected()
    {
        var cutout = Shader.Find(CutoutName);
        if (cutout == null)
        {
            Debug.LogError("[SlavicKitLook] Lit Cutout shader missing.");
            return;
        }

        var found = new HashSet<Material>(Selection.GetFiltered<Material>(SelectionMode.Assets));
        foreach (var go in Selection.gameObjects)
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null)
                        found.Add(m);

        // The game's own Lit Cutout materials (walls, floors, top tiles) never had the kit look; leave them alone
        // so selecting a prefab that mixes kit props and our walls doesn't reset the walls' settings
        found.RemoveWhere(m => m.shader == cutout && !AssetDatabase.GetAssetPath(m).StartsWith(KitFolder + "/")
                               && m.GetFloat("_OOT_PaletteSnap") <= 0f && m.GetFloat("_OOT_PixelGrain") <= 0f);

        int count = 0;
        var textures = new HashSet<Texture>();
        foreach (var m in found)
            if (Restyle(m, cutout))
            {
                count++;
                AddTexture(m, textures);
            }
        FixTextures(found, textures);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SlavicKitLook] Restyled {count} of {found.Count} selected materials " +
                  "(the game's own Lit Cutout materials, built-in/package materials and FBX-embedded ones are skipped).");
    }

    // Logs every kit prefab's size after its family scale, so the layout can be checked against the 1.5-unit
    // player, the 1.6 jump and the 6-unit walls (anything over 1.6 belongs north of, or beside, the paths)
    [MenuItem("Tools/Out Of Time/Medieval: Report Village Kit Sizes")]
    static void ReportSizes()
    {
        var lines = new List<(string path, string line)>();
        foreach (var path in KitPrefabPaths())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;
            float s = ScaleFor(path);
            Bounds b = MeasureBounds(prefab);
            Vector3 size = b.size * s;
            string flag = size.y > 6f ? "  OVER 6" : size.y > 1.6f ? "  tall" : "";
            lines.Add((path, $"{size.y,5:0.00} tall  {size.x,5:0.0} x {size.z,4:0.0}  (x{s:0.##}){flag}  {path.Substring(KitFolder.Length + 1)}"));
        }
        var text = new StringBuilder($"[SlavicKitLook] {lines.Count} kit prefabs in game units (player 1.5, jump 1.6, walls 6):\n");
        foreach (var l in lines.OrderBy(l => l.path))
            text.AppendLine(l.line);
        Debug.Log(text.ToString());
    }

    // ---------------------------------------------------------------- scale and placement

    // Uniform scale to give a placed kit prefab (prefab asset path, or anything inside the kit)
    public static float ScaleFor(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return WorldScale;
        foreach (var (folder, scale) in FamilyScales)
            if (assetPath.Contains(folder))
                return scale;
        return WorldScale;
    }

    // A prefab asset, or a placed instance of one (scene objects have no asset path of their own)
    public static float ScaleFor(GameObject prefab)
    {
        if (prefab == null)
            return WorldScale;
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
            path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab);
        return ScaleFor(path);
    }

    // Places a kit prefab (path relative to the kit folder, e.g. "Prefabs/Prop/Container/EA03_Prop_Container_Barrel_01d_PRE.prefab",
    // or a full Assets/ path) at its family scale times extraScale. Static by default, like the Caveman scenery
    public static GameObject Spawn(string prefabPath, Transform parent, Vector3 position, float yaw = 0f, float extraScale = 1f, bool isStatic = true)
    {
        string path = prefabPath.StartsWith("Assets/") ? prefabPath : $"{KitFolder}/{prefabPath}";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[SlavicKitLook] Missing kit prefab {path}");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = Vector3.one * (ScaleFor(path) * extraScale);
        if (isStatic)
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = true;
        return go;
    }

    // The prefab's visible bounds (LOD0 only) in its root's own space, before any placement scale.
    // Works on prefab assets, which have no renderer bounds of their own
    public static Bounds MeasureBounds(GameObject prefab)
    {
        var skip = new HashSet<Renderer>();
        foreach (var group in prefab.GetComponentsInChildren<LODGroup>(true))
        {
            var lods = group.GetLODs();
            for (int i = 1; i < lods.Length; i++)
                foreach (var r in lods[i].renderers)
                    if (r != null)
                        skip.Add(r);
        }

        Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
        bool any = false;
        var bounds = new Bounds();
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = filter.GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null || r == null || !r.enabled || skip.Contains(r)
                || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly || !ActiveInPrefab(filter.transform, prefab.transform))
                continue;
            Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
            Bounds local = filter.sharedMesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                    bounds.Encapsulate(p);
            }
        }
        return bounds;
    }

    // activeInHierarchy isn't meaningful on prefab assets, so walk up by hand
    static bool ActiveInPrefab(Transform t, Transform root)
    {
        for (; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
                return false;
            if (t == root)
                break;
        }
        return true;
    }

    // ---------------------------------------------------------------- materials

    static int RestyleKitMaterials(Shader cutout, out List<string> skipped)
    {
        skipped = new List<string>();
        var found = new HashSet<Material>();

        // Everything the kit's prefabs actually render with (wherever the material lives)...
        foreach (var path in KitPrefabPaths())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null)
                        found.Add(m);
        }
        // ...plus the kit's own material folder (mud and grass ground materials aren't on any prefab)
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { KitFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Skipped(path))
                continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null)
                found.Add(m);
        }

        int count = 0;
        var textures = new HashSet<Texture>();
        foreach (var m in found)
        {
            if (Restyle(m, cutout))
            {
                count++;
                AddTexture(m, textures);
            }
            else
                skipped.Add($"{m.name} ({(m.shader != null ? m.shader.name : "no shader")})");
        }
        FixTextures(found, textures);
        return count;
    }

    // Moves one material onto toon Lit Cutout with the kit look. Accepts URP Lit/Simple Lit/Complex Lit/Unlit,
    // Built-in Standard and Lit Cutout itself (re-applies the settings). Returns false if left alone
    public static bool Restyle(Material m, Shader cutout)
    {
        if (m == null || m.shader == null || cutout == null)
            return false;
        // Only our own project's material assets: built-in and package materials (Default-Material, URP's Lit.mat)
        // are shared by everything. Materials inside an FBX are read-only; FixModelImports remaps those instead
        string assetPath = AssetDatabase.GetAssetPath(m);
        if (!assetPath.StartsWith("Assets/") || AssetImporter.GetAtPath(assetPath) is ModelImporter)
            return false;

        string from = m.shader.name;
        bool standard = from == "Standard" || from == "Standard (Specular setup)";
        bool urp = from == "Universal Render Pipeline/Lit" || from == "Universal Render Pipeline/Simple Lit"
                   || from == "Universal Render Pipeline/Complex Lit" || from == "Universal Render Pipeline/Unlit";
        if (m.shader != cutout && !standard && !urp)
            return false;

        // Read what we keep before the shader swap
        string mapName = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? "_BaseMap" : m.HasProperty("_MainTex") ? "_MainTex" : null;
        Texture albedo = mapName != null ? m.GetTexture(mapName) : null;
        Vector2 tiling = mapName != null ? m.GetTextureScale(mapName) : Vector2.one;
        Vector2 offset = mapName != null ? m.GetTextureOffset(mapName) : Vector2.zero;
        Color color = m.HasProperty("_BaseColor") && !standard ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
        float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
        bool transparent = (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) || (standard && m.HasProperty("_Mode") && m.GetFloat("_Mode") >= 2f);
        bool clipped = transparent || m.IsKeywordEnabled("_ALPHATEST_ON") || (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f)
                       || (standard && m.HasProperty("_Mode") && m.GetFloat("_Mode") >= 1f);
        bool doubleSided = clipped && (transparent || (m.HasProperty("_Cull") && m.GetFloat("_Cull") < 0.5f) || standard);
        float cull = doubleSided ? 0f : m.HasProperty("_Cull") ? m.GetFloat("_Cull") : 2f;
        Color emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
        Texture emissionMap = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
        bool emissive = m.IsKeywordEnabled("_EMISSION") && emission.maxColorComponent > 0.001f;
        bool tilingSurface = TilingMaterials.Any(n => m.name.Contains(n)) && !clipped && albedo != null;

        m.shader = cutout;

        m.SetTexture("_BaseMap", albedo);
        m.SetTextureScale("_BaseMap", tilingSurface ? Vector2.one * TilingRepeat : tiling);
        m.SetTextureOffset("_BaseMap", tilingSurface ? Vector2.zero : offset);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_OOT_WorldUV", tilingSurface ? 1f : 0f);

        // Matte: toon shading ignores these, but the normal Lit path (and the look while toon is off) shouldn't shine
        m.SetFloat("_WorkflowMode", 1f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", Smoothness);
        m.SetFloat("_SpecularHighlights", 0f);
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        // Normal maps put noise into the toon bands and speckle the outline creases; toon ignores the rest
        foreach (var map in new[] { "_BumpMap", "_ParallaxMap", "_OcclusionMap", "_MetallicGlossMap", "_SpecGlossMap", "_DetailAlbedoMap", "_DetailNormalMap", "_DetailMask" })
            if (m.HasProperty(map))
                m.SetTexture(map, null);
        foreach (var keyword in new[] { "_NORMALMAP", "_PARALLAXMAP", "_OCCLUSIONMAP", "_METALLICSPECGLOSSMAP", "_SPECGLOSSMAP", "_SPECULAR_SETUP",
                     "_DETAIL_MULX2", "_DETAIL_SCALED", "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_RECEIVE_SHADOWS_OFF",
                     "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON" })
            m.DisableKeyword(keyword);
        m.SetFloat("_ReceiveShadows", 1f);

        // Opaque or alpha-clipped only: transparents write no depth, so they'd get no outlines and sort badly
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.Zero);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
        m.SetFloat("_ZWrite", 1f);
        m.SetFloat("_AlphaToMask", 0f);
        m.SetFloat("_Cull", cull);
        if (clipped)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }
        else
        {
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "");
            m.renderQueue = -1;
        }

        // RealtimeEmissive: with the default "emissive is black" flag, URP's material check strips _EMISSION on save
        if (emissive)
        {
            m.SetColor("_EmissionColor", emission);
            m.SetTexture("_EmissionMap", emissionMap);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        // Same toon settings as the rest of the game (SetupLook / the Medieval materials), so props match the floor
        m.SetFloat("_OOT_Toon", 1f);
        m.SetFloat("_OOT_ToonBands", 3f);
        m.SetFloat("_OOT_ToonSoftness", 0.12f);
        m.SetFloat("_OOT_AmbientStrength", 0.25f);
        m.SetFloat("_OOT_TorchStrength", 0.6f);
        m.SetFloat("_OOT_TorchMax", 1.2f);
        m.SetFloat("_OOT_TorchBands", 4f);
        // Top-face tiles use object extents, which static batching breaks
        m.SetFloat("_OOT_TopCount", 0f);
        m.SetFloat("_OOT_PaletteSnap", PaletteSnap);
        m.SetFloat("_OOT_PixelGrain", PixelGrain);

        // Hundreds of small pieces: instancing keeps the draw calls down
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return true;
    }

    static void AddTexture(Material m, HashSet<Texture> textures)
    {
        var t = m.GetTexture("_BaseMap");
        if (t != null)
            textures.Add(t);
    }

    // Point filter, no mipmaps, uncompressed: compression and mips smear the colour sheet's swatches into each other
    static void FixTextures(IEnumerable<Material> materials, HashSet<Texture> textures)
    {
        var tiling = new HashSet<Texture>(materials.Where(m => m != null && m.HasProperty("_OOT_WorldUV") && m.GetFloat("_OOT_WorldUV") > 0.5f)
            .Select(m => m.GetTexture("_BaseMap")).Where(t => t != null));
        foreach (var t in textures)
        {
            string path = AssetDatabase.GetAssetPath(t);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                continue;
            bool changed = importer.filterMode != FilterMode.Point || importer.mipmapEnabled
                           || importer.textureCompression != TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (tiling.Contains(t))
            {
                // 64px tiles at 0.25 repeats per unit = 16 px per unit; Unity's downscale averages, so no aliasing
                changed |= importer.maxTextureSize != TilingSize || importer.wrapMode != TextureWrapMode.Repeat;
                importer.maxTextureSize = TilingSize;
                importer.wrapMode = TextureWrapMode.Repeat;
            }
            else if (t.width > 128 || t.height > 128)
            {
                // Cards (grass) are small on screen; a big texture would only shimmer
                changed |= importer.maxTextureSize != 128;
                importer.maxTextureSize = 128;
            }
            if (changed)
                importer.SaveAndReimport();
        }
    }

    // ---------------------------------------------------------------- FBX import

    static int FixModelImports()
    {
        var sheet = AssetDatabase.LoadAssetAtPath<Material>(ColourSheet);
        var treeSheet = AssetDatabase.LoadAssetAtPath<Material>(TreeSheet);
        int count = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { KitFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Skipped(path) || !(AssetImporter.GetAtPath(path) is ModelImporter importer))
                    continue;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                bool changed = false;

                // Embedded materials (URP Lit copies) would look glossy and never get cut: point them at the colour sheet
                if (importer.materialImportMode != ModelImporterMaterialImportMode.None && sheet != null)
                {
                    foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                    {
                        var target = embedded.name.Contains("Tree") && treeSheet != null ? treeSheet : sheet;
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), target);
                        changed = true;
                    }
                }

                bool foliage = FoliageMeshes.Any(n => file.Contains(n));
                if (!foliage)
                {
                    float angle = RoundMeshes.Any(n => file.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0) ? RoundAngle : FlatAngle;
                    changed |= importer.importNormals != ModelImporterNormals.Calculate || !Mathf.Approximately(importer.normalSmoothingAngle, angle)
                               || importer.normalSmoothingSource != ModelImporterNormalSmoothingSource.FromAngle;
                    importer.importNormals = ModelImporterNormals.Calculate;
                    importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
                    importer.normalSmoothingSource = ModelImporterNormalSmoothingSource.FromAngle;
                    importer.normalSmoothingAngle = angle;
                }

                // Nothing here uses normal maps, and the kit has no rigs, cameras or lights worth importing
                changed |= importer.importTangents != ModelImporterTangents.None || importer.importCameras || importer.importLights
                           || importer.importBlendShapes || importer.importAnimation || importer.addCollider;
                importer.importTangents = ModelImporterTangents.None;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importBlendShapes = false;
                importer.importAnimation = false;
                importer.addCollider = false;

                if (changed)
                {
                    importer.SaveAndReimport();
                    count++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        return count;
    }

    // ---------------------------------------------------------------- LODs

    static int StripKitLods(out int groups)
    {
        groups = 0;
        int prefabs = 0;
        foreach (var path in KitPrefabPaths())
        {
            // Already stripped (or never had LODs): skip the slow open/save
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<LODGroup>(true) == null)
                continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int stripped = StripLods(root);
                if (stripped > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    groups += stripped;
                    prefabs++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return prefabs;
    }

    // Removes every LOD group under root, keeping LOD0. Lower LODs owned by root are deleted; ones inside a nested
    // prefab are just switched off (that prefab gets its own pass). Also usable on any other imported model
    public static int StripLods(GameObject root)
    {
        int count = 0;
        foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
        {
            if (group == null)
                continue; // went with an object deleted earlier in this pass
            if (PrefabUtility.IsPartOfPrefabInstance(group))
            {
                // A nested kit prefab's group is fixed in its own prefab. A nested FBX (or another pack's prefab)
                // never gets that pass, so switch its group and lower LODs off here instead (instance overrides)
                string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(group) ?? "";
                if (!(source.StartsWith(KitFolder + "/") && source.EndsWith(".prefab")))
                    count += DisableLods(group);
                continue;
            }
            var lods = group.GetLODs();
            if (lods.Length == 0)
            {
                Object.DestroyImmediate(group);
                count++;
                continue;
            }

            var keep = new HashSet<Renderer>(lods[0].renderers.Where(r => r != null));
            if (keep.Count == 0)
                continue; // odd group with an empty LOD0: leave it rather than lose the model
            var drop = new HashSet<Renderer>();
            for (int i = 1; i < lods.Length; i++)
                foreach (var r in lods[i].renderers)
                    if (r != null && !keep.Contains(r))
                        drop.Add(r);

            Object.DestroyImmediate(group);
            count++;
            foreach (var r in drop)
                RemoveRenderer(r);
        }
        return count;
    }

    // A switched-off LOD group culls nothing, so LOD0 always draws; the lower LODs are hidden so they don't overlap it.
    // Returns 1 only when something changed, so a second run doesn't re-save the prefab
    static int DisableLods(LODGroup group)
    {
        var lods = group.GetLODs();
        if (lods.Length == 0 || !lods[0].renderers.Any(r => r != null))
            return 0;
        var keep = new HashSet<Renderer>(lods[0].renderers.Where(r => r != null));
        bool changed = group.enabled;
        group.enabled = false;
        for (int i = 1; i < lods.Length; i++)
            foreach (var r in lods[i].renderers)
                if (r != null && !keep.Contains(r) && r.enabled)
                {
                    r.enabled = false;
                    changed = true;
                }
        return changed ? 1 : 0;
    }

    static void RemoveRenderer(Renderer r)
    {
        if (r == null)
            return;
        if (PrefabUtility.IsPartOfPrefabInstance(r))
        {
            r.enabled = false;
            return;
        }
        var go = r.gameObject;
        var filter = go.GetComponent<MeshFilter>();
        // Just a mesh: delete the whole object. Anything else on it (a collider, children) stays
        bool onlyMesh = go.transform.childCount == 0 && go.GetComponents<Component>().All(c => c is Transform || c is MeshFilter || c == r);
        if (onlyMesh)
        {
            Object.DestroyImmediate(go);
            return;
        }
        Object.DestroyImmediate(r);
        if (filter != null)
            Object.DestroyImmediate(filter);
    }

    // ---------------------------------------------------------------- helpers

    static IEnumerable<string> KitPrefabPaths()
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { KitFolder })
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Where(p => p.EndsWith(".prefab") && !Skipped(p))
            .Distinct();
    }
}
