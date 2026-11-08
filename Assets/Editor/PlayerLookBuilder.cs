using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Turns a hand-drawn character sheet (white background, two standing/walking poses and two falling/lying poses)
// into an era's player look: a pixel-art sprite sheet, Idle / Walk / Death clips, and an override of the
// player's AcPlayer animator. Then puts that look on a player in an Essentials prefab.
//
// The sheet is split automatically: each drawing is found as a blob of ink. Upright blobs are the walk poses
// (top one first), wide blobs are the death poses (falling, then lying). Every frame is shrunk to the game's pixel
// size and snapped to the drawing's own colours, so it reads as pixel art next to the rest of the game.
// Extra frames are generated for smooth motion: a breathing idle, and a bob between the two walk poses.
//
// Tools > Out Of Time > Build Player Looks rebuilds every look below. Safe to run again.
public static class PlayerLookBuilder
{
    public class Look
    {
        public string name;            // e.g. "Knight"
        public string sheet;           // source drawing (PNG) inside the project
        public string folder;          // where the sprite sheet, clips and controller go
        public bool facesLeft;         // the drawing faces left (PlayerMovement then flips it the other way)
        public int heightPixels = 44;  // standing height in game pixels
        public float pixelsPerUnit = 32f;
    }

    public static readonly Look Knight = new Look { name = "Knight", sheet = "Assets/Sprites/Medieval/Knight/KnightSheet.png", folder = "Assets/Sprites/Medieval/Knight", facesLeft = false };
    public static readonly Look Gentleman = new Look { name = "Gentleman", sheet = "Assets/Sprites/Steampunk/Gentleman/GentlemanSheet.png", folder = "Assets/Sprites/Steampunk/Gentleman", facesLeft = true };

    public class Result
    {
        public Texture2D sheet;
        public Sprite idle;
        public AnimatorOverrideController controller;
        public bool facesLeft;
    }

    [MenuItem("Tools/Out Of Time/Build Player Looks")]
    public static void BuildAll()
    {
        // Note: the Medieval and Steampunk scenes now override the player's sprite, controller and facing on their
        // Essentials instance (Assets/Animations/Medieval/MedievalPlayer and Steampunk/IndustrialPlayer looks),
        // so the prefab values written here only show up where a scene has no such override.
        var knight = Build(Knight);
        if (knight != null) ApplyToPrefab("Assets/prefabs/Medieval/MedievalEssentials.prefab", knight);
        var gentleman = Build(Gentleman);
        if (gentleman != null) ApplyToPrefab("Assets/prefabs/Steampunk/SteampunkEssentials.prefab", gentleman);
    }

    // ---------------------------------------------------------------- build

    public static Result Build(Look look)
    {
        if (!File.Exists(look.sheet)) { Debug.LogWarning($"[PlayerLook] {look.sheet} not found, skipping {look.name}."); return null; }
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes(look.sheet));
        int w = src.width, h = src.height;
        Color32[] px = src.GetPixels32();
        UnityEngine.Object.DestroyImmediate(src);

        // 1. Ink mask (anything not near-white), then blobs on a coarse grid so a drawing's loose bits stay together
        bool Ink(int x, int y) { var c = px[y * w + x]; return c.a > 128 && (c.r < 225 || c.g < 225 || c.b < 225); }
        const int cell = 12;
        int gw = (w + cell - 1) / cell, gh = (h + cell - 1) / cell;
        var grid = new bool[gw, gh];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (Ink(x, y)) grid[x / cell, y / cell] = true;
        // Dilate by 2 cells so monocle dots, buttons and plumes join their body
        var fat = new bool[gw, gh];
        for (int y = 0; y < gh; y++) for (int x = 0; x < gw; x++) if (grid[x, y])
            for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) { int a = x + dx, b = y + dy; if (a >= 0 && b >= 0 && a < gw && b < gh) fat[a, b] = true; }
        var blobs = new List<RectInt>();
        var seen = new bool[gw, gh];
        for (int y = 0; y < gh; y++) for (int x = 0; x < gw; x++)
        {
            if (!fat[x, y] || seen[x, y]) continue;
            int minX = x, maxX = x, minY = y, maxY = y, count = 0;
            var stack = new Stack<Vector2Int>(); stack.Push(new Vector2Int(x, y)); seen[x, y] = true;
            while (stack.Count > 0)
            {
                var p = stack.Pop(); count++;
                minX = Math.Min(minX, p.x); maxX = Math.Max(maxX, p.x); minY = Math.Min(minY, p.y); maxY = Math.Max(maxY, p.y);
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var q = p + d;
                    if (q.x < 0 || q.y < 0 || q.x >= gw || q.y >= gh || seen[q.x, q.y] || !fat[q.x, q.y]) continue;
                    seen[q.x, q.y] = true; stack.Push(q);
                }
            }
            if (count > 40) blobs.Add(new RectInt(minX * cell, minY * cell, (maxX - minX + 1) * cell, (maxY - minY + 1) * cell));
        }
        // Tighten each blob to its real ink
        for (int i = 0; i < blobs.Count; i++)
        {
            var r = blobs[i]; int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = r.yMin; y < Math.Min(h, r.yMax); y++) for (int x = r.xMin; x < Math.Min(w, r.xMax); x++)
                if (Ink(x, y)) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
            blobs[i] = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }
        blobs = blobs.OrderByDescending(b => b.width * b.height).Take(4).ToList();
        // Texture rows run bottom-up, so "top of the drawing" is the largest y
        var upright = blobs.Where(b => b.height >= b.width).OrderByDescending(b => b.yMax).ToList();
        var lying = blobs.Where(b => b.height < b.width).OrderByDescending(b => b.yMax).ToList();
        if (upright.Count < 2 || lying.Count < 1) { Debug.LogError($"[PlayerLook] {look.name}: expected 2 standing and 2 lying drawings, found {upright.Count} and {lying.Count}."); return null; }
        if (lying.Count < 2) lying.Add(lying[0]);

        // 2. The drawing's palette: its most common colours (outline, fills, accents)
        var palette = Palette(px, w, blobs);

        // 3. Shrink each pose to game pixels: the standing height decides the scale for every frame
        float scale = look.heightPixels / (float)Math.Max(upright[0].height, upright[1].height);
        Color32[,] walkA = Shrink(px, w, upright[0], scale, palette), walkB = Shrink(px, w, upright[1], scale, palette);
        Color32[,] fall = Shrink(px, w, lying[0], scale, palette), dead = Shrink(px, w, lying[1], scale, palette);

        // 4. Frames, including generated in-betweens:
        //    Idle: stand, breathe (top half sinks a pixel). Walk: A, A bobbing up, B, B bobbing up. Death: falling, lying
        var frames = new List<(string, Color32[,], int)>
        {
            ("Idle_0", walkA, 0), ("Idle_1", Breathe(walkA), 0),
            ("Walk_0", walkA, 0), ("Walk_1", walkA, 1), ("Walk_2", walkB, 0), ("Walk_3", walkB, 1),
            ("Death_0", fall, 0), ("Death_1", dead, 0),
        };
        int cw = frames.Max(f => f.Item2.GetLength(0)) + 2, ch = frames.Max(f => f.Item2.GetLength(1)) + 3;
        var atlas = new Texture2D(cw * frames.Count, ch, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
        for (int i = 0; i < frames.Count; i++)
        {
            var (_, f, lift) = frames[i]; int fw = f.GetLength(0), fh = f.GetLength(1), ox = i * cw + (cw - fw) / 2;
            for (int y = 0; y < fh; y++) for (int x = 0; x < fw; x++) if (f[x, y].a > 0) atlas.SetPixel(ox + x, y + lift + 1, f[x, y]);
        }
        atlas.Apply();
        string sheetPath = $"{look.folder}/{look.name}Sprites.png";
        File.WriteAllBytes(Path.GetFullPath(sheetPath), atlas.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(atlas);
        AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceUpdate);

        // 5. Slice it: one sprite per frame, pivot at the feet
        var importer = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = look.pixelsPerUnit; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
#pragma warning disable 618
        importer.spritesheet = frames.Select((f, i) => new SpriteMetaData { name = $"{look.name}_{f.Item1}", rect = new Rect(i * cw, 0, cw, ch), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 1f / ch) }).ToArray();
#pragma warning restore 618
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToDictionary(s => s.name);
        Sprite S(string n) => sprites[$"{look.name}_{n}"];

        // 6. Clips and the animator override (AcPlayer's Stand / Run / Dead)
        var idle = Clip(look, "Idle", 3f, true, S("Idle_0"), S("Idle_1"));
        var walk = Clip(look, "Walk", 8f, true, S("Walk_0"), S("Walk_1"), S("Walk_2"), S("Walk_3"));
        var death = Clip(look, "Death", 6f, false, S("Death_0"), S("Death_1"));
        var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player/AcPlayer.controller");
        string controllerPath = $"{look.folder}/{look.name}Player.overrideController";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        if (controller == null) { controller = new AnimatorOverrideController(baseController); AssetDatabase.CreateAsset(controller, controllerPath); }
        controller.runtimeAnimatorController = baseController;
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(pairs);
        for (int i = 0; i < pairs.Count; i++)
        {
            string n = pairs[i].Key.name;
            pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, n.StartsWith("Stand") ? idle : n.StartsWith("Run") ? walk : n.StartsWith("Dead") ? death : pairs[i].Value);
        }
        controller.ApplyOverrides(pairs);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PlayerLook] Built {look.name}: {frames.Count} frames ({cw}x{ch}px), clips and {controllerPath}.");
        return new Result { sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath), idle = S("Idle_0"), controller = controller, facesLeft = look.facesLeft };
    }

    // Puts a look on the player inside an Essentials prefab (sprite, animator, size, facing)
    public static void ApplyToPrefab(string prefabPath, Result look)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) { Debug.LogWarning($"[PlayerLook] {prefabPath} not found."); return; }
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        var player = root.GetComponentsInChildren<PlayerMovement>(true).FirstOrDefault();
        if (player != null) Apply(player.transform, look);
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath); PrefabUtility.UnloadPrefabContents(root);
    }

    public static void Apply(Transform player, Result look)
    {
        Transform art = player.Find("Sprite");
        if (art == null) { Debug.LogWarning("[PlayerLook] Player has no Sprite child."); return; }
        art.localPosition = new Vector3(0f, 0.02f, 0f); // pivot is at the feet
        art.localScale = Vector3.one;
        var renderer = art.GetComponent<SpriteRenderer>(); if (renderer != null) renderer.sprite = look.idle;
        var animator = art.GetComponent<Animator>(); if (animator != null) animator.runtimeAnimatorController = look.controller;
        var movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            var so = new SerializedObject(movement);
            so.FindProperty("leftFacingSpriteSheet").objectReferenceValue = look.facesLeft ? look.sheet : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ---------------------------------------------------------------- pixels

    static List<Color32> Palette(Color32[] px, int w, List<RectInt> areas)
    {
        var counts = new Dictionary<int, int>();
        foreach (var r in areas)
            for (int y = r.yMin; y < r.yMax; y += 2) for (int x = r.xMin; x < r.xMax; x += 2)
            {
                var c = px[y * w + x];
                if (c.a < 128 || (c.r > 225 && c.g > 225 && c.b > 225)) continue;
                int key = (c.r >> 3) << 10 | (c.g >> 3) << 5 | (c.b >> 3);
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            }
        var result = new List<Color32>();
        foreach (var kv in counts.OrderByDescending(k => k.Value))
        {
            var c = new Color32((byte)(((kv.Key >> 10) & 31) << 3 | 4), (byte)(((kv.Key >> 5) & 31) << 3 | 4), (byte)((kv.Key & 31) << 3 | 4), 255);
            // Keep colours that are clearly different from ones already picked (skips anti-aliasing blends)
            if (result.All(p => Dist(p, c) > 900) && kv.Value > 30) result.Add(c);
            if (result.Count >= 12) break;
        }
        return result;
    }

    static int Dist(Color32 a, Color32 b) { int r = a.r - b.r, g = a.g - b.g, bl = a.b - b.b; return r * r + g * g + bl * bl; }

    // Area-averages the drawing down to game pixels, cuts the alpha at half, and snaps colours to the palette
    static Color32[,] Shrink(Color32[] px, int w, RectInt r, float scale, List<Color32> palette)
    {
        int ow = Math.Max(1, Mathf.RoundToInt(r.width * scale)), oh = Math.Max(1, Mathf.RoundToInt(r.height * scale));
        var o = new Color32[ow, oh];
        for (int y = 0; y < oh; y++) for (int x = 0; x < ow; x++)
        {
            int x0 = r.xMin + (int)(x / scale), x1 = r.xMin + (int)((x + 1) / scale), y0 = r.yMin + (int)(y / scale), y1 = r.yMin + (int)((y + 1) / scale);
            float sr = 0, sg = 0, sb = 0, ink = 0, n = 0, dark = 0;
            for (int yy = y0; yy < Math.Max(y1, y0 + 1); yy++) for (int xx = x0; xx < Math.Max(x1, x0 + 1); xx++)
            {
                var c = px[yy * w + xx]; n++;
                if (c.a < 128 || (c.r > 225 && c.g > 225 && c.b > 225)) continue;
                ink++; sr += c.r; sg += c.g; sb += c.b;
                if (c.r + c.g + c.b < 200) dark++;
            }
            if (ink / n < 0.45f) continue;
            // Thin outlines would vanish in an average: a cell that is mostly outline stays outline
            var avg = dark / ink > 0.62f ? new Color32(0, 0, 0, 255) : new Color32((byte)(sr / ink), (byte)(sg / ink), (byte)(sb / ink), 255);
            o[x, y] = palette.OrderBy(p => Dist(p, avg)).First();
        }
        return o;
    }

    // Breathing frame: everything above the waist sinks one pixel
    static Color32[,] Breathe(Color32[,] f)
    {
        int fw = f.GetLength(0), fh = f.GetLength(1), waist = fh * 2 / 5;
        var o = new Color32[fw, fh];
        for (int y = 0; y < fh; y++) for (int x = 0; x < fw; x++)
            if (y < waist) o[x, y] = f[x, y];
            else o[x, y - 1] = f[x, y].a > 0 ? f[x, y] : o[x, y - 1];
        return o;
    }

    static AnimationClip Clip(Look look, string name, float fps, bool loop, params Sprite[] sprites)
    {
        string path = $"{look.folder}/{look.name}{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[sprites.Length + (loop ? 1 : 0)];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        if (loop) keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = sprites[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
