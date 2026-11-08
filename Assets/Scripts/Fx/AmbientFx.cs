using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ambient pixel particles for fires and steampunk machinery, hooked on when a scene loads (no prefab edits).
// Uses the shared FxParticles pool, so everything runs on world time and freezes with the world.
//  - The camp fires (bonfire, campfires): flickering flame licks, the odd sideways spark, and faint smoke
//    (bigger light range = bigger fire). FlameEmbers already adds the slow drifting embers.
//  - SteamPress: a shower of sparks the moment the head hits bottom
//  - ConveyorBelt: occasional sparks and grit at each end roller
//  - GearSpinner: rare tiny metal glints while turning
//  - Objects named Boiler / Pipe (steam wisps, unless a SteamEmitter or SteamVent already covers them),
//    Chimney / Smokestack (dark smoke curls), Furnace / Forge (embers and a soft glow pulse)
// One host object per scene drives every source from a single Update, culled to what is on screen.
public class AmbientFx : MonoBehaviour
{
    enum Kind { Fire, Press, Belt, Gear, Steam, Pipe, Smoke, Furnace }

    class Source
    {
        public Kind kind;
        public Transform transform;
        public TorchFlicker flicker;
        public SteamPress press;
        public ConveyorBelt belt;
        public GearSpinner gear;
        public Renderer shape;
        public Material material;
        public float big;       // 0..1 fire size
        public float timerA;
        public float timerB;
        public float timerC;
        public float lastY;
        public bool falling;
    }

    // Keeps a busy screen from eating the whole shared particle cap
    const int MaxSpawnsPerFrame = 8;
    const int MaxNamedSources = 48;
    const float MaxDistance = 45f;

    static readonly string[] SteamNames = { "Boiler" };
    static readonly string[] PipeNames = { "Pipe" };
    static readonly string[] SmokeNames = { "Chimney", "Smokestack" };
    static readonly string[] FurnaceNames = { "Furnace", "Forge" };

    static readonly Color FlameYellow = new Color(1f, 0.9f, 0.45f, 1f);
    static readonly Color FlameOrange = new Color(1f, 0.55f, 0.15f, 1f);
    static readonly Color FlameRed = new Color(0.9f, 0.2f, 0.05f, 0f);
    static readonly Color SparkHot = new Color(1f, 0.97f, 0.75f, 1f);
    static readonly Color SparkCool = new Color(1f, 0.45f, 0.1f, 0f);
    static readonly Color FireSmoke = new Color(0.45f, 0.43f, 0.42f, 0.28f);
    static readonly Color FireSmokeEnd = new Color(0.55f, 0.55f, 0.55f, 0f);
    static readonly Color SteamWhite = new Color(0.95f, 0.95f, 0.93f, 0.45f);
    static readonly Color SteamEnd = new Color(0.85f, 0.85f, 0.85f, 0f);
    static readonly Color SootDark = new Color(0.22f, 0.2f, 0.19f, 0.5f);
    static readonly Color SootEnd = new Color(0.4f, 0.4f, 0.4f, 0f);
    static readonly Color Grit = new Color(0.3f, 0.27f, 0.24f, 1f);
    static readonly Color GritEnd = new Color(0.3f, 0.27f, 0.24f, 0f);
    static readonly Color Glint = new Color(1f, 1f, 0.95f, 1f);
    static readonly Color Glow = new Color(1f, 0.5f, 0.12f, 0.16f);

    static AmbientFx instance;

    readonly List<Source> sources = new List<Source>();
    readonly HashSet<Component> known = new HashSet<Component>();
    int namedCount;
    int spawnedThisFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Begin();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Begin();

    static void Begin()
    {
        if (instance == null)
            instance = new GameObject("Ambient Fx").AddComponent<AmbientFx>();
        instance.Scan();
        instance.StartCoroutine(instance.RescanSoon());
    }

    // Level scripts build props in Start; look again a moment later to catch them
    IEnumerator RescanSoon()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        Scan();
        yield return new WaitForSecondsRealtime(2f);
        Scan();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // ---- Finding sources ----

    void Scan()
    {
        const FindObjectsInactive all = FindObjectsInactive.Include;
        const FindObjectsSortMode any = FindObjectsSortMode.None;

        foreach (var c in Object.FindObjectsByType<TorchFlicker>(all, any))
        {
            // Flame licks and smoke are for the big camp fires only; torches and lanterns keep just their embers
            if (!FxBootstrap.IsBonfire(c) || !known.Add(c))
                continue;
            float range = c.flame != null ? c.flame.range : 4f;
            sources.Add(new Source
            {
                kind = Kind.Fire,
                transform = c.transform,
                flicker = c,
                material = FxParticles.MaterialOf(c),
                big = Mathf.InverseLerp(3f, 12f, range),
                timerB = Random.Range(0.5f, 3f),
                timerC = Random.value,
            });
        }

        bool steampunk = SceneManager.GetActiveScene().name.IndexOf("steam", System.StringComparison.OrdinalIgnoreCase) >= 0;

        foreach (var c in Object.FindObjectsByType<SteamPress>(all, any))
        {
            steampunk = true;
            if (!known.Add(c))
                continue;
            sources.Add(new Source
            {
                kind = Kind.Press,
                transform = c.transform,
                press = c,
                shape = c.head != null ? c.head.GetComponentInChildren<Renderer>() : null,
                material = FxParticles.MaterialOf(c),
                lastY = c.head != null ? c.head.position.y : 0f,
            });
        }

        foreach (var c in Object.FindObjectsByType<ConveyorBelt>(all, any))
        {
            steampunk = true;
            if (!known.Add(c))
                continue;
            sources.Add(new Source
            {
                kind = Kind.Belt,
                transform = c.transform,
                belt = c,
                material = FxParticles.MaterialOf(c),
                timerA = Random.Range(0.5f, 1.5f),
            });
        }

        // Gears only in steampunk scenes: the Future rift discs also use GearSpinner
        if (!steampunk)
            return;

        foreach (var c in Object.FindObjectsByType<GearSpinner>(all, any))
        {
            if (!known.Add(c))
                continue;
            sources.Add(new Source
            {
                kind = Kind.Gear,
                transform = c.transform,
                gear = c,
                shape = FxParticles.ShapeOf(c),
                material = FxParticles.MaterialOf(c),
                timerA = Random.Range(2f, 9f),
            });
        }

        if (steampunk)
            ScanNames();
    }

    void ScanNames()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (namedCount >= MaxNamedSources)
                return;
            if (known.Contains(t))
                continue;
            string name = t.name;
            Kind kind;
            if (Matches(name, SmokeNames)) kind = Kind.Smoke;
            else if (Matches(name, FurnaceNames)) kind = Kind.Furnace;
            else if (Matches(name, SteamNames)) kind = Kind.Steam;
            else if (Matches(name, PipeNames)) kind = Kind.Pipe;
            else continue;

            // A boiler made of several named parts only gets one emitter, on its top-most match
            if (AncestorMatches(t))
                continue;
            // Steam vents already puff their own steam
            if ((kind == Kind.Steam || kind == Kind.Pipe) &&
                (t.GetComponentInChildren<SteamEmitter>(true) != null || t.GetComponentInParent<SteamEmitter>() != null ||
                 t.GetComponentInChildren<SteamVent>(true) != null || t.GetComponentInParent<SteamVent>() != null))
                continue;

            known.Add(t);
            namedCount++;
            sources.Add(new Source
            {
                kind = kind,
                transform = t,
                shape = t.GetComponentInChildren<Renderer>(),
                material = FxParticles.MaterialOf(t),
                timerA = Random.Range(0.2f, 2f),
                timerB = Random.Range(0.2f, 1.5f),
            });
        }
    }

    static bool Matches(string name, string[] words)
    {
        for (int i = 0; i < words.Length; i++)
            if (name.IndexOf(words[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    static bool AncestorMatches(Transform t)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
        {
            string n = p.name;
            if (Matches(n, SmokeNames) || Matches(n, FurnaceNames) || Matches(n, SteamNames) || Matches(n, PipeNames))
                return true;
        }
        return false;
    }

    // ---- Running ----

    void Update()
    {
        float dt = WorldTime.Delta;
        if (dt <= 0f)
            return;
        spawnedThisFrame = 0;
        var cam = Camera.main;
        if (cam == null)
            return;
        Vector3 camPos = cam.transform.position;

        for (int i = sources.Count - 1; i >= 0; i--)
        {
            Source s = sources[i];
            if (s.transform == null)
            {
                sources[i] = sources[sources.Count - 1];
                sources.RemoveAt(sources.Count - 1);
                continue;
            }
            if (!s.transform.gameObject.activeInHierarchy)
                continue;

            switch (s.kind)
            {
                case Kind.Fire: UpdateFire(s, dt, camPos); break;
                case Kind.Press: UpdatePress(s, dt, camPos); break;
                case Kind.Belt: UpdateBelt(s, dt, camPos); break;
                case Kind.Gear: UpdateGear(s, dt, camPos); break;
                default: UpdateNamed(s, dt, camPos); break;
            }
        }
    }

    static bool Visible(Vector3 position, Vector3 camPos)
    {
        return (position - camPos).sqrMagnitude < MaxDistance * MaxDistance && FxParticles.OnScreen(position);
    }

    void UpdateFire(Source s, float dt, Vector3 camPos)
    {
        Light flame = s.flicker != null ? s.flicker.flame : null;
        if (flame != null && !flame.isActiveAndEnabled)
            return;
        Vector3 origin = flame != null ? flame.transform.position : s.transform.position;
        if (!Visible(origin, camPos))
            return;

        float radius = 0.05f + 0.15f * s.big;

        // Flame licks: short, bright, rising a little
        s.timerA += (7f + 10f * s.big) * dt;
        if (s.timerA >= 1f)
        {
            s.timerA = Mathf.Min(s.timerA - 1f, 1f);
            Vector2 flat = Random.insideUnitCircle * radius;
            Emit(new FxParticles.Spec
            {
                shape = Random.value < 0.25f + 0.25f * s.big ? FxParticles.Shape.BigDot : FxParticles.Shape.Dot,
                position = origin + new Vector3(flat.x, Random.Range(-0.05f, 0.05f), flat.y),
                velocity = new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.35f, 0.7f) * (1f + s.big), Random.Range(-0.08f, 0.08f)),
                life = Random.Range(0.18f, 0.38f),
                startColor = Random.value < 0.55f ? FlameYellow : FlameOrange,
                endColor = FlameRed,
                startSize = 1f,
                endSize = 1f,
                gravity = -0.6f,
                drag = 1f,
                material = s.material,
            });
        }

        // Sparks that pop sideways now and then
        s.timerB -= dt;
        if (s.timerB <= 0f)
        {
            s.timerB = Random.Range(1.2f, 3.5f) * (1.2f - 0.6f * s.big);
            int count = Random.value < 0.3f ? 2 : 1;
            for (int k = 0; k < count; k++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                Emit(new FxParticles.Spec
                {
                    shape = FxParticles.Shape.Dot,
                    position = origin + Vector3.up * 0.05f,
                    velocity = new Vector3(dir.x * Random.Range(0.9f, 1.6f), Random.Range(0.8f, 1.6f), dir.y * Random.Range(0.9f, 1.6f)),
                    life = Random.Range(0.35f, 0.6f),
                    startColor = SparkHot,
                    endColor = SparkCool,
                    startSize = 1f,
                    endSize = 1f,
                    gravity = 3.5f,
                    drag = 0.6f,
                    material = s.material,
                });
            }
        }

        // Faint smoke over the bigger fires only
        if (s.big > 0.35f)
        {
            s.timerC += 1.6f * s.big * dt;
            if (s.timerC >= 1f)
            {
                s.timerC = Mathf.Min(s.timerC - 1f, 1f);
                Vector2 flat = Random.insideUnitCircle * radius;
                Emit(new FxParticles.Spec
                {
                    shape = FxParticles.Shape.Puff,
                    position = origin + new Vector3(flat.x, 0.25f + 0.35f * s.big, flat.y),
                    velocity = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0.35f, 0.55f), Random.Range(-0.1f, 0.1f)),
                    life = Random.Range(2f, 3f),
                    startColor = FireSmoke,
                    endColor = FireSmokeEnd,
                    startSize = 1f,
                    endSize = 2.2f + s.big,
                    gravity = -0.05f,
                    drag = 0.3f,
                    twinkle = true,
                    material = s.material,
                });
            }
        }
    }

    // The press head drops fast during the slam; the frame it stops is the impact
    void UpdatePress(Source s, float dt, Vector3 camPos)
    {
        Transform head = s.press != null ? s.press.head : null;
        if (head == null)
            return;
        float y = head.position.y;
        float down = (s.lastY - y) / dt;
        s.lastY = y;
        if (down > 3f)
        {
            s.falling = true;
            return;
        }
        if (!s.falling || down > 0.5f)
            return;
        s.falling = false;

        Vector3 hit;
        if (s.shape != null && s.shape.enabled)
        {
            Bounds b = s.shape.bounds;
            hit = new Vector3(b.center.x, b.min.y, b.center.z);
        }
        else
            hit = head.position;
        if (!Visible(hit, camPos))
            return;

        float width = s.shape != null ? Mathf.Max(0.2f, s.shape.bounds.extents.x) : 0.4f;
        for (int k = 0; k < 12; k++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Emit(new FxParticles.Spec
            {
                shape = Random.value < 0.2f ? FxParticles.Shape.Star : FxParticles.Shape.Dot,
                position = hit + new Vector3(Random.Range(-width, width), 0.03f, Random.Range(-0.2f, 0.2f)),
                velocity = new Vector3(dir.x * Random.Range(1.5f, 3f), Random.Range(1f, 2.5f), dir.y * Random.Range(1f, 2f)),
                life = Random.Range(0.35f, 0.65f),
                startColor = SparkHot,
                endColor = SparkCool,
                startSize = 1f,
                endSize = 1f,
                gravity = 7f,
                drag = 0.5f,
                material = s.material,
            });
        }
    }

    void UpdateBelt(Source s, float dt, Vector3 camPos)
    {
        if (s.belt == null || !s.belt.isActiveAndEnabled || s.belt.speed <= 0f)
            return;
        s.timerA -= dt;
        if (s.timerA > 0f)
            return;
        s.timerA = Random.Range(0.7f, 1.8f);

        Vector3 local = Random.value < 0.5f ? s.belt.start : s.belt.end;
        Vector3 roller = s.transform.TransformPoint(local) + Vector3.down * 0.15f;
        if (!Visible(roller, camPos))
            return;

        bool spark = Random.value < 0.45f;
        int count = spark ? Random.Range(1, 4) : Random.Range(2, 4);
        for (int k = 0; k < count; k++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Emit(new FxParticles.Spec
            {
                shape = FxParticles.Shape.Dot,
                position = roller + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.1f, 0.1f)),
                velocity = spark
                    ? new Vector3(dir.x * Random.Range(0.8f, 1.6f), Random.Range(0.6f, 1.2f), dir.y * 0.8f)
                    : new Vector3(dir.x * 0.3f, Random.Range(0f, 0.3f), dir.y * 0.3f),
                life = spark ? Random.Range(0.3f, 0.5f) : Random.Range(0.6f, 0.9f),
                startColor = spark ? SparkHot : Grit,
                endColor = spark ? SparkCool : GritEnd,
                startSize = 1f,
                endSize = 1f,
                gravity = spark ? 5f : 4f,
                drag = 0.5f,
                material = s.material,
            });
        }
    }

    void UpdateGear(Source s, float dt, Vector3 camPos)
    {
        if (s.gear == null || Mathf.Abs(s.gear.CurrentDegreesPerSecond) < 1f)
            return;
        s.timerA -= dt;
        if (s.timerA > 0f)
            return;
        s.timerA = Random.Range(4f, 10f);

        Vector3 at;
        if (s.shape != null && s.shape.enabled)
        {
            Bounds b = s.shape.bounds;
            Vector2 rim = Random.insideUnitCircle.normalized * 0.8f;
            at = b.center + new Vector3(rim.x * b.extents.x, rim.y * b.extents.y, -0.02f);
        }
        else
            at = s.transform.position;
        if (!Visible(at, camPos))
            return;

        Emit(new FxParticles.Spec
        {
            shape = FxParticles.Shape.Star,
            position = at,
            velocity = Vector3.zero,
            life = Random.Range(0.25f, 0.4f),
            startColor = Glint,
            endColor = Glint,
            startSize = 1f,
            endSize = 0.6f,
            twinkle = true,
            material = s.material,
        });
    }

    void UpdateNamed(Source s, float dt, Vector3 camPos)
    {
        Vector3 top;
        float halfWidth;
        if (s.shape != null && s.shape.enabled)
        {
            Bounds b = s.shape.bounds;
            top = new Vector3(b.center.x, b.max.y, b.center.z);
            halfWidth = b.extents.x;
        }
        else
        {
            top = s.transform.position + Vector3.up * 0.5f;
            halfWidth = 0.2f;
        }
        if (!Visible(top, camPos))
            return;

        s.timerA -= dt;
        switch (s.kind)
        {
            case Kind.Steam:
                if (s.timerA > 0f) return;
                s.timerA = Random.Range(0.7f, 1.5f);
                Emit(new FxParticles.Spec
                {
                    shape = FxParticles.Shape.Puff,
                    position = top + new Vector3(Random.Range(-halfWidth, halfWidth) * 0.6f, 0f, 0f),
                    velocity = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.5f, 0.8f), 0f),
                    life = Random.Range(1.4f, 2.2f),
                    startColor = SteamWhite,
                    endColor = SteamEnd,
                    startSize = 1f,
                    endSize = 2.5f,
                    gravity = -0.1f,
                    drag = 0.5f,
                    twinkle = true,
                    material = s.material,
                });
                break;

            case Kind.Pipe:
                // A leaky joint: a small wisp now and then, somewhere along the pipe
                if (s.timerA > 0f) return;
                s.timerA = Random.Range(3f, 7f);
                Vector3 leak = s.shape != null && s.shape.enabled
                    ? s.shape.bounds.center + new Vector3(Random.Range(-1f, 1f) * s.shape.bounds.extents.x, s.shape.bounds.extents.y * 0.5f, 0f)
                    : top;
                Emit(new FxParticles.Spec
                {
                    shape = FxParticles.Shape.Puff,
                    position = leak,
                    velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 0.7f), 0f),
                    life = Random.Range(0.9f, 1.4f),
                    startColor = SteamWhite,
                    endColor = SteamEnd,
                    startSize = 0.6f,
                    endSize = 1.6f,
                    gravity = -0.1f,
                    drag = 0.8f,
                    twinkle = true,
                    material = s.material,
                });
                break;

            case Kind.Smoke:
                if (s.timerA > 0f) return;
                s.timerA = Random.Range(0.5f, 0.9f);
                Emit(new FxParticles.Spec
                {
                    shape = FxParticles.Shape.Puff,
                    position = top + new Vector3(Random.Range(-halfWidth, halfWidth) * 0.4f, 0.05f, 0f),
                    velocity = new Vector3(Random.Range(0.05f, 0.25f), Random.Range(0.5f, 0.75f), 0f),
                    life = Random.Range(2.5f, 3.5f),
                    startColor = SootDark,
                    endColor = SootEnd,
                    startSize = 1.5f,
                    endSize = 4f,
                    gravity = -0.05f,
                    drag = 0.25f,
                    twinkle = true,
                    material = s.material,
                });
                break;

            case Kind.Furnace:
                Vector3 mouth = s.shape != null && s.shape.enabled ? s.shape.bounds.center : s.transform.position;
                if (s.timerA <= 0f)
                {
                    s.timerA = Random.Range(0.25f, 0.6f);
                    Vector2 flat = Random.insideUnitCircle * Mathf.Max(0.1f, halfWidth * 0.4f);
                    Emit(new FxParticles.Spec
                    {
                        shape = Random.value < 0.25f ? FxParticles.Shape.BigDot : FxParticles.Shape.Dot,
                        position = mouth + new Vector3(flat.x, 0f, flat.y * 0.3f),
                        velocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.5f, 1f), 0f),
                        life = Random.Range(0.8f, 1.4f),
                        startColor = Random.value < 0.5f ? FlameYellow : FlameOrange,
                        endColor = FlameRed,
                        startSize = 1f,
                        endSize = 1f,
                        gravity = -0.3f,
                        drag = 0.4f,
                        material = s.material,
                    });
                }
                // Soft glow pulse at the mouth
                s.timerB -= dt;
                if (s.timerB <= 0f)
                {
                    s.timerB = Random.Range(0.9f, 1.4f);
                    Emit(new FxParticles.Spec
                    {
                        shape = FxParticles.Shape.Puff,
                        position = mouth + Vector3.back * 0.05f,
                        velocity = Vector3.zero,
                        life = 1.3f,
                        startColor = Glow,
                        endColor = Glow,
                        startSize = 3.5f,
                        endSize = 4.5f,
                        twinkle = true,
                        material = s.material,
                    });
                }
                break;
        }
    }

    void Emit(in FxParticles.Spec spec)
    {
        if (spawnedThisFrame >= MaxSpawnsPerFrame)
            return;
        spawnedThisFrame++;
        FxParticles.Spawn(spec);
    }
}
