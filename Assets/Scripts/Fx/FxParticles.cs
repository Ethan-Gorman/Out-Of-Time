using System.Collections.Generic;
using UnityEngine;

// One shared pool of tiny pixel "particles" (the project has the Particle System module off), used by the small
// self-attaching effects in this folder: embers, sparkles, the time-gain burst and enemy death puffs.
// Each particle is a pooled SpriteRenderer with a 1-4 px sprite drawn at runtime (16 px per unit like the rest of
// the art), billboarded to the camera. Most run on world time so they hang in the air when the player stands still;
// feedback can ask for real time instead. Particles live on the pool, not on their source, so a puff from an enemy
// that gets destroyed a moment later still finishes.
public class FxParticles : MonoBehaviour
{
    public enum Shape { Dot, BigDot, Star, Puff }

    // Everything one particle needs; filled in by the emitter and copied into the pool
    public struct Spec
    {
        public Shape shape;
        public Vector3 position;
        public Vector3 velocity;
        public float life;
        public Color startColor;
        public Color endColor;
        public float startSize;
        public float endSize;
        public float gravity;   // negative floats it upwards, like an ember on hot air
        public float drag;
        public bool twinkle;    // fades in then out, instead of only out
        public bool unscaled;   // real time, for feedback that should play even while the world is frozen
        public Material material;
    }

    // Hard cap so a burst of everything at once can't flood the scene
    const int MaxLive = 240;
    // Same idea as SteamEmitter's puffs: just in front of the ordinary sprites
    const int SortingOrder = 3;

    struct Particle
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Spec spec;
        public float age;
    }

    static FxParticles instance;
    static Sprite[] sprites;

    readonly List<Particle> live = new List<Particle>(MaxLive);
    readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>(MaxLive);
    Material defaultMaterial;
    float nextMaterialLook;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    // Lives in the scene, so a scene load clears every particle with it
    public static FxParticles Ensure()
    {
        if (instance == null)
            instance = new GameObject("Fx Particles").AddComponent<FxParticles>();
        return instance;
    }

    public static void Spawn(in Spec spec)
    {
        var fx = Ensure();
        if (fx.live.Count >= MaxLive)
            return;

        SpriteRenderer r = fx.pool.Count > 0 ? fx.pool.Pop() : fx.CreateRenderer();
        r.sprite = SpriteFor(spec.shape);
        Material material = spec.material != null ? spec.material : fx.DefaultMaterial();
        if (material != null && r.sharedMaterial != material)
            r.sharedMaterial = material;
        Color start = spec.startColor;
        if (spec.twinkle)
            start.a = 0f;
        r.color = start;

        Transform t = r.transform;
        t.position = spec.position;
        t.localScale = Vector3.one * spec.startSize;
        r.gameObject.SetActive(true);

        fx.live.Add(new Particle { transform = t, renderer = r, spec = spec, age = 0f });
    }

    // True if a point is on (or just off) the screen, so emitters can skip work nobody would see
    public static bool OnScreen(Vector3 position, float margin = 0.15f)
    {
        var cam = Camera.main;
        if (cam == null)
            return false;
        Vector3 v = cam.WorldToViewportPoint(position);
        return v.z > 0f && v.x > -margin && v.x < 1f + margin && v.y > -margin && v.y < 1f + margin;
    }

    // The material of an object's own sprite, so its particles light and sort like it does (null = the pool's default)
    public static Material MaterialOf(Component source)
    {
        if (source == null)
            return null;
        var sprite = source.GetComponentInChildren<SpriteRenderer>(true);
        return sprite != null ? sprite.sharedMaterial : null;
    }

    // Where an object visibly is: the middle of its sprite (or mesh), else a little above its pivot
    public static Vector3 CentreOf(Renderer shape, Transform fallback, float fallbackHeight)
    {
        if (shape != null && shape.enabled && shape.gameObject.activeInHierarchy)
            return shape.bounds.center;
        return fallback.position + Vector3.up * fallbackHeight;
    }

    // The visible renderer to centre effects on: a sprite if there is one, else any mesh
    public static Renderer ShapeOf(Component source)
    {
        var sprite = source.GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
            return sprite;
        return source.GetComponentInChildren<MeshRenderer>();
    }

    SpriteRenderer CreateRenderer()
    {
        var go = new GameObject("Fx");
        go.transform.SetParent(transform, false);
        go.SetActive(false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sortingOrder = SortingOrder;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    // The player's sprite material, the same one most sprites in the game use
    Material DefaultMaterial()
    {
        // Keep looking (at most once a second) in case the player turns up after the first particle
        if (defaultMaterial == null && Time.unscaledTime >= nextMaterialLook)
        {
            nextMaterialLook = Time.unscaledTime + 1f;
            var player = FindAnyObjectByType<PlayerMovement>();
            if (player != null && player.sprite != null)
                defaultMaterial = player.sprite.sharedMaterial;
        }
        return defaultMaterial;
    }

    void Update()
    {
        float worldDt = WorldTime.Delta;
        float realDt = Time.unscaledDeltaTime;
        var cam = Camera.main;
        Quaternion facing = cam != null ? cam.transform.rotation : Quaternion.identity;

        for (int i = live.Count - 1; i >= 0; i--)
        {
            Particle p = live[i];
            if (p.transform == null)
            {
                RemoveAt(i);
                continue;
            }

            float dt = p.spec.unscaled ? realDt : worldDt;
            p.age += dt;
            if (p.age >= p.spec.life)
            {
                p.renderer.gameObject.SetActive(false);
                pool.Push(p.renderer);
                RemoveAt(i);
                continue;
            }

            float t = p.age / p.spec.life;
            p.spec.velocity += Vector3.down * (p.spec.gravity * dt);
            p.spec.velocity *= 1f - Mathf.Clamp01(p.spec.drag * dt);
            p.transform.position += p.spec.velocity * dt;
            p.transform.rotation = facing;
            p.transform.localScale = Vector3.one * Mathf.Lerp(p.spec.startSize, p.spec.endSize, t);

            Color c = Color.Lerp(p.spec.startColor, p.spec.endColor, t);
            if (p.spec.twinkle)
                c.a = p.spec.startColor.a * Mathf.Sin(t * Mathf.PI);
            p.renderer.color = c;
            live[i] = p;
        }
    }

    // Swap with the last one instead of shifting the list; safe because Update walks backwards
    void RemoveAt(int i)
    {
        int last = live.Count - 1;
        live[i] = live[last];
        live.RemoveAt(last);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // ---- Sprites ----

    static Sprite SpriteFor(Shape shape)
    {
        // Rebuilt if the editor threw the textures away between plays (domain reload is off, so the array survives)
        if (sprites == null || sprites[(int)shape] == null)
        {
            // Drawn once: white, so the renderer colour tints them
            sprites = new Sprite[4];
            sprites[(int)Shape.Dot] = MakeSprite(new[] { "#" }, "Fx Dot");
            sprites[(int)Shape.BigDot] = MakeSprite(new[] { "##", "##" }, "Fx Big Dot");
            sprites[(int)Shape.Star] = MakeSprite(new[] { ".#.", "###", ".#." }, "Fx Star");
            sprites[(int)Shape.Puff] = MakeSprite(new[] { ".##.", "####", "####", ".##." }, "Fx Puff");
        }
        return sprites[(int)shape];
    }

    static Sprite MakeSprite(string[] rows, string name)
    {
        int h = rows.Length;
        int w = rows[0].Length;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                texture.SetPixel(x, h - 1 - y, rows[y][x] == '#' ? Color.white : Color.clear);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        sprite.name = name;
        return sprite;
    }
}
