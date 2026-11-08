using UnityEngine;

// A few orange and yellow embers drifting up off a flame and fading out.
// Added automatically to every TorchFlicker by FxBootstrap. Runs on world time, so embers hang in the air when the
// player stands still, and does nothing while the flame's light is off or the torch is off screen.
[DisallowMultipleComponent]
public class FlameEmbers : MonoBehaviour
{
    // Embers per second
    public float rate = 2.5f;
    public float spawnRadius = 0.12f;

    static readonly Color Yellow = new Color(1f, 0.86f, 0.38f, 1f);
    static readonly Color Orange = new Color(1f, 0.55f, 0.16f, 1f);
    static readonly Color Dying = new Color(0.85f, 0.22f, 0.05f, 0f);

    TorchFlicker flicker;
    Material material;
    float accumulator;

    void Start()
    {
        flicker = GetComponent<TorchFlicker>();
        material = FxParticles.MaterialOf(this);
        // Torches placed side by side shouldn't puff in step
        accumulator = Random.value;
    }

    void Update()
    {
        if (rate <= 0f)
            return;
        accumulator += rate * WorldTime.Delta;
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

        Vector2 flat = Random.insideUnitCircle * spawnRadius;
        bool big = Random.value < 0.2f;
        FxParticles.Spawn(new FxParticles.Spec
        {
            shape = big ? FxParticles.Shape.BigDot : FxParticles.Shape.Dot,
            position = origin + new Vector3(flat.x, 0f, flat.y),
            velocity = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.5f, 0.9f), Random.Range(-0.15f, 0.15f)),
            life = Random.Range(0.9f, 1.5f),
            startColor = Random.value < 0.5f ? Yellow : Orange,
            endColor = Dying,
            startSize = 1f,
            endSize = 1f,
            gravity = -0.3f,
            drag = 0.4f,
            material = material,
        });
    }
}
