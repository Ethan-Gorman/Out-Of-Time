using UnityEngine;

// Embers drifting up off a fire and fading out. How many, how big and what colour depends on the fire
// (a roaring bonfire vs a clay oven vs a coal furnace), set by FxBootstrap from the kind of source.
// Runs on world time, so embers hang in the air when the player stands still, and does nothing while the
// fire's light is off or the fire is off screen.
[DisallowMultipleComponent]
public class FlameEmbers : MonoBehaviour
{
    public enum Kind { Bonfire, Campfire, Oven, Furnace, Torch, Lantern }

    public struct Profile
    {
        public float rate;          // embers per second
        public float spawnRadius;   // how wide the fire is
        public float bigChance;     // share of 2 px embers
        public Vector2 rise;        // upward speed range
        public float spread;        // sideways speed
        public Vector2 life;        // lifetime range
        public Vector2 size;        // size multiplier range
        public float shrink;        // end size as a share of start size (0 = default 0.6)
        public bool twoTone;        // each ember is exactly hot or warm, instead of a blend between them
        public Color hot, warm, dying;
    }

    public static Profile For(Kind kind)
    {
        switch (kind)
        {
            case Kind.Bonfire:
                // Big and lively: lots of embers, some white-hot, flung a bit wider and higher
                return new Profile
                {
                    rate = 7f, spawnRadius = 0.55f, bigChance = 0.35f, rise = new Vector2(0.7f, 1.4f), spread = 0.3f,
                    life = new Vector2(1.1f, 2f), size = new Vector2(1f, 1.6f),
                    hot = new Color(1f, 0.95f, 0.7f, 1f), warm = new Color(1f, 0.55f, 0.16f, 1f), dying = new Color(0.85f, 0.2f, 0.05f, 0f),
                };
            case Kind.Campfire:
                return new Profile
                {
                    rate = 3f, spawnRadius = 0.25f, bigChance = 0.2f, rise = new Vector2(0.5f, 0.9f), spread = 0.18f,
                    life = new Vector2(0.9f, 1.5f), size = new Vector2(1f, 1.2f),
                    hot = new Color(1f, 0.86f, 0.38f, 1f), warm = new Color(1f, 0.55f, 0.16f, 1f), dying = new Color(0.85f, 0.22f, 0.05f, 0f),
                };
            case Kind.Oven:
                // A banked clay oven: a few slow, deep orange embers out of the mouth
                return new Profile
                {
                    rate = 1.2f, spawnRadius = 0.2f, bigChance = 0.1f, rise = new Vector2(0.3f, 0.6f), spread = 0.1f,
                    life = new Vector2(0.8f, 1.3f), size = new Vector2(0.8f, 1f),
                    hot = new Color(1f, 0.6f, 0.2f, 1f), warm = new Color(0.9f, 0.32f, 0.08f, 1f), dying = new Color(0.5f, 0.1f, 0.05f, 0f),
                };
            case Kind.Torch:
                // The original torch embers: either yellow or orange, a steady size, drifting up and fading
                return new Profile
                {
                    rate = 2.5f, spawnRadius = 0.12f, bigChance = 0.2f, rise = new Vector2(0.5f, 0.9f), spread = 0.15f,
                    life = new Vector2(0.9f, 1.5f), size = new Vector2(1f, 1f), shrink = 1f, twoTone = true,
                    hot = new Color(1f, 0.86f, 0.38f, 1f), warm = new Color(1f, 0.55f, 0.16f, 1f), dying = new Color(0.85f, 0.22f, 0.05f, 0f),
                };
            case Kind.Lantern:
                // A candle behind glass: only the odd tiny spark escapes
                return new Profile
                {
                    rate = 0.4f, spawnRadius = 0.05f, bigChance = 0f, rise = new Vector2(0.3f, 0.5f), spread = 0.05f,
                    life = new Vector2(0.5f, 0.8f), size = new Vector2(0.7f, 0.9f),
                    hot = new Color(1f, 0.95f, 0.65f, 1f), warm = new Color(1f, 0.75f, 0.35f, 1f), dying = new Color(0.9f, 0.4f, 0.1f, 0f),
                };
            default:
                // Coal furnace: fast red-orange sparks that die quickly
                return new Profile
                {
                    rate = 4.5f, spawnRadius = 0.35f, bigChance = 0.15f, rise = new Vector2(1f, 1.8f), spread = 0.35f,
                    life = new Vector2(0.5f, 0.9f), size = new Vector2(0.8f, 1.2f),
                    hot = new Color(1f, 0.7f, 0.3f, 1f), warm = new Color(1f, 0.3f, 0.1f, 1f), dying = new Color(0.6f, 0.08f, 0.05f, 0f),
                };
        }
    }

    public Kind kind = Kind.Campfire;

    TorchFlicker flicker;
    Material material;
    Profile profile;
    float accumulator;

    void Start()
    {
        flicker = GetComponent<TorchFlicker>();
        material = FxParticles.MaterialOf(this);
        profile = For(kind);
        // Fires side by side shouldn't puff in step
        accumulator = Random.value;
    }

    void Update()
    {
        if (profile.rate <= 0f)
            return;
        accumulator += profile.rate * WorldTime.Delta;
        if (accumulator < 1f)
            return;
        // At most one per frame, so a long frame doesn't dump a clump
        accumulator = Mathf.Min(accumulator - 1f, 1f);

        Light flame = flicker != null ? flicker.flame : null;
        if (flame != null && !flame.isActiveAndEnabled)
            return;
        Vector3 origin = flame != null ? flame.transform.position : transform.position;
        if (!FxParticles.OnScreen(origin))
            return;

        Vector2 flat = Random.insideUnitCircle * profile.spawnRadius;
        bool big = Random.value < profile.bigChance;
        float size = Random.Range(profile.size.x, profile.size.y);
        FxParticles.Spawn(new FxParticles.Spec
        {
            shape = big ? FxParticles.Shape.BigDot : FxParticles.Shape.Dot,
            position = origin + new Vector3(flat.x, 0f, flat.y),
            velocity = new Vector3(Random.Range(-profile.spread, profile.spread), Random.Range(profile.rise.x, profile.rise.y), Random.Range(-profile.spread, profile.spread)),
            life = Random.Range(profile.life.x, profile.life.y),
            // Anywhere between white-hot and orange, so no two embers match (or one of the two, for torches)
            startColor = profile.twoTone ? (Random.value < 0.5f ? profile.hot : profile.warm) : Color.Lerp(profile.hot, profile.warm, Random.value),
            endColor = profile.dying,
            startSize = size,
            endSize = size * (profile.shrink > 0f ? profile.shrink : 0.6f),
            gravity = -0.3f,
            drag = 0.4f,
            material = material,
        });
    }
}
