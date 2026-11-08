using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Makes the Caveman level look natural:
//  - every tree and rock gets its own low-poly faceted shape, size, lean, turn and colour (seeded by its position,
//    so running this again gives the same result)
//  - the box walls become generated mountains: a ridge around the whole level and a cave mountain with
//    overhanging inner cliffs, a rock arch over the entrance, stalagmites and boulders at the cliff feet
// Meshes are flat-shaded with terraced ledges and point-filtered pixel textures, so they read as pixel art
// through the low render scale and outlines. All generated meshes live in one asset next to the prefabs.
// Run via Tools > Out Of Time > Caveman: Natural Trees, Rocks and Mountains (edits prefabs/Caveman/CavemanLevel.prefab).
// CavemanSceneBuilder calls Apply() too, so a full rebuild keeps it.
public static class CavemanNature
{
    const string Prefabs = "Assets/prefabs/Caveman";
    const string Mats = "Assets/Materials/Caveman";
    const string Textures = "Assets/Textures/Caveman";
    const string MeshAssetPath = Prefabs + "/CavemanNatureMeshes.asset";
    const string LevelPrefab = Prefabs + "/CavemanLevel.prefab";

    // Level outline (floor is 64 x 36 around the origin) and the cave
    static readonly Vector2 CaveCenter = new Vector2(18f, 0f);
    static readonly Vector2[] CavePath =
    {
        new Vector2(11.9f, 1.5f), new Vector2(11.3f, 3.9f), new Vector2(12.8f, 6.1f), new Vector2(15.8f, 6.8f),
        new Vector2(19.2f, 6.9f), new Vector2(22.4f, 6.2f), new Vector2(24.6f, 4.2f), new Vector2(25.3f, 0.4f),
        new Vector2(24.7f, -3.8f), new Vector2(22.6f, -6.2f), new Vector2(19.1f, -6.9f), new Vector2(15.7f, -6.7f),
        new Vector2(12.9f, -6.0f), new Vector2(11.3f, -3.9f), new Vector2(11.9f, -1.5f),
    };
    static readonly Vector3[] MagneticStones = { new Vector3(14.5f, 0, -2.2f), new Vector3(18, 0, 2.4f), new Vector3(21.3f, 0, -1.5f) };

    static Material stone, stoneWarm, stoneCool, moss, leaves, leavesDeep, leavesOlive, bark, barkDark;
    static Material mountainRock, caveRock, mountainGrass;
    static Object meshContainer;
    static int meshCount;

    [MenuItem("Tools/Out Of Time/Caveman: Natural Trees, Rocks and Mountains")]
    static void ApplyToPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(LevelPrefab);
        try
        {
            Apply(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, LevelPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[CavemanNature] Trees, rocks and mountains updated in " + LevelPrefab);
    }

    public static void Apply(Transform level)
    {
        LoadMaterials();
        StartMeshAsset();

        RemoveBoxWalls(level);
        FixLayout(level);

        foreach (var breakable in level.GetComponentsInChildren<Breakable>(true))
        {
            if (breakable.name.StartsWith("CavemanTree")) VaryTree(breakable);
            else if (breakable.name.StartsWith("CavemanRock")) VaryRock(breakable);
        }

        var mountains = Group(level, "Mountains");
        BuildPerimeter(mountains);
        BuildCave(Group(level, "5 Cave Mountain"));

        AssetDatabase.SaveAssets();
    }

    // ---------- Setup ----------

    static void LoadMaterials()
    {
        stone = Load("Stone"); leaves = Load("Leaves"); bark = Load("Bark");
        // Rock tops get dusty cave earth, not grass or moss: this is the inside of a cavern
        var dustTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/CaveFloor32.png");
        var stoneTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/Stone32.png");
        var leafTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/Leaves32.png");

        stoneWarm = Variant("StoneWarm", stone, stoneTexture, new Color(1f, 0.93f, 0.84f), 0.5f, true);
        stoneCool = Variant("StoneCool", stone, stoneTexture, new Color(0.88f, 0.86f, 0.84f), 0.5f, true);
        moss = Variant("RockDust", stone, dustTexture, new Color(1f, 0.95f, 0.88f), 0.5f, true);
        leavesDeep = Variant("LeavesDeep", leaves, leafTexture, new Color(0.7f, 0.84f, 0.7f), 1.4f);
        leavesOlive = Variant("LeavesOlive", leaves, leafTexture, new Color(1f, 0.92f, 0.62f), 1.4f);
        barkDark = Variant("BarkDark", bark, AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/Bark32.png"), new Color(0.74f, 0.68f, 0.66f), 1f);
        var cliffTexture = CliffTexture();
        mountainRock = Variant("MountainRock", stone, cliffTexture, new Color(1f, 0.9f, 0.78f), 0.4f, true);
        caveRock = Variant("CaveRock", stone, cliffTexture, new Color(0.6f, 0.64f, 0.74f), 0.4f, true);
        mountainGrass = Variant("RockDustLight", stone, dustTexture, new Color(1f, 0.95f, 0.86f), 0.5f, true);
        AssetDatabase.DeleteAsset(Mats + "/Moss.mat"); AssetDatabase.DeleteAsset(Mats + "/MountainGrass.mat");
    }

    // 32x32 pixel rock face with horizontal strata: layers of warm greys and browns, a dark crack under each
    // layer and a lit lip on top, broken up by a few pebbles. On cliffs (projected sideways) it reads as layered rock
    static Texture2D CliffTexture()
    {
        string path = Textures + "/Cliff32.png";
        Color[] layers = { Hex("8E8474"), Hex("A39887"), Hex("7A7165"), Hex("978B78"), Hex("857A6B") };
        int[] heights = { 6, 5, 7, 6, 8 };
        var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var rng = new System.Random(321);
        int y0 = 0;
        for (int layer = 0; layer < layers.Length; layer++)
        {
            int h = heights[layer];
            for (int y = y0; y < y0 + h && y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    // Layer edges wobble a pixel, so the strata aren't ruler-straight
                    int wobble = (x * 7 + layer * 13) % 11 < 3 ? 1 : 0;
                    int local = y - y0 - wobble;
                    Color c = layers[layer];
                    if (local <= 0) c = Hex("4F4840");                       // crack under the layer
                    else if (local == h - 1) c = Color.Lerp(c, Hex("C9BEA8"), 0.55f); // lit lip
                    else if (((x / 2) * 7 + (y / 2) * 13 + layer * 5) % 17 == 0) c = Color.Lerp(c, Hex("5E564C"), 0.5f); // 2x2 chips, not lone pixels
                    else if (((x / 2) * 11 + (y / 2) * 5 + layer * 3) % 23 == 3) c = Color.Lerp(c, Hex("BDB29C"), 0.5f);
                    t.SetPixel(x, y, c);
                }
            y0 += h;
        }
        // Vertical joints, as in real cliffs
        for (int k = 0; k < 6; k++)
        {
            int x = rng.Next(32), y = rng.Next(32), len = rng.Next(3, 6);
            for (int i = 0; i < len; i++) t.SetPixel(x, (y + i) % 32, Hex("5A5249"));
        }
        t.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(path), t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out Color c); return c; }

    static Material Load(string name)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/" + name + ".mat");
        if (m == null) throw new System.InvalidOperationException("Missing " + Mats + "/" + name + ".mat. Run Build Caveman Era first.");
        return m;
    }

    // A copy of a base material (same shader and settings) with its own texture, tint and tiling
    // worldUV: texture mapped in world space (Tiling = repeats per unit), so every rock and cliff shows the same
    // pixel size as the sprites (0.5 = a 32px texture over 2 units = 16px per unit), whatever its scale
    static Material Variant(string name, Material source, Texture texture, Color tint, float tiling, bool worldUV = false)
    {
        string path = Mats + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(source);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = source.shader;
            m.CopyPropertiesFromMaterial(source);
        }
        // Always the toon cutout shader: a stock Lit base would look glossy, then change when OccluderFader cuts it
        var cutout = Shader.Find("Out Of Time/Lit Cutout");
        if (cutout != null && m.shader != cutout) m.shader = cutout;
        m.DisableKeyword("_EMISSION");
        if (texture != null) m.SetTexture("_BaseMap", texture);
        m.SetTextureScale("_BaseMap", Vector2.one * tiling);
        m.color = tint;
        m.SetFloat("_OOT_WorldUV", worldUV ? 1f : 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void StartMeshAsset()
    {
        AssetDatabase.DeleteAsset(MeshAssetPath);
        meshContainer = null;
        meshCount = 0;
    }

    static Mesh Save(Mesh mesh)
    {
        mesh.name = $"{mesh.name} {meshCount++}";
        if (meshContainer == null)
        {
            AssetDatabase.CreateAsset(mesh, MeshAssetPath);
            meshContainer = mesh;
        }
        else
        {
            AssetDatabase.AddObjectToAsset(mesh, meshContainer);
        }
        return mesh;
    }

    static readonly string[] BoxWalls =
    {
        "Cave North Wall", "Cave South Wall", "Cave Back Wall", "Cave Entrance A", "Cave Entrance B", "Carved Cave Lintel", "Cave Rock",
        "North Boundary", "South Boundary", "East Boundary", "West Boundary", "Mountains", "5 Cave Mountain",
    };

    static void RemoveBoxWalls(Transform level)
    {
        foreach (var t in level.GetComponentsInChildren<Transform>(true).Where(t => t != level && BoxWalls.Contains(t.name)).ToList())
            if (t != null)
                Object.DestroyImmediate(t.gameObject);
    }

    // Bigger floor under the mountains, a cave floor that stays inside the cave, the bramble filling the
    // (now wider) entrance, and the cave glyphs moved off the old wall line
    static void FixLayout(Transform level)
    {
        foreach (var t in level.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Cave Earth Floor")
            {
                // Mapped in world space, so only the size changes
                t.localScale = new Vector3(64f, 0.6f, 36f);
            }
            else if (t.name == "Dark Cave Floor")
            {
                // Kept inside the rounded cave walls, so its corners don't poke out past them
                t.localPosition = new Vector3(18.3f, 0.015f, 0f);
                t.localScale = new Vector3(12f, 0.08f, 12f);
            }
            else if (t.name.StartsWith("CaveBramble"))
            {
                t.localPosition = new Vector3(11.6f, 0f, 0f);
                var solid = t.Find("Solid Collider");
                if (solid != null && solid.TryGetComponent(out BoxCollider box))
                {
                    box.center = new Vector3(0f, 0.8f, 0f);
                    box.size = new Vector3(0.9f, 1.6f, 3.4f);
                }
                if (t.TryGetComponent(out SphereCollider range)) range.radius = 1.4f;
            }
        }

        var glyphs = level.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("CaveGlyph") && t.parent != null && !t.parent.name.StartsWith("CaveGlyph")).ToList();
        for (int i = 0; i < glyphs.Count; i++)
            glyphs[i].localPosition = new Vector3(13.4f, 0.9f, i % 2 == 0 ? -3.1f : 3.1f);
    }

    // ---------- Trees and rocks ----------

    static void VaryTree(Breakable tree)
    {
        Transform root = tree.transform;
        int seed = Seed(root.localPosition);
        var rng = new System.Random(seed);

        float size = Range(rng, 0.85f, 1.2f);
        root.localRotation = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
        root.localScale = new Vector3(size, size * Range(rng, 0.9f, 1.22f), size);

        if (tree.visual != null)
            tree.visual.localRotation = Quaternion.Euler(Range(rng, -5f, 5f), 0f, Range(rng, -5f, 5f));

        Material leaf = Pick(rng, leaves, leavesDeep, leavesOlive);
        Material trunk = rng.NextDouble() < 0.5 ? bark : barkDark;

        foreach (Transform part in tree.visual != null ? tree.visual : root)
        {
            Transform source = Source(part);
            var renderer = part.GetComponent<MeshRenderer>();
            if (renderer == null) continue;

            if (part.name.StartsWith("Canopy"))
            {
                var filter = part.GetComponent<MeshFilter>();
                filter.sharedMesh = Save(Blob("Canopy", rng.Next(), 2, 0.32f, -0.3f, 0f));
                part.localPosition = source.localPosition + new Vector3(Range(rng, -0.18f, 0.18f), Range(rng, -0.15f, 0.2f), Range(rng, -0.18f, 0.18f));
                part.localScale = Vector3.Scale(source.localScale, new Vector3(Range(rng, 0.85f, 1.18f), Range(rng, 0.85f, 1.15f), Range(rng, 0.85f, 1.18f)));
                part.localRotation = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
                renderer.sharedMaterials = new[] { leaf, leaf };
                // Some trees lose a side clump, so the silhouettes differ
                part.gameObject.SetActive(!(part.name.EndsWith("West") || part.name.EndsWith("East")) || rng.NextDouble() > 0.3);
            }
            else if (part.name.StartsWith("Pixel Leaf"))
            {
                part.localPosition = source.localPosition + new Vector3(Range(rng, -0.3f, 0.3f), Range(rng, -0.2f, 0.25f), Range(rng, -0.3f, 0.3f));
                part.localRotation = Quaternion.Euler(Range(rng, -20f, 20f), Range(rng, 0f, 90f), Range(rng, -20f, 20f));
                renderer.sharedMaterial = leaf;
                part.gameObject.SetActive(rng.NextDouble() > 0.25);
            }
            else
            {
                renderer.sharedMaterial = trunk;
                if (part.name == "Trunk")
                    part.localScale = Vector3.Scale(source.localScale, new Vector3(Range(rng, 0.85f, 1.15f), 1f, Range(rng, 0.85f, 1.15f)));
                else
                    part.localRotation = source.localRotation * Quaternion.Euler(0f, Range(rng, -40f, 40f), Range(rng, -10f, 10f));
            }
        }
    }

    static void VaryRock(Breakable rock)
    {
        Transform root = rock.transform;
        var rng = new System.Random(Seed(root.localPosition) + 911);

        root.localRotation = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
        root.localScale = new Vector3(Range(rng, 0.8f, 1.25f), Range(rng, 0.72f, 1.2f), Range(rng, 0.8f, 1.25f));
        if (rock.visual != null)
            rock.visual.localRotation = Quaternion.Euler(Range(rng, -6f, 6f), 0f, Range(rng, -6f, 6f));

        Material body = Pick(rng, stone, stoneWarm, stoneCool);
        Material top = rng.NextDouble() < 0.45 ? moss : body;

        foreach (Transform part in rock.visual != null ? rock.visual : root)
        {
            var filter = part.GetComponent<MeshFilter>();
            var renderer = part.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null) continue;
            Transform source = Source(part);

            filter.sharedMesh = Save(Blob("Rock", rng.Next(), 1, 0.38f, -0.32f, 0f));
            part.localPosition = source.localPosition + new Vector3(Range(rng, -0.12f, 0.12f), 0f, Range(rng, -0.12f, 0.12f));
            part.localScale = Vector3.Scale(source.localScale, new Vector3(Range(rng, 0.85f, 1.2f), Range(rng, 0.8f, 1.2f), Range(rng, 0.85f, 1.2f)));
            part.localRotation = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
            renderer.sharedMaterials = new[] { body, top };
            if (part.name != "Main Boulder")
                part.gameObject.SetActive(rng.NextDouble() > 0.2);
        }
    }

    static Transform Source(Transform part)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(part);
        return source != null ? source : part;
    }

    // ---------- Mountains and cave ----------

    // Profiles across a ridge: (offset across, -1 = play side .. 1 = far side; height as a fraction of the ridge's height).
    // Both feet rise straight to over half the height, so the player can't jump onto a ledge and climb out
    static readonly Vector2[] PerimeterProfile =
    {
        new Vector2(-1.0f, -0.3f), new Vector2(-0.96f, 0.62f), new Vector2(-0.78f, 0.72f), new Vector2(-0.55f, 0.95f),
        new Vector2(-0.15f, 1.0f), new Vector2(0.3f, 0.88f), new Vector2(0.75f, 0.62f), new Vector2(1.05f, -0.3f),
    };

    // Cave cliffs lean over the cave floor (the lip sticks out past the foot)
    static readonly Vector2[] CaveProfile =
    {
        new Vector2(-1.0f, -0.3f), new Vector2(-1.03f, 0.58f), new Vector2(-1.16f, 0.8f), new Vector2(-0.8f, 1.0f),
        new Vector2(-0.25f, 0.9f), new Vector2(0.3f, 1.0f), new Vector2(0.78f, 0.64f), new Vector2(1.05f, -0.3f),
    };

    static void BuildPerimeter(Transform parent)
    {
        // Rounded loop just outside the play area (inner foot around x = +-26.5, z = +-12)
        var loop = new List<Vector2>();
        const int steps = 64;
        for (int i = 0; i < steps; i++)
        {
            float a = i / (float)steps * Mathf.PI * 2f;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            // Superellipse: a rectangle with rounded corners
            float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.25f) * 29f;
            float z = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.25f) * 14.6f;
            loop.Add(new Vector2(x, z));
        }

        // Taller at the back (north), lower at the front (south) where it sits between the camera and the player
        var mesh = Ridge("Perimeter Ridge", loop.ToArray(), true, Vector2.zero, 2.7f, 1337,
            PerimeterProfile, p => p.y > 4f ? new Vector2(5.5f, 8.5f) : p.y < -4f ? new Vector2(3.6f, 4.6f) : new Vector2(4.2f, 7f));
        Rock("Perimeter Mountains", parent, mesh, mountainRock, true);
        // The ridge has ledges a jump can reach, so an invisible wall runs inside it, just behind the inner foot
        InvisibleWalls(parent, "Perimeter Wall", loop, true, 1.6f, Vector2.zero);

        // Loose boulders along the inner foot
        var rng = new System.Random(4242);
        for (int i = 0; i < loop.Count; i++)
        {
            if (rng.NextDouble() > 0.55) continue;
            Vector2 p = loop[i];
            Vector2 inward = -p.normalized;
            Vector2 at = p + inward * (2.7f + Range(rng, 0.1f, 0.6f));
            if (Vector2.Distance(at, new Vector2(-21f, 0f)) < 4f) continue; // keep the time machine clear
            Boulder(parent, rng, new Vector3(at.x, 0f, at.y), Range(rng, 0.5f, 1.2f), mountainRock);
        }
    }

    static void BuildCave(Transform parent)
    {
        var mesh = Ridge("Cave Ridge", CavePath, false, CaveCenter, 1.25f, 4242, CaveProfile, _ => new Vector2(3.8f, 5.4f));
        Rock("Cave Mountain", parent, mesh, caveRock, true);
        // Down the middle of the cave ridge, so it can't be climbed into or out of; the bramble closes the entrance
        InvisibleWalls(parent, "Cave Wall", Resample(CavePath, false, 1.1f), false, 0f, CaveCenter);

        // Rock arch over the entrance
        var arch = Rock("Entrance Arch", parent, Save(Blob("Arch", 77, 2, 0.3f, -1f, 0f)), caveRock, false);
        arch.transform.localPosition = new Vector3(11.7f, 3.4f, 0f);
        arch.transform.localScale = new Vector3(2.3f, 1.5f, 4.6f);

        // Stalagmites, clear of the magnetic stones and the walking line
        var rng = new System.Random(99);
        Vector3[] spots =
        {
            new Vector3(13.4f, 0, -4.6f), new Vector3(15.6f, 0, 4.9f), new Vector3(19.6f, 0, -5.0f), new Vector3(23.2f, 0, 1.9f),
            new Vector3(20.6f, 0, 5.1f), new Vector3(16.8f, 0, -0.4f), new Vector3(23.0f, 0, -3.1f),
        };
        foreach (var at in spots)
        {
            float h = Range(rng, 0.9f, 1.7f);
            var go = Rock("Stalagmite", parent, Save(Spike("Stalagmite", rng.Next())), caveRock, false);
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(Range(rng, 0.5f, 0.75f), h, Range(rng, 0.5f, 0.75f));
            go.transform.localRotation = Quaternion.Euler(Range(rng, -6f, 6f), Range(rng, 0f, 360f), Range(rng, -6f, 6f));
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.5f, 0f); col.radius = 0.35f; col.height = 1f;
        }

        // Boulders along the inner cliff foot, away from the entrance and the stones
        for (int i = 0; i < 26; i++)
        {
            float t = (i + 0.5f) / 26f;
            Vector2 p = PointOnPath(CavePath, t, out Vector2 tangent);
            Vector2 n = new Vector2(-tangent.y, tangent.x);
            if (Vector2.Dot(n, CaveCenter - p) < 0f) n = -n;
            Vector2 at = p + n * (1.35f + Range(rng, 0f, 0.3f));
            if (at.x < 13.2f && Mathf.Abs(at.y) < 3f) continue;
            if (MagneticStones.Any(s => Vector2.Distance(at, new Vector2(s.x, s.z)) < 1.6f)) continue;
            if (rng.NextDouble() > 0.6) continue;
            Boulder(parent, rng, new Vector3(at.x, 0f, at.y), Range(rng, 0.4f, 0.9f), caveRock);
        }
    }

    // Tall, thin, invisible boxes joining the points of a path, moved inset units towards interior.
    // Far higher than any jump, so mountains stop the player even where their slopes have ledges
    static void InvisibleWalls(Transform parent, string name, List<Vector2> pts, bool closed, float inset, Vector2 interior)
    {
        const float height = 14f, thickness = 0.8f;
        var group = Group(parent, name + "s");
        int count = closed ? pts.Count : pts.Count - 1;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
            Vector2 along = (b - a).normalized, normal = new Vector2(-along.y, along.x);
            if (Vector2.Dot(normal, interior - (a + b) * 0.5f) < 0f) normal = -normal;
            a += normal * inset; b += normal * inset;
            var go = new GameObject(name); go.transform.SetParent(group, false);
            Vector2 mid = (a + b) * 0.5f;
            go.transform.localPosition = new Vector3(mid.x, height * 0.5f - 1f, mid.y);
            go.transform.localRotation = Quaternion.LookRotation(new Vector3(along.x, 0f, along.y));
            // A little longer than the segment, so neighbouring walls overlap at the bends
            go.AddComponent<BoxCollider>().size = new Vector3(thickness, height, Vector2.Distance(a, b) + thickness);
            go.isStatic = true;
        }
    }

    static void Boulder(Transform parent, System.Random rng, Vector3 at, float size, Material body)
    {
        var go = Rock("Boulder", parent, Save(Blob("Boulder", rng.Next(), 1, 0.4f, -0.25f, 0f)), body, false);
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { body, rng.NextDouble() < 0.5 ? mountainGrass : body };
        go.transform.localPosition = at + Vector3.up * size * 0.2f;
        go.transform.localScale = new Vector3(size * Range(rng, 0.9f, 1.4f), size * Range(rng, 0.7f, 1.1f), size * Range(rng, 0.9f, 1.4f));
        go.transform.localRotation = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
        var col = go.AddComponent<MeshCollider>();
        col.convex = true;
    }

    static GameObject Rock(string name, Transform parent, Mesh mesh, Material body, bool solid)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { body, mountainGrass };
        go.isStatic = true;
        if (solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    // A ridge along a path: cross-section rows from the profile, heights wandering between heightRange(x, y),
    // ledges stepped to 0.4 units, edges jittered so nothing lines up. Open paths get flat rock end caps
    static Mesh Ridge(string name, Vector2[] path, bool closed, Vector2 interior, float halfWidth, int seed,
                      Vector2[] profile, System.Func<Vector2, Vector2> heightRange)
    {
        var pts = Resample(path, closed, 1.1f);
        int n = pts.Count, m = profile.Length;
        var grid = new Vector3[n, m];
        var rng = new System.Random(seed);
        // Coherent noise along the ridge (smooth bulges and bays), plus a little per-vertex roughness
        float Wave(int i, int j, float salt) => Mathf.PerlinNoise(seed * 0.011f + i * 0.21f + salt, j * 0.37f + salt * 1.7f) - 0.5f;

        for (int i = 0; i < n; i++)
        {
            Vector2 p = pts[i];
            Vector2 prev = closed ? pts[(i - 1 + n) % n] : pts[Mathf.Max(0, i - 1)];
            Vector2 next = closed ? pts[(i + 1) % n] : pts[Mathf.Min(n - 1, i + 1)];
            Vector2 tangent = (next - prev).normalized;
            Vector2 outward = new Vector2(-tangent.y, tangent.x);
            if (Vector2.Dot(outward, interior - p) > 0f) outward = -outward;

            Vector2 range = heightRange(p);
            float h = Mathf.Lerp(range.x, range.y, Mathf.PerlinNoise(seed * 0.013f + i * 0.09f, 3.7f));
            if (rng.NextDouble() < 0.08) h *= 1.18f; // the odd crag

            for (int j = 0; j < m; j++)
            {
                bool foot = j == 0 || j == m - 1;
                float off = profile[j].x * halfWidth + Wave(i, j, 0f) * (foot ? 0.5f : 0.9f) * halfWidth
                            + (float)(rng.NextDouble() - 0.5) * 0.08f * halfWidth;
                float y = profile[j].y < 0f ? profile[j].y : profile[j].y * h * (1f + Wave(i, j, 9f) * 0.35f) + (float)(rng.NextDouble() - 0.5) * 0.1f;
                if (y > 0f) y = Mathf.Lerp(y, Mathf.Round(y / 0.5f) * 0.5f, 0.8f);
                Vector2 slide = tangent * Wave(i, j, 4f) * 0.5f;
                Vector2 at = p + outward * off + slide;
                grid[i, j] = new Vector3(at.x, y, at.y);
            }
        }

        var tris = new List<Vector3>();
        int rows = closed ? n : n - 1;
        for (int i = 0; i < rows; i++)
        {
            int i2 = (i + 1) % n;
            for (int j = 0; j < m - 1; j++)
            {
                Vector3 a = grid[i, j], b = grid[i2, j], c = grid[i2, j + 1], d = grid[i, j + 1];
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }
        }

        // One winding for the whole grid: make the top of the middle row face up
        int peak = 0;
        for (int j = 1; j < m; j++) if (profile[j].y > profile[peak].y) peak = j;
        int probe = (n / 2) * (m - 1) * 6 + Mathf.Min(peak, m - 2) * 6;
        if (Vector3.Cross(tris[probe + 1] - tris[probe], tris[probe + 2] - tris[probe]).y < 0f)
            for (int t = 0; t < tris.Count; t += 3) (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);

        if (!closed)
        {
            Cap(tris, grid, 0, m, pts[0] - pts[1]);
            Cap(tris, grid, n - 1, m, pts[n - 1] - pts[n - 2]);
        }

        return Save(Faceted(name, tris, 0.25f));
    }

    // Closes an open ridge end with a fan, facing away along the path
    static void Cap(List<Vector3> tris, Vector3[,] grid, int row, int m, Vector2 away)
    {
        Vector3 center = Vector3.zero;
        for (int j = 0; j < m; j++) center += grid[row, j];
        center /= m;
        center.y = Mathf.Max(0.2f, center.y * 0.6f);
        Vector3 dir = new Vector3(away.x, 0f, away.y);
        for (int j = 0; j < m - 1; j++)
        {
            Vector3 a = center, b = grid[row, j], c = grid[row, j + 1];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), dir) < 0f) (b, c) = (c, b);
            tris.Add(a); tris.Add(b); tris.Add(c);
        }
        // Bottom edge, so the cap is closed
        Vector3 b0 = grid[row, m - 1], c0 = grid[row, 0];
        if (Vector3.Dot(Vector3.Cross(b0 - center, c0 - center), dir) < 0f) (b0, c0) = (c0, b0);
        tris.Add(center); tris.Add(b0); tris.Add(c0);
    }

    static List<Vector2> Resample(Vector2[] path, bool closed, float step)
    {
        var result = new List<Vector2>();
        int segments = closed ? path.Length : path.Length - 1;
        for (int s = 0; s < segments; s++)
        {
            Vector2 a = path[s], b = path[(s + 1) % path.Length];
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
            for (int k = 0; k < count; k++) result.Add(Vector2.Lerp(a, b, k / (float)count));
        }
        if (!closed) result.Add(path[path.Length - 1]);
        return result;
    }

    static Vector2 PointOnPath(Vector2[] path, float t, out Vector2 tangent)
    {
        var pts = Resample(path, false, 0.2f);
        float f = t * (pts.Count - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, pts.Count - 2);
        tangent = (pts[i + 1] - pts[i]).normalized;
        return Vector2.Lerp(pts[i], pts[i + 1], f - i);
    }

    // ---------- Meshes ----------

    // A lumpy, flat-shaded ball (radius about 0.5, like Unity's sphere) with a flattened base.
    // Submesh 0 = sides, submesh 1 = faces pointing up (moss, grass, sunlit leaves)
    static Mesh Blob(string name, int seed, int subdivisions, float roughness, float flatBelow, float taper)
    {
        Icosphere(subdivisions, out var verts, out var faces);
        var rng = new System.Random(seed);
        // Smooth-ish lumps: a few random bulges summed, plus small per-vertex noise
        var bulges = Enumerable.Range(0, 5).Select(_ => (dir: Random3(rng).normalized, amount: Range(rng, -1f, 1f))).ToArray();
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 v = verts[i];
            float r = 1f;
            foreach (var bulge in bulges) r += bulge.amount * roughness * 0.5f * Mathf.Max(0f, Vector3.Dot(v, bulge.dir));
            r += Range(rng, -0.5f, 0.5f) * roughness * 0.4f;
            v *= 0.5f * r;
            if (taper > 0f) { float k = Mathf.Lerp(1f, 1f - taper, Mathf.InverseLerp(-0.5f, 0.5f, v.y)); v.x *= k; v.z *= k; }
            if (v.y < flatBelow) v.y = flatBelow;
            verts[i] = v;
        }
        var tris = new List<Vector3>();
        foreach (var f in faces) { tris.Add(verts[f.x]); tris.Add(verts[f.y]); tris.Add(verts[f.z]); }
        FaceOutwards(tris, _ => Vector3.zero);
        return Faceted(name, tris, 1f);
    }

    // A tapering, bumpy cone (unit height, base on y = 0)
    static Mesh Spike(string name, int seed)
    {
        var rng = new System.Random(seed);
        const int sides = 7, rings = 4;
        var ring = new Vector3[rings + 1, sides];
        for (int r = 0; r <= rings; r++)
        {
            float y = r / (float)rings;
            float radius = Mathf.Lerp(0.5f, 0.06f, Mathf.Pow(y, 0.8f));
            for (int s = 0; s < sides; s++)
            {
                float a = (s + Range(rng, -0.2f, 0.2f)) / sides * Mathf.PI * 2f;
                float rr = radius * Range(rng, 0.8f, 1.15f);
                ring[r, s] = new Vector3(Mathf.Cos(a) * rr + Range(rng, -0.04f, 0.04f), y, Mathf.Sin(a) * rr);
            }
        }
        var tris = new List<Vector3>();
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++)
            {
                int s2 = (s + 1) % sides;
                Vector3 a = ring[r, s], b = ring[r, s2], c = ring[r + 1, s2], d = ring[r + 1, s];
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(a); tris.Add(d); tris.Add(c);
            }
        Vector3 tip = new Vector3(Range(rng, -0.05f, 0.05f), 1.08f, Range(rng, -0.05f, 0.05f));
        for (int s = 0; s < sides; s++) { tris.Add(ring[rings, s]); tris.Add(tip); tris.Add(ring[rings, (s + 1) % sides]); }
        FaceOutwards(tris, centroid => new Vector3(0f, Mathf.Clamp(centroid.y, 0f, 0.9f), 0f));
        return Faceted(name, tris, 1f);
    }

    // Flips any triangle whose normal points towards the inside (the point center(triangle centroid) gives)
    static void FaceOutwards(List<Vector3> tris, System.Func<Vector3, Vector3> center)
    {
        for (int t = 0; t < tris.Count; t += 3)
        {
            Vector3 a = tris[t], b = tris[t + 1], c = tris[t + 2];
            Vector3 centroid = (a + b + c) / 3f;
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), centroid - center(centroid)) < 0f)
                (tris[t + 1], tris[t + 2]) = (c, b);
        }
    }

    // Flat shading: every triangle gets its own vertices and normal. UVs are projected along the face's main axis
    // (so textures don't stretch on cliffs), and up-facing faces go to submesh 1
    static Mesh Faceted(string name, List<Vector3> tris, float uvScale)
    {
        var vertices = new List<Vector3>(tris.Count);
        var normals = new List<Vector3>(tris.Count);
        var uvs = new List<Vector2>(tris.Count);
        var sides = new List<int>();
        var tops = new List<int>();

        for (int t = 0; t < tris.Count; t += 3)
        {
            Vector3 a = tris[t], b = tris[t + 1], c = tris[t + 2];
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) continue;
            n.Normalize();
            var target = n.y > 0.72f ? tops : sides;
            foreach (var v in new[] { a, b, c })
            {
                target.Add(vertices.Count);
                vertices.Add(v);
                normals.Add(n);
                Vector3 an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                Vector2 uv = an.y >= an.x && an.y >= an.z ? new Vector2(v.x, v.z) : an.x >= an.z ? new Vector2(v.z, v.y) : new Vector2(v.x, v.y);
                uvs.Add(uv * uvScale);
            }
        }

        var mesh = new Mesh { name = name };
        if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(sides, 0);
        mesh.SetTriangles(tops, 1);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    static void Icosphere(int subdivisions, out List<Vector3> verts, out List<Vector3Int> faces)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        verts = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
        faces = new List<Vector3Int>
        {
            new Vector3Int(0, 11, 5), new Vector3Int(0, 5, 1), new Vector3Int(0, 1, 7), new Vector3Int(0, 7, 10), new Vector3Int(0, 10, 11),
            new Vector3Int(1, 5, 9), new Vector3Int(5, 11, 4), new Vector3Int(11, 10, 2), new Vector3Int(10, 7, 6), new Vector3Int(7, 1, 8),
            new Vector3Int(3, 9, 4), new Vector3Int(3, 4, 2), new Vector3Int(3, 2, 6), new Vector3Int(3, 6, 8), new Vector3Int(3, 8, 9),
            new Vector3Int(4, 9, 5), new Vector3Int(2, 4, 11), new Vector3Int(6, 2, 10), new Vector3Int(8, 6, 7), new Vector3Int(9, 8, 1),
        };
        for (int s = 0; s < subdivisions; s++)
        {
            var cache = new Dictionary<long, int>();
            var next = new List<Vector3Int>();
            var vlist = verts;
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int index)) return index;
                vlist.Add(((vlist[a] + vlist[b]) * 0.5f).normalized);
                cache[key] = vlist.Count - 1;
                return vlist.Count - 1;
            }
            foreach (var f in faces)
            {
                int ab = Mid(f.x, f.y), bc = Mid(f.y, f.z), ca = Mid(f.z, f.x);
                next.Add(new Vector3Int(f.x, ab, ca)); next.Add(new Vector3Int(f.y, bc, ab));
                next.Add(new Vector3Int(f.z, ca, bc)); next.Add(new Vector3Int(ab, bc, ca));
            }
            faces = next;
        }
        // Unity winds clockwise for front faces
        for (int i = 0; i < faces.Count; i++) faces[i] = new Vector3Int(faces[i].x, faces[i].z, faces[i].y);
    }

    // ---------- Used by CavemanSettlement (after Apply, in the same build) ----------

    // Not saved here: the settlement keeps it in its own mesh asset, so rebuilding nature alone can't delete it
    internal static Mesh StalagmiteMesh(int seed) => Spike("Stalagmite", seed);
    internal static Material[] WallMaterials => new[] { mountainRock, mountainGrass };

    // ---------- Helpers ----------

    static Transform Group(Transform parent, string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        return g;
    }

    static int Seed(Vector3 p) => Mathf.RoundToInt(p.x * 7919f) ^ Mathf.RoundToInt(p.z * 104729f) ^ 0x5bd1e995;
    static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    static Vector3 Random3(System.Random rng) => new Vector3(Range(rng, -1f, 1f), Range(rng, -1f, 1f), Range(rng, -1f, 1f));
    static Material Pick(System.Random rng, params Material[] options) => options[rng.Next(options.Length)];
}
