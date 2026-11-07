using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using B = CavemanSceneBuilder;

// Turns the Caveman level into a lived-in cave settlement:
//  - cobblestone paths and plazas (generated meshes, world-space pixel textures) and darker earth patches
//  - a big bonfire with dancers, a drummer and a running child; an elder telling stories on a bear-skin rug;
//    a kitchen with an oven, baskets and pots; a flint knapper by the crafting stone; a hunter watching the boar
//  - rope-bound wooden pillars with dreamcatchers, torches along the paths, cave paintings on the cavern wall,
//    a mammoth-tusk arch, drying racks, bones, stalagmites
// All art is generated pixel art (same style as CavemanSceneBuilder's), saved under Sprites/Caveman.
// Villagers use CaveNpc: idle loops on world time and one line of talk per press.
// Called by CavemanSceneBuilder.MakeLevel after CavemanNature.Apply (needs the mountains for the paintings).
public static class CavemanSettlement
{
    const string Sprites = "Assets/Sprites/Caveman";
    const string Textures = "Assets/Textures/Caveman";
    const string Mats = "Assets/Materials/Caveman";
    const string MeshAssetPath = "Assets/prefabs/Caveman/CavemanSettlementMeshes.asset";
    const float NpcPpu = 18f; // same pixel density as the player sprite

    static readonly Color Ink = Hex("2B1D14");
    static readonly Color Clear = new Color(0, 0, 0, 0);

    static Material spriteMat, shadowCasterMat, cobble, soot, dirt, bark, rope, stone;
    static UnityEngine.Object meshContainer;
    static int meshCount;

    static readonly Vector3 Fire = new Vector3(-16f, 0f, 6.6f);
    static readonly Vector3 Rug = new Vector3(-2.5f, 0f, 6.6f);
    static readonly Vector3 Kitchen = new Vector3(-17f, 0f, -6.8f);

    public static void Apply(Transform level)
    {
        spriteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLit.mat");
        shadowCasterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteShadowCaster.mat");
        LoadMaterials();
        made.Clear();
        AssetDatabase.DeleteAsset(MeshAssetPath);
        meshContainer = null; meshCount = 0; prism = null;

        var root = Group(level, "7 Settlement");
        Ground(Group(root, "Ground"));
        Camp(Group(root, "Bonfire"));
        Elder(Group(root, "Story Rug"));
        KitchenArea(Group(root, "Kitchen"));
        Workshop(Group(root, "Knapping"));
        Hunters(Group(root, "Hunters"));
        Pillars(Group(root, "Pillars"));
        Torches(Group(root, "Torches"));
        Paintings(level, Group(root, "Cave Paintings"));
        Stalagmites(Group(root, "Stalagmites"));
        RaiseObstacles(level);
        AssetDatabase.SaveAssets();
    }

    // ---------- Materials ----------

    static void LoadMaterials()
    {
        stone = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/Stone.mat");
        bark = AssetDatabase.LoadAssetAtPath<Material>(Mats + "/BarkDark.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(Mats + "/Bark.mat");
        var floor = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/CaveFloor32.png");
        cobble = Mat("Cobble", B.Texture("Cobble32", 32, CobblePixel()), Color.white, 0.5f);
        soot = Mat("CaveSoot", floor, new Color(0.5f, 0.44f, 0.4f), 0.5f);
        dirt = Mat("CaveEarthDark", floor, new Color(0.84f, 0.78f, 0.72f), 0.5f);
        rope = Mat("Rope", B.Texture("Rope32", 32, (x, y) =>
        {
            int band = ((x + y * 2) / 3) % 3;
            return band == 0 ? Hex("C49A5C") : band == 1 ? Hex("9A6B3F") : Hex("E3C38A");
        }), Color.white, 1.5f);
    }

    // A copy of the caveman Stone material (toon Lit Cutout) with its own texture, mapped in world space
    static Material Mat(string name, Texture texture, Color tint, float tiling)
    {
        string path = Mats + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(stone); AssetDatabase.CreateAsset(m, path); }
        else { m.shader = stone.shader; m.CopyPropertiesFromMaterial(stone); }
        m.SetTexture("_BaseMap", texture);
        m.SetTextureScale("_BaseMap", Vector2.one * tiling);
        m.SetFloat("_OOT_WorldUV", 1f);
        m.color = tint;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Rounded cobblestones: a wrapping Voronoi pattern, dark mortar between stones, each stone lit on its
    // upper-left rim and shaded on its lower-right (north is up on screen)
    static Func<int, int, Color> CobblePixel()
    {
        var rng = new System.Random(57);
        var points = new List<Vector2>();
        // Staggered rows (like laid stones) with jitter, so it never reads as a square grid
        for (int gy = 0; gy < 4; gy++)
            for (int gx = 0; gx < 4; gx++)
                points.Add(new Vector2((gx + 0.5f + (gy % 2) * 0.5f + (float)(rng.NextDouble() - 0.5) * 0.5f) * 8f, (gy + 0.5f + (float)(rng.NextDouble() - 0.5) * 0.4f) * 8f));
        Color[] tones = { Hex("8C857B"), Hex("7E776E"), Hex("978F84"), Hex("857D72") };
        var noise = new float[32, 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) noise[x, y] = (float)rng.NextDouble();
        return (x, y) =>
        {
            float d1 = float.MaxValue, d2 = float.MaxValue; int nearest = 0; Vector2 toCenter = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
                for (int ox = -1; ox <= 1; ox++)
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - (points[i] + new Vector2(ox * 32, oy * 32));
                        float dist = d.magnitude;
                        if (dist < d1) { d2 = d1; d1 = dist; nearest = i; toCenter = d; }
                        else if (dist < d2) d2 = dist;
                    }
            float edge = d2 - d1;
            if (edge < 1.1f) return Hex("3F3833");
            Color c = tones[nearest % tones.Length];
            if (edge < 2.1f) c = toCenter.y - toCenter.x * 0.5f > 0f ? Hex("A8A094") : Hex("635C55");
            else if (noise[x, y] < 0.07f) c = Hex("6E675F");
            else if (noise[x, y] > 0.95f) c = Hex("A39B8F");
            return c;
        };
    }

    // ---------- Ground: paths, plazas, earth patches ----------

    static void Ground(Transform parent)
    {
        float y = 0.03f;
        float Next() { y += 0.004f; return y; }

        Flat(parent, "Main Path", Ribbon(new[] { new Vector2(-24f, 0.2f), new Vector2(-20f, 0f), new Vector2(-15f, 0.4f), new Vector2(-10f, -0.2f), new Vector2(-5f, 0.1f), new Vector2(0f, -0.2f), new Vector2(5f, 0.2f), new Vector2(9f, 0f), new Vector2(11.4f, 0f) }, 2.3f, 11, Next()), cobble);
        Flat(parent, "Bonfire Path", Ribbon(new[] { new Vector2(-14.2f, 0.4f), new Vector2(-14.8f, 2.4f), new Vector2(-15.6f, 4.4f) }, 1.6f, 12, Next()), cobble);
        Flat(parent, "Story Path", Ribbon(new[] { new Vector2(-3f, 0.4f), new Vector2(-2.8f, 2.6f), new Vector2(-2.6f, 4.4f) }, 1.5f, 13, Next()), cobble);
        Flat(parent, "Kitchen Path", Ribbon(new[] { new Vector2(-16f, -0.3f), new Vector2(-16.8f, -2.8f), new Vector2(-17f, -4.8f) }, 1.5f, 14, Next()), cobble);

        Flat(parent, "Time Machine Plaza", Disc(new Vector2(-21.2f, 0f), 3.1f, 21, Next()), cobble);
        Flat(parent, "Bonfire Plaza", Disc(new Vector2(Fire.x, Fire.z), 3.4f, 22, Next()), cobble);
        Flat(parent, "Story Plaza", Disc(new Vector2(Rug.x, Rug.z), 2.9f, 23, Next()), cobble);
        Flat(parent, "Kitchen Plaza", Disc(new Vector2(Kitchen.x, Kitchen.z), 2.7f, 24, Next()), cobble);
        Flat(parent, "Workshop Plaza", Disc(new Vector2(-3.5f, 0f), 2.1f, 25, Next()), cobble);
        Flat(parent, "Bonfire Soot", Disc(new Vector2(Fire.x, Fire.z), 1.5f, 26, Next()), soot);

        // Darker earth here and there, so the floor isn't one flat colour
        var rng = new System.Random(808);
        Vector2[] patches = { new Vector2(-9f, 6f), new Vector2(4f, 5.5f), new Vector2(-7f, -6.5f), new Vector2(2.5f, -5f), new Vector2(-23f, -8f), new Vector2(-23.5f, 7.5f), new Vector2(7.5f, -8.5f), new Vector2(8f, 8f) };
        float low = 0.022f;
        foreach (var p in patches)
        {
            low += 0.001f;
            Flat(parent, "Earth Patch", Disc(p, Range(rng, 1.6f, 2.6f), rng.Next(), low), dirt);
        }
    }

    static void Flat(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.isStatic = true;
    }

    // A flat strip along a path with wobbly edges
    static Mesh Ribbon(Vector2[] path, float width, int seed, float y)
    {
        var pts = new List<Vector2>();
        for (int s = 0; s < path.Length - 1; s++)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(path[s], path[s + 1]) / 0.4f));
            for (int k = 0; k < count; k++) pts.Add(Vector2.Lerp(path[s], path[s + 1], k / (float)count));
        }
        pts.Add(path[path.Length - 1]);

        var verts = new List<Vector3>();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(pts.Count - 1, i + 1)];
            Vector2 t = (next - prev).normalized, n = new Vector2(-t.y, t.x);
            float taper = i == 0 || i == pts.Count - 1 ? 0.75f : 1f;
            float wl = width * 0.5f * taper * (1f + (Mathf.PerlinNoise(seed * 0.17f + i * 0.45f, 0.3f) - 0.5f) * 0.45f);
            float wr = width * 0.5f * taper * (1f + (Mathf.PerlinNoise(seed * 0.17f + i * 0.45f, 7.9f) - 0.5f) * 0.45f);
            Vector2 l = pts[i] + n * wl, r = pts[i] - n * wr;
            verts.Add(new Vector3(l.x, y, l.y)); verts.Add(new Vector3(r.x, y, r.y));
        }
        var tris = new List<int>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            AddUp(verts, tris, a, c, b); AddUp(verts, tris, b, c, d);
        }
        return SaveFlat("Path", verts, tris);
    }

    // A flat, irregular round patch
    static Mesh Disc(Vector2 center, float radius, int seed, float y)
    {
        const int sides = 22;
        var verts = new List<Vector3> { new Vector3(center.x, y, center.y) };
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            float r = radius * (1f + (Mathf.PerlinNoise(seed * 0.13f + Mathf.Cos(a) * 1.3f, seed * 0.07f + Mathf.Sin(a) * 1.3f) - 0.5f) * 0.5f);
            verts.Add(new Vector3(center.x + Mathf.Cos(a) * r, y, center.y + Mathf.Sin(a) * r));
        }
        var tris = new List<int>();
        for (int i = 0; i < sides; i++) AddUp(verts, tris, 0, 1 + i, 1 + (i + 1) % sides);
        return SaveFlat("Patch", verts, tris);
    }

    // Adds a triangle wound so it faces up
    static void AddUp(List<Vector3> v, List<int> tris, int a, int b, int c)
    {
        if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).y < 0f) (b, c) = (c, b);
        tris.Add(a); tris.Add(b); tris.Add(c);
    }

    static Mesh SaveFlat(string name, List<Vector3> verts, List<int> tris)
    {
        var mesh = new Mesh { name = $"{name} {meshCount++}" };
        mesh.SetVertices(verts);
        mesh.SetNormals(Enumerable.Repeat(Vector3.up, verts.Count).ToList());
        mesh.SetUVs(0, verts.Select(v => new Vector2(v.x, v.z) * 0.5f).ToList());
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        if (meshContainer == null) { AssetDatabase.CreateAsset(mesh, MeshAssetPath); meshContainer = mesh; }
        else AssetDatabase.AddObjectToAsset(mesh, meshContainer);
        return mesh;
    }

    // ---------- Areas ----------

    static void Camp(Transform parent)
    {
        var fireFrames = Enumerable.Range(0, 4).Select(f => Art("Bonfire_" + (f + 1), 48, 60, t => BonfirePixels(t, f))).ToArray();
        var fire = new GameObject("Bonfire");
        fire.transform.SetParent(parent, false); fire.transform.localPosition = Fire;
        var art = SpriteChild(fire.transform, fireFrames[0], 1f, 2);
        var anim = fire.AddComponent<CavemanSpriteAnimation>(); anim.sprite = art; anim.frames = fireFrames; anim.framesPerSecond = 8f;
        AddLight(fire.transform, new Vector3(0, 1.1f, 0), Hex("FF9B42"), 4f, 9f, 0.18f);
        Solid(fire.transform, 0.75f, 1f);
        FlatSprite(parent, "Fire Stones", Fire + new Vector3(0, 0.07f, 0), Art("FireRing", 44, 28, StoneRingPixels), 1.15f);

        Place(parent, "CavemanLog", Fire + new Vector3(-2.7f, 0, -1.4f));
        Place(parent, "CavemanLog", Fire + new Vector3(2.6f, 0, 1.3f));
        Place(parent, "CavemanLog", Fire + new Vector3(0.6f, 0, 2.7f));
        Prop(parent, "Drum", Fire + new Vector3(-3.3f, 0, 2.2f), Art("Drum", 18, 18, DrumPixels), 0.9f, 0.3f);
        Prop(parent, "Water Pot", Fire + new Vector3(-3.9f, 0, 1.0f), Art("WaterPot", 20, 24, t => ZigzagPotPixels(t, true)), 0.85f, 0.3f);
        Prop(parent, "Grain Pot", Fire + new Vector3(3.6f, 0, 2.3f), Art("ZigzagPot", 20, 24, t => ZigzagPotPixels(t, false)), 0.9f, 0.3f);
        Prop(parent, "Mammoth Tusk Arch", new Vector3(-14.5f, 0, 2.0f), Art("TuskArch", 64, 52, TuskArchPixels), 1f, -1f);
        foreach (float side in new[] { -1.75f, 1.75f })
        {
            var post = new GameObject("Tusk Post"); post.transform.SetParent(parent, false); post.transform.localPosition = new Vector3(-14.5f + side, 0, 2.0f);
            var box = post.AddComponent<BoxCollider>(); box.center = new Vector3(0, BlockHeight * 0.5f, 0); box.size = new Vector3(0.45f, BlockHeight, 0.45f);
        }

        var drummer = Villager(parent, "Drummer", Fire + new Vector3(-2.4f, 0, 1.2f), Look.Drummer, Frames("Drummer", Look.Drummer, Pose.Drum, 2), 4f,
            "Boom. Boom-boom. The fire dances when I drum.", "You walk strange. Like the wind forgot you.");
        drummer.puffSprites = new[] { Art("NoteA", 9, 12, NotePixels), Art("NoteB", 12, 12, DoubleNotePixels) };
        drummer.puffInterval = 0.75f;
        Villager(parent, "Dancer", Fire + new Vector3(1.9f, 0, -1.2f), Look.DancerA, Frames("DancerA", Look.DancerA, Pose.Dance, 4), 4f,
            "Dance with us! The fire keeps the cold spirits away.", "When the drum stops, I stop. Ha!");
        Villager(parent, "Dancer", Fire + new Vector3(-1.3f, 0, -1.9f), Look.DancerB, Frames("DancerB", Look.DancerB, Pose.Dance, 4), 4.5f,
            "Hop, hop! My feet are warm!", "Big hunt soon. Tonight we dance for it.");
        var child = Villager(parent, "Running Child", Fire + new Vector3(3.6f, 0, 0f), Look.Kid, Frames("Kid", Look.Kid, Pose.Walk, 2), 6f,
            "You have no fur! Are you cold?", "I'm the fastest in the whole cave! Catch me!");
        child.transform.localScale = Vector3.one * 0.72f;
        child.orbitCenter = Fire; child.orbitRadius = 3.6f; child.orbitSpeed = 0.55f;
        UnityEngine.Object.DestroyImmediate(child.transform.Find("Solid").gameObject);

        Villager(parent, "Painter", new Vector3(-6.2f, 0, 10.0f), Look.Painter, Frames("Painter", Look.Painter, Pose.Paint, 2), 2f,
            "I paint the great hunt. Beat the boar, and I'll paint you too.", "Red from the earth, black from the fire. Don't touch, still wet!");
    }

    static void Elder(Transform parent)
    {
        FlatSprite(parent, "Bear Skin Rug", Rug + new Vector3(0, 0.07f, 0.2f), Art("BearRug", 48, 40, RugPixels), 1.3f);
        FlatSprite(parent, "Sitting Stone", Rug + new Vector3(-1.5f, 0.08f, -0.7f), Art("SitSlab", 24, 16, SlabPixels), 1f);
        FlatSprite(parent, "Sitting Stone", Rug + new Vector3(1.6f, 0.08f, -0.5f), Art("SitSlab", 24, 16, SlabPixels), 1f, 25f);
        Villager(parent, "Elder", Rug + new Vector3(0, 0, 1.0f), Look.Elder, Blink("Elder", Look.Elder, Pose.Staff), 2.5f,
            "Ahh... the stranger who fell out of the humming machine.", "The glowing stones sleep deep in the cave, past the thorns.", "Thorns fear the axe. Make one at the flat rock.");
        var kid = Villager(parent, "Listening Child", Rug + new Vector3(-1.5f, 0, -0.7f), Look.KidB, Blink("KidB", Look.KidB, Pose.Seated), 2.5f,
            "Shh! Grandfather is telling the old stories.");
        kid.transform.localScale = Vector3.one * 0.72f;
        Villager(parent, "Listener", Rug + new Vector3(1.6f, 0, -0.5f), Look.Listener, Blink("Listener", Look.Listener, Pose.Seated), 2.5f,
            "The boar took my brother's spear. And his sandal.", "Grandfather says the sky was once a big fire.");
        Prop(parent, "Water Pot", Rug + new Vector3(-2.4f, 0, 1.2f), Art("WaterPot", 20, 24, t => ZigzagPotPixels(t, true)), 0.85f, 0.3f);
        Prop(parent, "Fern Pot", Rug + new Vector3(2.5f, 0, 1.6f), Art("FernPot", 24, 34, FernPotPixels), 0.9f, 0.3f);
        Prop(parent, "Bones", Rug + new Vector3(2.4f, 0, -1.7f), Art("BonePile", 26, 16, BonePixels), 0.6f, 0f);
    }

    static void KitchenArea(Transform parent)
    {
        Villager(parent, "Seed Grinder", Kitchen, Look.Grinder, Frames("Grinder", Look.Grinder, Pose.Grind, 2), 3f,
            "Grinding seeds for flatbread. Hungry? No? More for me.", "Fish from the cold river, berries from the hill. The cave never starves.");
        var oven = Prop(parent, "Clay Oven", new Vector3(-21.2f, 0, -7.2f), Art("ClayOven", 40, 34, OvenPixels), 1f, 0.9f);
        AddLight(oven.transform, new Vector3(0, 0.5f, -0.6f), Hex("FF8A3A"), 2.2f, 4.5f, 0.2f);
        Prop(parent, "Berry Basket", Kitchen + new Vector3(1.7f, 0, -0.8f), Art("BerryBasket", 26, 22, t => FruitBasketPixels(t, 0)), 0.9f, 0.3f);
        Prop(parent, "Fruit Basket", Kitchen + new Vector3(2.6f, 0, 0.3f), Art("FruitBasket", 26, 22, t => FruitBasketPixels(t, 1)), 0.9f, 0.3f);
        Prop(parent, "Fish Basket", Kitchen + new Vector3(-1.8f, 0, 1.4f), Art("FishBasket", 26, 22, t => FruitBasketPixels(t, 2)), 0.9f, 0.3f);
        Prop(parent, "Water Pot", Kitchen + new Vector3(-2.4f, 0, -2.0f), Art("WaterPot", 20, 24, t => ZigzagPotPixels(t, true)), 0.85f, 0.3f);
        Prop(parent, "Grain Pot", Kitchen + new Vector3(-1.5f, 0, -2.5f), Art("ZigzagPot", 20, 24, t => ZigzagPotPixels(t, false)), 0.9f, 0.3f);
        Prop(parent, "Grain Pot", Kitchen + new Vector3(3.2f, 0, -2.2f), Art("ZigzagPot", 20, 24, t => ZigzagPotPixels(t, false)), 0.9f, 0.3f);
        Place(parent, "ClayPot", Kitchen + new Vector3(-0.6f, 0, -2.4f));
        Place(parent, "WovenBasket", Kitchen + new Vector3(0.6f, 0, 1.6f));
        Prop(parent, "Fern Pot", new Vector3(-23.4f, 0, -5.6f), Art("FernPot", 24, 34, FernPotPixels), 0.9f, 0.3f);
        Prop(parent, "Fern Pot", new Vector3(-23.6f, 0, 3.8f), Art("FernPot", 24, 34, FernPotPixels), 0.9f, 0.3f);
        Prop(parent, "Drying Rack", new Vector3(-8.6f, 0, -7.8f), Art("DryingRack", 40, 36, RackPixels), 1f, 0f);
        Villager(parent, "Gatherer", new Vector3(-7.2f, 0, -6.8f), Look.Gatherer, Blink("Gatherer", Look.Gatherer, Pose.Stand), 2.5f,
            "Hides dry best near the fire. Fish too. Smells good, yes?", "Out there, far away, giant hairy beasts with long teeth. Here, we are safe.");
    }

    static void Workshop(Transform parent)
    {
        FlatSprite(parent, "Knapping Stone", new Vector3(-3.2f, 0.08f, -3.7f), Art("SitSlab", 24, 16, SlabPixels), 1.1f, -15f);
        Villager(parent, "Flint Knapper", new Vector3(-3.2f, 0, -3.6f), Look.Knapper, Frames("Knapper", Look.Knapper, Pose.Knap, 2), 3f,
            "Two wood, two stone. Strike them together on the flat rock, and you have an axe.", "Careful. Flint bites.");
        FlatSprite(parent, "Sleeping Fur", new Vector3(0.6f, 0.07f, -8.4f), Art("BearRug", 48, 40, RugPixels), 0.75f, 80f);
        FlatSprite(parent, "Sleeping Fur", new Vector3(-2.2f, 0.075f, -8.8f), Art("BearRug", 48, 40, RugPixels), 0.7f, 100f);
        Prop(parent, "Bones", new Vector3(2.4f, 0, -9.4f), Art("BonePile", 26, 16, BonePixels), 0.6f, 0f);
        Prop(parent, "Fern Pot", new Vector3(-0.4f, 0, 9.6f), Art("FernPot", 24, 34, FernPotPixels), 0.9f, 0.3f);
    }

    static void Hunters(Transform parent)
    {
        Villager(parent, "Hunter", new Vector3(6.6f, 0, -4.6f), Look.Hunter, Blink("Hunter", Look.Hunter, Pose.Spear), 2.5f,
            "That boar is angry. Hit it with an axe, then step back before it charges.", "Bare hands? Ha! The boar will laugh at you.");
        // The hunters' own small fire, with a spear maker beside it
        var fireAt = new Vector3(7.2f, 0, -7.6f);
        Place(parent, "CavemanCampfire", fireAt);
        FlatSprite(parent, "Fire Stones", fireAt + new Vector3(0, 0.07f, 0), Art("FireRing", 44, 28, StoneRingPixels), 0.7f);
        Villager(parent, "Spear Maker", fireAt + new Vector3(-1.6f, 0, 0.4f), Look.Knapper2, Frames("SpearMaker", Look.Knapper2, Pose.Knap, 2), 3f,
            "A good spear needs sharp flint. Like your axe will.", "When the boar is gone, we all eat well tonight!");
        Place(parent, "CavemanLog", fireAt + new Vector3(1.5f, 0, 0.6f));
        Prop(parent, "Bones", new Vector3(8.6f, 0, -5.6f), Art("BonePile", 26, 16, BonePixels), 0.6f, 0f);
        Prop(parent, "Drying Rack", new Vector3(5.8f, 0, 7.6f), Art("DryingRack", 40, 36, RackPixels), 1f, 0f);
        Prop(parent, "Bones", new Vector3(7.4f, 0, 5.6f), Art("BonePile", 26, 16, BonePixels), 0.6f, 0f);
        Prop(parent, "Grain Pot", new Vector3(4.4f, 0, 7.2f), Art("ZigzagPot", 20, 24, t => ZigzagPotPixels(t, false)), 0.9f, 0.3f);
    }

    static void Pillars(Transform parent)
    {
        var charm = Art("Dreamcatcher", 16, 30, DreamcatcherPixels);
        Vector3[] spots = { new Vector3(-8.8f, 0, 8.8f), new Vector3(2.8f, 0, 9.4f), new Vector3(-12.4f, 0, -8.6f), new Vector3(3.4f, 0, -6.6f), new Vector3(-24f, 0, -1.8f) };
        var rng = new System.Random(31);
        foreach (var at in spots)
        {
            var root = new GameObject("Wooden Pillar"); root.transform.SetParent(parent, false); root.transform.localPosition = at;
            root.transform.localRotation = Quaternion.Euler(0, Range(rng, 0, 360), 0);
            float h = Range(rng, 3.1f, 3.6f);
            Part(PrimitiveType.Cylinder, "Trunk", root.transform, new Vector3(0, h * 0.5f, 0), new Vector3(0.62f, h * 0.5f, 0.62f), bark);
            Part(PrimitiveType.Cylinder, "Rope Band", root.transform, new Vector3(0, 1.1f, 0), new Vector3(0.74f, 0.16f, 0.74f), rope);
            Part(PrimitiveType.Cylinder, "Rope Band", root.transform, new Vector3(0, 1.45f, 0), new Vector3(0.72f, 0.1f, 0.72f), rope);
            Part(PrimitiveType.Cylinder, "Rope Band", root.transform, new Vector3(0, h - 0.5f, 0), new Vector3(0.72f, 0.14f, 0.72f), rope);
            Part(PrimitiveType.Cylinder, "Branch Stub", root.transform, new Vector3(0.36f, h * 0.7f, 0), new Vector3(0.2f, 0.22f, 0.2f), bark).transform.localRotation = Quaternion.Euler(0, 0, -60);
            Part(PrimitiveType.Sphere, "Base Stone", root.transform, new Vector3(0.35f, 0.1f, 0.2f), new Vector3(0.5f, 0.3f, 0.45f), stone);
            Part(PrimitiveType.Sphere, "Base Stone", root.transform, new Vector3(-0.3f, 0.08f, -0.25f), new Vector3(0.42f, 0.25f, 0.4f), stone);
            var c = new GameObject("Charm"); c.transform.SetParent(root.transform, false);
            c.transform.position = at + new Vector3(0, 1.0f, -0.42f);
            var r = c.AddComponent<SpriteRenderer>(); r.sprite = charm; r.sharedMaterial = spriteMat; r.sortingOrder = 2; c.AddComponent<Billboard>();
            Solid(root.transform, 0.36f, h);
        }
    }

    static void Torches(Transform parent)
    {
        var frames = Enumerable.Range(0, 4).Select(f => Art("Torch_" + (f + 1), 12, 36, t => TorchPixels(t, f))).ToArray();
        Vector3[] spots =
        {
            new Vector3(-19.2f, 0, 2.5f), new Vector3(-19.2f, 0, -2.5f), new Vector3(-9f, 0, 1.8f), new Vector3(-1f, 0, -1.8f),
            new Vector3(5f, 0, 1.8f), new Vector3(10.1f, 0, 2.7f), new Vector3(10.1f, 0, -2.7f), new Vector3(-11.6f, 0, 8.6f),
            new Vector3(-21.4f, 0, 9.4f), new Vector3(1.2f, 0, 8.4f), new Vector3(-22.4f, 0, -5.4f), new Vector3(-5.8f, 0, -8.4f), new Vector3(5.2f, 0, -8f),
        };
        foreach (var at in spots)
        {
            var torch = Prop(parent, "Torch", at, frames[0], 1f, 0.15f);
            var anim = torch.AddComponent<CavemanSpriteAnimation>(); anim.sprite = torch.GetComponentInChildren<SpriteRenderer>(); anim.frames = frames; anim.framesPerSecond = 8f;
            AddLight(torch.transform, new Vector3(0, 2.1f, 0), Hex("FF9E47"), 2.6f, 6.5f, 0.22f);
        }
    }

    // Cave paintings on the cavern's back wall: each is placed where rays hit the perimeter mountain
    static void Paintings(Transform level, Transform parent)
    {
        var wall = level.GetComponentsInChildren<MeshCollider>(true).FirstOrDefault(c => c.name == "Perimeter Mountains");
        Physics.SyncTransforms();
        var hunt = Art("PaintingHunt", 96, 40, t => PaintingPixels(t, 0));
        var herd = Art("PaintingHerd", 96, 40, t => PaintingPixels(t, 1));
        (float x, Sprite art)[] spots = { (-17f, hunt), (-6.2f, herd), (5.5f, hunt) };
        foreach (var (x, art) in spots)
        {
            Vector3 origin = new Vector3(x, 1.8f, 6f);
            Vector3 pos = new Vector3(x, 1.8f, 11.0f), normal = Vector3.back;
            if (wall != null && wall.Raycast(new Ray(origin, Vector3.forward), out RaycastHit hit, 20f))
            {
                // Average the wall's facing over the painting's width, so it leans with the rock
                Vector3 n = hit.normal; int count = 1;
                foreach (float dx in new[] { -2.2f, 2.2f })
                    if (wall.Raycast(new Ray(origin + Vector3.right * dx, Vector3.forward), out RaycastHit side, 20f)) { n += side.normal; count++; }
                normal = (n / count).normalized;
                normal.y = Mathf.Clamp(normal.y, -0.1f, 0.45f);
                normal.Normalize();
                pos = hit.point + normal * 0.18f;
            }
            else Debug.LogWarning("[CavemanSettlement] No wall found for the cave painting at x = " + x);
            var go = new GameObject("Cave Painting"); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.LookRotation(-normal);
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = art; r.sharedMaterial = spriteMat; r.sortingOrder = -1;
        }
    }

    static void Stalagmites(Transform parent)
    {
        Vector3[] spots = { new Vector3(-25f, 0, 7.5f), new Vector3(-24.6f, 0, -8.8f), new Vector3(-9.8f, 0, -10.6f), new Vector3(8.6f, 0, 9.8f), new Vector3(9.4f, 0, -9.8f), new Vector3(-0.4f, 0, 10.9f), new Vector3(-23.6f, 0, 10f), new Vector3(-13.2f, 0, 10.6f) };
        var rng = new System.Random(5);
        foreach (var at in spots)
        {
            var go = new GameObject("Stalagmite"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = CavemanNature.StalagmiteMesh(rng.Next());
            go.AddComponent<MeshRenderer>().sharedMaterials = CavemanNature.WallMaterials;
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(Range(rng, 0.6f, 0.9f), Range(rng, 1.3f, 2.3f), Range(rng, 0.6f, 0.9f));
            go.transform.localRotation = Quaternion.Euler(Range(rng, -5, 5), Range(rng, 0, 360), Range(rng, -5, 5));
            var col = go.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 0.5f, 0); col.radius = 0.4f; col.height = 1f;
            go.isStatic = true;
        }
    }

    // ---------- Object helpers ----------

    static Transform Group(Transform parent, string name) { var g = new GameObject(name).transform; g.SetParent(parent, false); return g; }

    static void Place(Transform parent, string prefab, Vector3 at)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/Caveman/" + prefab + ".prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
        go.transform.SetParent(parent, false); go.transform.localPosition = at;
        // Sprite props from CavemanSceneBuilder (logs, pots, the small campfire) have no collider: block their footprint
        if (!go.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger))
        {
            var r = go.GetComponentInChildren<Renderer>();
            float width = r != null ? Mathf.Max(0.5f, r.bounds.size.x * 0.75f) : 0.8f;
            Block(go.transform, width, Mathf.Min(width, 0.8f));
        }
    }

    static SpriteRenderer SpriteChild(Transform parent, Sprite art, float scale, int order)
    {
        var go = new GameObject("Sprite"); go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, art.bounds.extents.y * scale, 0);
        go.transform.localScale = Vector3.one * scale;
        var r = go.AddComponent<SpriteRenderer>(); r.sprite = art; r.sharedMaterial = spriteMat; r.sortingOrder = order;
        go.AddComponent<Billboard>();
        return r;
    }

    // A standing sprite prop with a tall blocker over its footprint (at least solidRadius, else most of the
    // sprite's width), so the player can neither walk through nor jump over it. solidRadius < 0 = walk-through
    static GameObject Prop(Transform parent, string name, Vector3 at, Sprite art, float scale, float solidRadius)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.localPosition = at;
        SpriteChild(root.transform, art, scale, 1);
        if (solidRadius >= 0f)
        {
            float width = Mathf.Max(solidRadius * 2f, art.bounds.size.x * scale * 0.75f, 0.5f);
            Block(root.transform, width, Mathf.Clamp(width, 0.5f, 0.9f));
        }
        return root;
    }

    // Taller than any jump (MedievalJump reaches 1.6), so blockers can't be hopped over or stood on
    const float BlockHeight = 6f;

    static void Block(Transform parent, float width, float depth)
    {
        var go = new GameObject("Solid"); go.transform.SetParent(parent, false);
        var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(width, BlockHeight, depth); box.center = new Vector3(0, BlockHeight * 0.5f, 0);
    }

    // Everything else that stands in the level (rocks, firewood, the crafting stone, the time machine, boulders,
    // stalagmites, the bramble) only had knee-high colliders: raise them all to Block Height.
    // Breakables keep it on their own collider (so breaking them clears the way); the rest get a world-aligned box
    static void RaiseObstacles(Transform level)
    {
        Physics.SyncTransforms();
        var blockers = Group(level, "Tall Blockers");
        string[] skip = { "Cave Earth Floor", "Dark Cave Floor" };
        foreach (var c in level.GetComponentsInChildren<Collider>(true).ToList())
        {
            if (c.isTrigger || skip.Contains(c.name) || c.transform.IsChildOf(blockers)) continue;
            if (c is MeshCollider mesh && !mesh.convex) continue; // the mountains: see CavemanNature's invisible walls
            if (c.GetComponentInParent<WildBoar>() != null) continue;
            if (c.bounds.max.y >= BlockHeight - 0.05f) continue;

            var breakable = c.GetComponentInParent<Breakable>();
            if (breakable != null)
            {
                if (c is BoxCollider box)
                {
                    float scaleY = Mathf.Max(0.01f, box.transform.lossyScale.y);
                    box.size = new Vector3(box.size.x, BlockHeight / scaleY, box.size.z);
                    box.center = new Vector3(box.center.x, box.size.y * 0.5f, box.center.z);
                }
                continue;
            }

            Bounds b = c.bounds;
            var go = new GameObject("Blocker " + c.name); go.transform.SetParent(blockers, false);
            go.transform.position = new Vector3(b.center.x, BlockHeight * 0.5f, b.center.z);
            go.AddComponent<BoxCollider>().size = new Vector3(Mathf.Max(0.4f, b.size.x * 0.85f), BlockHeight, Mathf.Max(0.4f, b.size.z * 0.85f));
        }
    }

    // A sprite lying flat on the floor (rugs, slabs, the fire's stone ring), drawn under standing sprites
    static void FlatSprite(Transform parent, string name, Vector3 at, Sprite art, float scale, float turn = 0f)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.transform.localPosition = at; go.transform.localRotation = Quaternion.Euler(90f, turn, 0f); go.transform.localScale = Vector3.one * scale;
        var r = go.AddComponent<SpriteRenderer>(); r.sprite = art; r.sharedMaterial = spriteMat; r.sortingOrder = -2;
    }

    // Round blocker for villagers, pillars and the bonfire (height is always Block Height)
    static void Solid(Transform parent, float radius, float height)
    {
        var go = new GameObject("Solid"); go.transform.SetParent(parent, false);
        var c = go.AddComponent<CapsuleCollider>(); c.radius = radius; c.height = BlockHeight; c.center = new Vector3(0, BlockHeight * 0.5f, 0);
    }

    static void AddLight(Transform parent, Vector3 at, Color color, float intensity, float range, float flicker)
    {
        var go = new GameObject("Light"); go.transform.SetParent(parent, false); go.transform.localPosition = at;
        var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        var f = parent.gameObject.AddComponent<TorchFlicker>(); f.flame = l; f.flickerAmount = flicker; f.flickerSpeed = 6f;
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        // Round cylinders shade in smooth gradients; an 8-sided column gives one flat tone per face, like pixel art
        if (type == PrimitiveType.Cylinder) go.GetComponent<MeshFilter>().sharedMesh = PrismMesh();
        return go;
    }

    static Mesh prism;

    // Same size as Unity's cylinder (radius 0.5, y from -1 to 1), but 8 flat faces with hard edges.
    // UVs wrap like the cylinder's (u around, v up), so the bark and rope textures tile the same way
    static Mesh PrismMesh()
    {
        if (prism != null) return prism;
        const int sides = 8;
        var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int s = 0; s < sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.5f, p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 0.5f;
            Vector3 n = ((p0 + p1) * 0.5f).normalized;
            int i = verts.Count;
            verts.AddRange(new[] { p0 + Vector3.down, p1 + Vector3.down, p1 + Vector3.up, p0 + Vector3.up });
            normals.AddRange(new[] { n, n, n, n });
            float u0 = s / (float)sides, u1 = (s + 1) / (float)sides;
            uvs.AddRange(new[] { new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1) });
            // Front faces wind clockwise seen from outside
            if (Vector3.Dot(Vector3.Cross(verts[i + 1] - verts[i], verts[i + 2] - verts[i]), n) > 0f) tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            else tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            foreach (float y in new[] { 1f, -1f })
            {
                int c = verts.Count; Vector3 up = Vector3.up * y;
                verts.AddRange(new[] { up, p0 + up, p1 + up }); normals.AddRange(new[] { up, up, up });
                uvs.AddRange(new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f + p0.x, 0.5f + p0.z), new Vector2(0.5f + p1.x, 0.5f + p1.z) });
                if (Vector3.Dot(Vector3.Cross(verts[c + 1] - verts[c], verts[c + 2] - verts[c]), up) > 0f) tris.AddRange(new[] { c, c + 1, c + 2 });
                else tris.AddRange(new[] { c, c + 2, c + 1 });
            }
        }
        prism = new Mesh { name = "Pixel Column" };
        prism.SetVertices(verts); prism.SetNormals(normals); prism.SetUVs(0, uvs); prism.SetTriangles(tris, 0);
        prism.RecalculateBounds(); prism.RecalculateTangents();
        if (meshContainer == null) { AssetDatabase.CreateAsset(prism, MeshAssetPath); meshContainer = prism; }
        else AssetDatabase.AddObjectToAsset(prism, meshContainer);
        return prism;
    }

    static CaveNpc Villager(Transform parent, string name, Vector3 at, Look look, Sprite[] frames, float fps, params string[] lines)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.localPosition = at;
        var talk = root.AddComponent<SphereCollider>(); talk.isTrigger = true; talk.center = new Vector3(0, 0.7f, 0); talk.radius = 1.5f;
        var sprite = SpriteChild(root.transform, frames[0], 1f, 2);
        var shadow = sprite.gameObject.AddComponent<SpriteShadowCaster>(); shadow.shadowMaterial = shadowCasterMat;
        var npc = root.AddComponent<CaveNpc>(); npc.sprite = sprite; npc.frames = frames; npc.framesPerSecond = fps; npc.lines = lines;
        Solid(root.transform, 0.35f, 1.4f);
        return npc;
    }

    // ---------- Pixel art helpers ----------

    static readonly Dictionary<string, Sprite> made = new Dictionary<string, Sprite>();

    // Draws (once per build) and outlines a sprite at 16 px per unit
    static Sprite Art(string name, int w, int h, Action<Texture2D> draw, float ppu = 16f)
    {
        string key = "Settlement/" + name;
        if (made.TryGetValue(key, out var s) && s != null) return s;
        if (!AssetDatabase.IsValidFolder(Sprites + "/Settlement")) AssetDatabase.CreateFolder(Sprites, "Settlement");
        s = B.Sprite("Settlement/" + name, w, h, t => { draw(t); Outline(t); }, ppu);
        made[key] = s;
        return s;
    }

    // Dark outline around every opaque shape, like hand-made pixel art
    static void Outline(Texture2D t)
    {
        var px = t.GetPixels();
        int w = t.width, h = t.height;
        bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a > 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (!Solid(x, y) && (Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1)))
                    t.SetPixel(x, y, Ink);
    }

    static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out Color c); return c; }
    static void R(Texture2D t, int x, int y, int w, int h, Color c) => B.Rect(t, x, y, w, h, c);
    static void P(Texture2D t, int x, int y, Color c) => B.Pixel(t, x, y, c);
    static void E(Texture2D t, int x, int y, int w, int h, Color c) => B.Ellipse(t, x, y, w, h, c);
    static void Poly(Texture2D t, Color c, params int[] xy)
    {
        var pts = new Vector2Int[xy.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2Int(xy[i * 2], xy[i * 2 + 1]);
        B.Polygon(t, c, pts);
    }
    static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

    // ---------- Villager art ----------

    enum Pose { Stand, Dance, Drum, Seated, Grind, Spear, Staff, Paint, Walk, Knap }

    struct Look
    {
        public Color skin, skinShade, hair, hairShade, fur, furShade, furSpot, band;
        public int style;     // 0 messy, 1 long, 2 topknot, 3 bald with beard
        public bool feather, hasBand;

        static Look Make(string skin, string skinShade, string hair, string hairShade, string fur, string furShade, string spot, int style, string band = null, bool feather = false)
            => new Look
            {
                skin = Hex(skin), skinShade = Hex(skinShade), hair = Hex(hair), hairShade = Hex(hairShade),
                fur = Hex(fur), furShade = Hex(furShade), furSpot = Hex(spot), style = style,
                hasBand = band != null, band = band != null ? Hex(band) : Clear, feather = feather,
            };

        public static Look Drummer => Make("C98C5E", "A86E45", "A0502C", "7A3A20", "B07A48", "8A5A33", "6E4A2F", 0, "E3C38A");
        public static Look DancerA => Make("F0C29A", "D49A72", "E3C36A", "BF9A45", "D2B48A", "AE9068", "8A6A48", 1, "C8402F");
        public static Look DancerB => Make("8A5634", "6C3F25", "2E2420", "1E1714", "B07A48", "8A5A33", "6E4A2F", 0, null, true);
        public static Look Kid => Make("F0C29A", "D49A72", "6E4A2F", "4A3222", "8E8A82", "6E6A64", "4E4A46", 0);
        public static Look KidB => Make("C98C5E", "A86E45", "2E2420", "1E1714", "D2B48A", "AE9068", "8A6A48", 2, "3B5EA8");
        public static Look Elder => Make("C98C5E", "A86E45", "E8E2CF", "BDB6A3", "6E4A2F", "4F3420", "3A2618", 3);
        public static Look Listener => Make("F0C29A", "D49A72", "2E2420", "1E1714", "6E4A2F", "4F3420", "3A2618", 1);
        public static Look Painter => Make("C98C5E", "A86E45", "6E4A2F", "4A3222", "D2B48A", "AE9068", "8A6A48", 2, "9C3B22");
        public static Look Grinder => Make("8A5634", "6C3F25", "2E2420", "1E1714", "D2B48A", "AE9068", "8A6A48", 1, "E3C38A");
        public static Look Knapper => Make("F0C29A", "D49A72", "A0502C", "7A3A20", "8E8A82", "6E6A64", "4E4A46", 0, null, true);
        public static Look Hunter => Make("C98C5E", "A86E45", "2E2420", "1E1714", "6E4A2F", "4F3420", "3A2618", 0, "C8402F", true);
        public static Look Knapper2 => Make("8A5634", "6C3F25", "6E4A2F", "4A3222", "B07A48", "8A5A33", "6E4A2F", 2, "E3C38A");
        public static Look Gatherer => Make("F0C29A", "D49A72", "E3C36A", "BF9A45", "B07A48", "8A5A33", "6E4A2F", 2);
    }

    static Sprite[] Frames(string name, Look look, Pose pose, int count)
        => Enumerable.Range(0, count).Select(f => Art(name + "_" + (f + 1), 22, 30, t => Person(t, look, pose, f, false), NpcPpu)).ToArray();

    // Idle with an occasional blink: open, open, open, open, open, blink
    static Sprite[] Blink(string name, Look look, Pose pose)
    {
        var open = Art(name + "_1", 22, 30, t => Person(t, look, pose, 0, false), NpcPpu);
        var shut = Art(name + "_2", 22, 30, t => Person(t, look, pose, 1, true), NpcPpu);
        return new[] { open, open, open, open, open, shut };
    }

    // A chibi cave person on a 22 x 30 canvas (1px margin for the outline), facing the camera
    // (or away, for the painter). b = the row the fur tunic's hem sits on
    static void Person(Texture2D t, Look k, Pose pose, int frame, bool blink)
    {
        bool sit = pose == Pose.Drum || pose == Pose.Seated || pose == Pose.Grind || pose == Pose.Staff || pose == Pose.Knap;
        bool back = pose == Pose.Paint;
        int lift = pose == Pose.Dance && frame % 2 == 1 ? 1 : 0;
        int b = (sit ? 3 : 5) + lift;
        Color wrap = Hex("4A3222");

        // Long hair hangs behind the body
        if (k.style == 1) { R(t, 4, b + 3, 14, 12, k.hair); R(t, 15, b + 3, 3, 12, k.hairShade); }

        // Legs
        if (sit)
        {
            R(t, 4, 1, 14, 3, k.skin); R(t, 14, 1, 4, 3, k.skinShade);
            R(t, 3, 1, 2, 2, wrap); R(t, 17, 1, 2, 2, wrap);
        }
        else
        {
            int l = pose == Pose.Walk && frame == 0 ? 1 : 0, r = pose == Pose.Walk && frame == 1 ? 1 : 0;
            R(t, 8, 3 + l + lift, 2, 2, k.skin); R(t, 12, 3 + r + lift, 2, 2, k.skinShade);
            R(t, 7, 1 + l + lift, 3, 2, wrap); R(t, 12, 1 + r + lift, 3, 2, wrap);
        }

        // Fur tunic with a jagged hem, spots, a rope belt and one bare shoulder
        R(t, 6, b + 1, 10, 7, k.fur); R(t, 7, b + 8, 8, 1, k.fur);
        for (int x = 6; x <= 15; x += 2) P(t, x, b, k.fur);
        R(t, 14, b + 1, 2, 7, k.furShade);
        P(t, 8, b + 5, k.furSpot); P(t, 11, b + 2, k.furSpot); P(t, 13, b + 6, k.furSpot); P(t, 9, b + 1, k.furSpot); P(t, 12, b + 4, k.furSpot);
        R(t, 6, b + 3, 10, 1, Hex("8A5A33"));
        if (back) R(t, 7, b + 7, 2, 2, k.skin); else R(t, 13, b + 7, 2, 2, k.skin);
        R(t, 10, b + 9, 2, 1, k.skin);

        // Head
        E(t, 5, b + 9, 12, 11, k.skin);
        R(t, 7, b + 9, 8, 1, k.skinShade);
        if (back)
        {
            E(t, 5, b + 9, 12, 11, k.hair);
        }
        else
        {
            if (blink) { R(t, 7, b + 12, 2, 1, Ink); R(t, 13, b + 12, 2, 1, Ink); }
            else { R(t, 7, b + 12, 2, 2, Ink); R(t, 13, b + 12, 2, 2, Ink); P(t, 7, b + 13, Hex("F5EBDD")); P(t, 13, b + 13, Hex("F5EBDD")); }
            P(t, 6, b + 11, Hex("E39A8A")); P(t, 15, b + 11, Hex("E39A8A"));
            R(t, 10, b + 10, 2, 1, k.skinShade);
        }

        // Hair
        if (k.style == 3)
        {
            R(t, 4, b + 12, 2, 4, k.hair); R(t, 16, b + 12, 2, 4, k.hair);
            if (!back)
            {
                E(t, 6, b + 4, 10, 8, k.hair); R(t, 8, b + 10, 6, 1, k.hairShade);
                R(t, 7, b + 14, 2, 1, k.hair); R(t, 13, b + 14, 2, 1, k.hair);
                P(t, 10, b + 18, k.skinShade); P(t, 12, b + 17, k.skinShade);
            }
        }
        else
        {
            E(t, 4, b + 14, 14, 8, k.hair);
            for (int x = 6; x <= 15; x++) if (x % 3 == 0) P(t, x, b + 13, k.hair);
            Poly(t, k.hair, 6, b + 20, 8, b + 23, 10, b + 20);
            Poly(t, k.hair, 10, b + 21, 12, b + 24, 14, b + 21);
            Poly(t, k.hair, 13, b + 20, 17, b + 22, 16, b + 18);
            R(t, 4, b + 12, 2, 4, k.hair); R(t, 16, b + 12, 2, 4, k.hair);
            R(t, 15, b + 14, 2, 5, k.hairShade);
            R(t, 8, b + 20, 3, 1, Color.Lerp(k.hair, Color.white, 0.25f));
            if (k.style == 2) { E(t, 8, b + 20, 6, 5, k.hair); R(t, 9, b + 21, 4, 1, Hex("C49A5C")); }
            if (k.style == 1) { R(t, 4, b + 9, 2, 6, k.hair); R(t, 16, b + 9, 2, 6, k.hairShade); }
        }
        if (k.hasBand) R(t, 5, b + 16, 12, 1, k.band);
        if (k.feather) { Poly(t, Hex("C8402F"), 15, b + 17, 18, b + 25, 18, b + 17); P(t, 17, b + 24, Hex("E8E2CF")); }

        // Arms (and whatever they hold)
        Color arm = k.skin, armShade = k.skinShade;
        switch (pose)
        {
            case Pose.Dance:
                if (frame < 2) { R(t, 3, b + 7, 2, 9, arm); R(t, 17, b + 7, 2, 9, armShade); }
                else { R(t, 3, b + 7, 2, 9, arm); R(t, 16, b + 6, 4, 2, armShade); }
                break;
            case Pose.Drum:
                E(t, 5, 0, 12, 6, Hex("8A5A33")); R(t, 6, 2, 10, 1, Hex("C49A5C"));
                E(t, 5, 4, 12, 4, Hex("E3C38A"));
                int hl = frame == 0 ? 2 : 0, hr = frame == 0 ? 0 : 2;
                R(t, 5, b + 3, 2, 5, arm); R(t, 7, b + 2 + hl, 2, 2, arm);
                R(t, 15, b + 3, 2, 5, armShade); R(t, 13, b + 2 + hr, 2, 2, arm);
                break;
            case Pose.Grind:
                E(t, 6, 0, 10, 6, Hex("8E8A82")); R(t, 7, 5, 8, 1, Hex("4E4A46"));
                int up = frame == 0 ? 2 : 0;
                R(t, 10, 4 + up, 2, 5, Hex("C9CFC6"));
                R(t, 6, b + 3, 2, 4, arm); R(t, 14, b + 3, 2, 4, armShade);
                R(t, 8, b + 4 + up, 6, 2, arm);
                break;
            case Pose.Knap:
                E(t, 7, 1, 8, 4, Hex("4E4A46")); P(t, 9, 3, Hex("7A8A93"));
                int hit = frame == 0 ? 3 : 0;
                R(t, 5, b + 3, 2, 4, arm); R(t, 7, b + 2, 2, 2, arm);
                R(t, 15, b + 3, 2, 4 + hit, armShade); R(t, 13, b + 2 + hit, 3, 3, Hex("8E8A82"));
                break;
            case Pose.Spear:
                R(t, 4, b + 2, 2, 6, arm);
                R(t, 18, 1, 1, 25, Hex("7A4A2A"));
                Poly(t, Hex("7A8A93"), 17, 25, 18, 29, 20, 25); P(t, 18, 27, Hex("C9CFC6"));
                R(t, 16, b + 3, 2, 5, armShade); R(t, 17, b + 5, 2, 2, arm);
                break;
            case Pose.Staff:
                R(t, 4, b + 2, 2, 5, arm);
                R(t, 18, 1, 1, b + 17, Hex("7A4A2A")); E(t, 17, b + 16, 3, 3, Hex("8A5A33"));
                R(t, 16, b + 3, 2, 4, armShade); R(t, 17, b + 5, 2, 2, arm);
                break;
            case Pose.Paint:
                R(t, 4, b + 2, 2, 6, arm);
                if (frame == 0) { R(t, 16, b + 7, 2, 7, armShade); P(t, 17, b + 14, Hex("9C3B22")); }
                else { R(t, 16, b + 6, 2, 4, armShade); R(t, 17, b + 9, 2, 4, armShade); P(t, 18, b + 13, Hex("9C3B22")); }
                break;
            case Pose.Walk:
                int swing = frame == 0 ? 1 : 0;
                R(t, 4, b + 2 + swing, 2, 6, arm); R(t, 16, b + 3 - swing, 2, 6, armShade);
                break;
            default:
                R(t, 4, b + 2, 2, 6, arm); R(t, 16, b + 2, 2, 6, armShade);
                break;
        }
    }

    // ---------- Prop art ----------

    static void TorchPixels(Texture2D t, int f)
    {
        int s = f % 2;
        R(t, 5, 0, 2, 24, Hex("6E4A2F")); R(t, 6, 0, 1, 24, Hex("4A3222"));
        R(t, 4, 20, 4, 5, Hex("8A5A33")); R(t, 4, 21, 4, 1, Hex("4A3222")); R(t, 4, 23, 4, 1, Hex("4A3222"));
        Poly(t, Hex("D84B25"), 2, 25, 2 + s, 30, 6, 35 - (f % 3 == 2 ? 1 : 0), 10 - s, 30, 10, 25);
        Poly(t, Hex("F08A32"), 3, 25, 4 + s, 29, 6, 33 - s, 8, 29, 9, 25);
        R(t, 5, 25, 2, 5 + s, Hex("FFD45A")); R(t, 5, 26, 2, 2, Hex("FFF0A3"));
    }

    // A big bonfire: a glowing ember bed, crossed logs and three layers of flame tongues that sway per frame
    static void BonfirePixels(Texture2D t, int f)
    {
        int s = f % 2 == 0 ? 1 : -1, h = f % 3;
        E(t, 6, 0, 36, 10, Hex("5B2C23")); E(t, 9, 1, 30, 7, Hex("A5502B")); E(t, 14, 2, 20, 4, Hex("E0A24A"));
        Poly(t, Hex("D84B25"), 8, 5, 6, 22, 12, 18, 14 + s, 36 + h, 19, 28, 24 + s, 56 - h, 29, 30, 34 + s, 40 + h, 37, 20, 42, 24, 40, 5);
        Poly(t, Hex("F08A32"), 12, 5, 11, 20, 16, 17, 19 + s, 32, 24 + s, 46 - h, 28, 28, 32 + s, 34, 35, 18, 37, 5);
        Poly(t, Hex("FFD45A"), 16, 5, 15, 18, 20, 16, 24 + s, 34 + h, 28, 18, 32, 5);
        Poly(t, Hex("FFF0A3"), 19, 5, 20, 13, 24, 23 - h, 27, 13, 28, 5);
        void Log(int x0, int y0, int x1, int y1)
        {
            for (float k = 0; k <= 1f; k += 0.02f)
            {
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, k)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, k));
                R(t, x - 1, y - 1, 4, 4, Hex("6E4A2F")); P(t, x, y + 2, Hex("8A5A33"));
            }
            E(t, x0 - 2, y0 - 2, 5, 5, Hex("C8925A")); P(t, x0, y0, Hex("A06C3E"));
        }
        Log(4, 3, 22, 12); Log(44, 3, 26, 12); Log(13, 2, 35, 2);
        int[,] sparks = { { 9, 46 }, { 38, 50 }, { 14, 54 }, { 33, 44 }, { 20, 58 } };
        for (int i = 0; i < 5; i++) if ((i + f) % 2 == 0) P(t, sparks[i, 0] + s, sparks[i, 1] - h * 2, Hex("FFD45A"));
    }

    static void StoneRingPixels(Texture2D t)
    {
        E(t, 8, 5, 28, 18, Hex("3A2618")); E(t, 13, 8, 18, 12, Hex("2B1D14"));
        P(t, 18, 12, Hex("D84B25")); P(t, 24, 14, Hex("F08A32")); P(t, 21, 10, Hex("D84B25"));
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(22 + Mathf.Cos(a) * 17 - 3), y = Mathf.RoundToInt(14 + Mathf.Sin(a) * 10 - 2);
            Color c = i % 3 == 0 ? Hex("7A8A93") : i % 3 == 1 ? Hex("8E8A82") : Hex("9A948A");
            E(t, x, y, 7, 5, c); R(t, x + 1, y + 3, 3, 1, Hex("C9CFC6"));
        }
    }

    static void DrumPixels(Texture2D t)
    {
        Poly(t, Hex("8A5A33"), 2, 12, 3, 2, 14, 2, 15, 12);
        R(t, 3, 4, 12, 1, Hex("C49A5C")); R(t, 3, 8, 12, 1, Hex("C49A5C"));
        for (int x = 4; x < 15; x += 3) R(t, x, 4, 1, 5, Hex("C49A5C"));
        R(t, 12, 3, 2, 9, Hex("6E4A2F"));
        E(t, 1, 10, 16, 6, Hex("E3C38A")); E(t, 4, 11, 8, 3, Hex("F2DDA9"));
    }

    static void ZigzagPotPixels(Texture2D t, bool water)
    {
        Poly(t, Hex("A85A32"), 5, 1, 3, 8, 3, 15, 6, 19, 14, 19, 17, 15, 17, 8, 15, 1);
        R(t, 13, 2, 3, 15, Hex("7A3A20"));
        R(t, 5, 19, 10, 3, Hex("C9773F")); R(t, 6, 21, 8, 1, water ? Hex("3B5EA8") : Hex("3A2618"));
        if (water) R(t, 7, 21, 3, 1, Hex("7A8A93"));
        for (int x = 3; x <= 16; x++)
        {
            int y = 11 + ((x / 2) % 2 == 0 ? x % 2 : 1 - x % 2);
            P(t, x, y, Hex("E3C38A")); P(t, x, y + 1, Hex("E3C38A"));
        }
        R(t, 5, 6, 2, 3, Hex("C9773F"));
    }

    static void FernPotPixels(Texture2D t)
    {
        Poly(t, Hex("A85A32"), 7, 1, 5, 6, 5, 11, 8, 14, 16, 14, 19, 11, 19, 6, 17, 1);
        R(t, 15, 2, 3, 10, Hex("7A3A20")); R(t, 6, 13, 12, 2, Hex("C9773F"));
        for (int x = 5; x <= 18; x++) P(t, x, 8 + (x % 4 < 2 ? 0 : 1), Hex("E3C38A"));
        Poly(t, Hex("3D5A2A"), 12, 15, 2, 24, 4, 26, 12, 18);
        Poly(t, Hex("3D5A2A"), 12, 15, 22, 24, 20, 26, 12, 18);
        Poly(t, Hex("6B8F3A"), 11, 15, 5, 31, 8, 32, 13, 17);
        Poly(t, Hex("6B8F3A"), 13, 15, 19, 31, 16, 32, 12, 17);
        Poly(t, Hex("A7C25A"), 12, 15, 11, 33, 13, 33, 13, 16);
        P(t, 4, 25, Hex("A7C25A")); P(t, 20, 25, Hex("A7C25A")); P(t, 7, 30, Hex("A7C25A")); P(t, 17, 30, Hex("A7C25A"));
    }

    static void FruitBasketPixels(Texture2D t, int kind)
    {
        Color a = kind == 0 ? Hex("C8402F") : kind == 1 ? Hex("E08A2E") : Hex("7A8A93");
        Color hi = kind == 0 ? Hex("E8B796") : kind == 1 ? Hex("F2C46A") : Hex("C9CFC6");
        if (kind == 2)
        {
            for (int i = 0; i < 4; i++)
            {
                int x = 4 + i * 5;
                E(t, x, 10, 5, 10, a); P(t, x + 1, 17, Ink); R(t, x + 1, 12, 1, 4, hi);
                Poly(t, a, x, 19, x + 2, 21, x + 4, 19);
            }
        }
        else
        {
            int[,] spots = { { 4, 11 }, { 9, 12 }, { 14, 11 }, { 18, 12 }, { 7, 15 }, { 12, 16 }, { 16, 15 } };
            for (int i = 0; i < spots.GetLength(0); i++)
            {
                E(t, spots[i, 0], spots[i, 1], 5, 5, i % 3 == 2 && kind == 0 ? Hex("9C3B22") : a);
                P(t, spots[i, 0] + 1, spots[i, 1] + 3, hi);
            }
            P(t, 11, 20, Hex("6B8F3A")); P(t, 12, 21, Hex("6B8F3A"));
        }
        Poly(t, Hex("A96E3E"), 2, 12, 5, 1, 21, 1, 24, 12);
        R(t, 3, 10, 21, 3, Hex("D29A58")); R(t, 6, 6, 15, 1, Hex("C98748"));
        for (int x = 7; x < 21; x += 4) R(t, x, 2, 1, 8, Hex("80522F"));
    }

    static void RackPixels(Texture2D t)
    {
        R(t, 3, 0, 3, 31, Hex("6E4A2F")); R(t, 34, 0, 3, 31, Hex("6E4A2F"));
        R(t, 5, 0, 1, 31, Hex("4A3222")); R(t, 36, 0, 1, 31, Hex("4A3222"));
        R(t, 1, 28, 38, 3, Hex("8A5A33")); R(t, 1, 30, 38, 1, Hex("B07A48"));
        R(t, 2, 27, 5, 5, Hex("C49A5C")); R(t, 33, 27, 5, 5, Hex("C49A5C"));
        for (int i = 0; i < 3; i++)
        {
            int x = 8 + i * 5;
            R(t, x + 2, 24, 1, 4, Hex("C49A5C"));
            E(t, x, 12, 5, 13, Hex("7A8A93")); R(t, x + 1, 14, 2, 8, Hex("C9CFC6")); P(t, x + 2, 22, Ink);
            Poly(t, Hex("7A8A93"), x, 12, x + 2, 9, x + 5, 12);
        }
        R(t, 26, 26, 1, 2, Hex("C49A5C")); R(t, 30, 26, 1, 2, Hex("C49A5C"));
        Poly(t, Hex("D2B48A"), 23, 26, 33, 26, 32, 12, 29, 10, 24, 13);
        P(t, 26, 20, Hex("8A6A48")); P(t, 29, 16, Hex("8A6A48")); P(t, 27, 14, Hex("AE9068")); R(t, 31, 14, 1, 11, Hex("AE9068"));
    }

    static void BonePixels(Texture2D t)
    {
        Color bone = Hex("E8E2CF"), shade = Hex("BDB6A3");
        R(t, 3, 3, 13, 2, bone); E(t, 1, 1, 4, 4, bone); E(t, 1, 4, 4, 3, bone); E(t, 14, 1, 4, 4, bone); E(t, 14, 4, 4, 3, bone);
        R(t, 3, 3, 13, 1, shade);
        R(t, 6, 6, 2, 7, bone); E(t, 4, 11, 4, 4, bone); E(t, 7, 11, 4, 4, bone);
        E(t, 13, 4, 11, 10, bone); R(t, 15, 2, 7, 4, bone); R(t, 16, 2, 1, 2, shade); R(t, 18, 2, 1, 2, shade); R(t, 20, 2, 1, 2, shade);
        E(t, 15, 7, 3, 3, Ink); E(t, 19, 7, 3, 3, Ink); P(t, 18, 6, Ink); R(t, 21, 5, 2, 7, shade);
    }

    static void OvenPixels(Texture2D t)
    {
        E(t, 2, -14, 36, 46, Hex("B39B7C"));
        E(t, 5, -12, 30, 42, Hex("C4AC8A"));
        R(t, 26, 0, 8, 26, Hex("9A8266"));
        for (int y = 6; y < 30; y += 6) for (int x = 4; x < 36; x++) if ((x + y) % 7 == 0) P(t, x, y, Hex("8F7A5E"));
        E(t, 12, -2, 16, 15, Ink); E(t, 14, -2, 12, 10, Hex("D84B25")); E(t, 16, -2, 8, 7, Hex("F08A32")); E(t, 18, -2, 4, 4, Hex("FFD45A"));
        E(t, 16, 28, 8, 4, Hex("3A2618"));
        R(t, 6, 0, 28, 1, Hex("8F7A5E"));
    }

    static void TuskArchPixels(Texture2D t)
    {
        Color ivory = Hex("E8E2CF"), shade = Hex("BDB6A3");
        foreach (int side in new[] { -1, 1 })
        {
            Vector2 p0 = new Vector2(32 + side * 24, 0), p1 = new Vector2(32 + side * 30, 38), p2 = new Vector2(32 - side * 2, 49);
            for (float s = 0; s <= 1f; s += 0.004f)
            {
                Vector2 q = (1 - s) * (1 - s) * p0 + 2 * (1 - s) * s * p1 + s * s * p2;
                float r = Mathf.Lerp(3.2f, 1.2f, s);
                E(t, Mathf.RoundToInt(q.x - r), Mathf.RoundToInt(q.y - r), Mathf.RoundToInt(r * 2), Mathf.RoundToInt(r * 2), ivory);
                P(t, Mathf.RoundToInt(q.x + side * (r - 1)), Mathf.RoundToInt(q.y), shade);
            }
            int bx = 32 + side * 24;
            R(t, bx - 4, 0, 8, 7, Hex("6E4A2F")); R(t, bx - 4, 2, 8, 1, Hex("C49A5C")); R(t, bx - 4, 5, 8, 1, Hex("C49A5C"));
        }
        R(t, 27, 44, 10, 5, Hex("C49A5C")); R(t, 27, 46, 10, 1, Hex("9A6B3F"));
        R(t, 31, 36, 1, 8, Hex("C49A5C"));
        E(t, 27, 28, 9, 9, ivory); R(t, 29, 26, 5, 4, ivory); E(t, 28, 31, 3, 3, Ink); E(t, 32, 31, 3, 3, Ink);
        P(t, 30, 27, shade); P(t, 32, 27, shade);
        for (int i = 0; i < 5; i++) { R(t, 16 + i * 7, 40 - Mathf.Abs(i - 2) * 2, 1, 3, Hex("C49A5C")); Poly(t, ivory, 15 + i * 7, 39 - Mathf.Abs(i - 2) * 2, 16 + i * 7, 35 - Mathf.Abs(i - 2) * 2, 18 + i * 7, 39 - Mathf.Abs(i - 2) * 2); }
    }

    static void RugPixels(Texture2D t)
    {
        Color fur = Hex("5E3A26"), mid = Hex("7A4E32"), light = Hex("946240");
        Poly(t, fur, 13, 24, 2, 31, 4, 35, 16, 29);
        Poly(t, fur, 35, 24, 46, 31, 44, 35, 32, 29);
        Poly(t, fur, 14, 12, 2, 6, 3, 2, 17, 8);
        Poly(t, fur, 34, 12, 46, 6, 45, 2, 31, 8);
        E(t, 10, 5, 28, 27, fur); E(t, 13, 8, 22, 21, mid);
        E(t, 18, 28, 12, 10, fur); E(t, 17, 35, 4, 4, fur); E(t, 27, 35, 4, 4, fur);
        E(t, 21, 29, 6, 5, light); R(t, 23, 30, 2, 1, Ink); P(t, 20, 33, Ink); P(t, 27, 33, Ink);
        R(t, 22, 2, 4, 4, fur);
        foreach (var (x, y) in new[] { (2, 32), (3, 34), (45, 32), (44, 34), (3, 3), (2, 5), (45, 3), (46, 5) }) P(t, x, y, Hex("E8E2CF"));
        var rng = new System.Random(3);
        for (int i = 0; i < 60; i++)
        {
            int x = rng.Next(12, 36), y = rng.Next(7, 30);
            if (t.GetPixel(x, y).a > 0.5f) { P(t, x, y, i % 2 == 0 ? light : Hex("3A2618")); P(t, x, y + 1, i % 2 == 0 ? light : Hex("3A2618")); }
        }
    }

    static void SlabPixels(Texture2D t)
    {
        Poly(t, Hex("8F7A5E"), 2, 3, 6, 1, 19, 1, 22, 5, 21, 12, 15, 14, 4, 13, 1, 8);
        Poly(t, Hex("B39B7C"), 3, 5, 7, 3, 18, 3, 20, 6, 19, 11, 14, 13, 5, 12, 2, 8);
        R(t, 5, 10, 8, 1, Hex("CDB896")); P(t, 15, 6, Hex("8F7A5E")); P(t, 9, 6, Hex("8F7A5E"));
    }

    static void DreamcatcherPixels(Texture2D t)
    {
        R(t, 7, 25, 2, 5, Hex("8A5A33"));
        E(t, 2, 12, 12, 13, Hex("C49A5C")); E(t, 4, 14, 8, 9, Clear);
        P(t, 8, 22, Hex("E3C38A")); P(t, 6, 18, Hex("E3C38A")); P(t, 10, 18, Hex("E3C38A")); P(t, 8, 15, Hex("E3C38A"));
        P(t, 7, 20, Hex("E3C38A")); P(t, 9, 16, Hex("E3C38A")); R(t, 7, 18, 2, 2, Hex("3B5EA8"));
        foreach (int x in new[] { 4, 8, 11 })
        {
            int top = x == 8 ? 12 : 13, len = x == 8 ? 5 : 3;
            R(t, x, top - len, 1, len, Hex("C49A5C"));
            Poly(t, Hex("E8E2CF"), x - 1, top - len, x, top - len - 6, x + 2, top - len);
            P(t, x, top - len - 5, Hex("9C3B22"));
        }
    }

    static void NotePixels(Texture2D t)
    {
        E(t, 1, 1, 5, 4, Color.white); R(t, 5, 3, 1, 8, Color.white); R(t, 6, 9, 2, 1, Color.white); P(t, 7, 8, Color.white);
    }

    static void DoubleNotePixels(Texture2D t)
    {
        E(t, 1, 1, 4, 3, Color.white); E(t, 7, 2, 4, 3, Color.white);
        R(t, 4, 2, 1, 8, Color.white); R(t, 10, 3, 1, 8, Color.white); R(t, 4, 9, 7, 2, Color.white);
    }

    // Ochre and charcoal on a patch of smoothed tan rock: a hunt (0) or a herd with handprints and a sun (1)
    static void PaintingPixels(Texture2D t, int kind)
    {
        var rng = new System.Random(kind == 0 ? 17 : 29);
        // Plaster: an irregular blob
        var outline = new List<int>();
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            outline.Add(Mathf.Clamp(Mathf.RoundToInt(48 + Mathf.Cos(a) * Range(rng, 40, 47)), 1, 94));
            outline.Add(Mathf.Clamp(Mathf.RoundToInt(20 + Mathf.Sin(a) * Range(rng, 15, 19)), 1, 38));
        }
        Poly(t, Hex("C9A779"), outline.ToArray());
        for (int y = 0; y < 40; y++)
            for (int x = 0; x < 96; x++)
            {
                if (t.GetPixel(x, y).a < 0.5f) continue;
                bool edge = t.GetPixel(x - 1, y).a < 0.5f || t.GetPixel(x + 1, y).a < 0.5f || t.GetPixel(x, y - 1).a < 0.5f || t.GetPixel(x, y + 1).a < 0.5f;
                double r = rng.NextDouble();
                if (edge) t.SetPixel(x, y, Hex("A88660"));
                else if (r < 0.07) t.SetPixel(x, y, Hex("D8BC8E"));
                else if (r < 0.12) t.SetPixel(x, y, Hex("B8956A"));
            }
        Color red = Hex("9C3B22"), dark = Hex("4A2E1E");
        if (kind == 0)
        {
            Beast(t, 52, 14, dark, true); Beast(t, 70, 20, red, false);
            Hunter(t, 18, 10, red); Hunter(t, 27, 12, red); Hunter(t, 36, 9, dark);
            Hand(t, 82, 8, red);
        }
        else
        {
            Beast(t, 18, 15, red, false); Beast(t, 40, 12, dark, true); Beast(t, 60, 17, red, true);
            Hand(t, 80, 10, red); Hand(t, 86, 20, dark);
            E(t, 76, 26, 7, 7, red); E(t, 78, 28, 3, 3, Hex("C9A779"));
            foreach (var (dx, dy) in new[] { (-2, 3), (8, 3), (3, -2), (3, 8) }) P(t, 76 + dx, 26 + dy, red);
        }
        // A few charcoal dashes, like counting marks
        for (int i = 0; i < 5; i++) R(t, 12 + i * 2, 29, 1, 3, dark);
    }

    // A bison (horns, hump) or a deer (antlers), facing left
    static void Beast(Texture2D t, int x, int y, Color c, bool bison)
    {
        if (bison)
        {
            E(t, x, y, 14, 8, c); E(t, x - 3, y + 3, 6, 6, c); E(t, x + 2, y + 5, 7, 5, c);
            P(t, x - 3, y + 9, c); P(t, x - 4, y + 10, c); P(t, x + 1, y + 9, c);
        }
        else
        {
            E(t, x, y, 12, 6, c); R(t, x - 2, y + 4, 3, 5, c); R(t, x - 4, y + 7, 3, 2, c);
            P(t, x - 3, y + 10, c); P(t, x - 4, y + 11, c); P(t, x - 2, y + 11, c); P(t, x - 1, y + 12, c); P(t, x - 5, y + 12, c);
        }
        R(t, x + 2, y - 4, 1, 5, c); R(t, x + 4, y - 4, 1, 5, c); R(t, x + 8, y - 4, 1, 5, c); R(t, x + 10, y - 4, 1, 5, c);
        P(t, x + 12, y + 4, c); P(t, x + 13, y + 3, c);
    }

    static void Hunter(Texture2D t, int x, int y, Color c)
    {
        R(t, x, y + 9, 2, 2, c); R(t, x, y + 3, 1, 6, c);
        P(t, x - 1, y + 2, c); P(t, x - 2, y + 1, c); P(t, x + 1, y + 2, c); P(t, x + 2, y + 1, c); P(t, x + 2, y, c);
        P(t, x + 1, y + 7, c); P(t, x + 2, y + 8, c);
        for (int i = 0; i < 9; i++) P(t, x + 2 + i, y + 3 + i / 2, c);
        P(t, x + 11, y + 8, c); P(t, x + 10, y + 8, c);
    }

    static void Hand(Texture2D t, int x, int y, Color c)
    {
        R(t, x, y, 4, 4, c);
        R(t, x - 1, y + 4, 1, 2, c); R(t, x + 1, y + 4, 1, 3, c); R(t, x + 2, y + 4, 1, 3, c); R(t, x + 3, y + 4, 1, 2, c); R(t, x + 4, y + 1, 1, 2, c);
    }
}
