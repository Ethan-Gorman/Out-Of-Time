using UnityEngine;

// Little twinkles around a pickup: cyan on crystals, gold on coins.
// Added automatically by FxBootstrap. Runs on world time and skips work while the object is off screen.
[DisallowMultipleComponent]
public class FxSparkle : MonoBehaviour
{
    public static readonly Color CrystalColor = new Color(0.45f, 0.95f, 1f, 1f);
    public static readonly Color CoinColor = new Color(1f, 0.86f, 0.3f, 1f);

    public Color color = CrystalColor;
    // Twinkles per second
    public float rate = 1.4f;
    public float radius = 0.35f;

    Renderer shape;
    Material material;
    float accumulator;

    // Call for anything spawned after the scene loaded
    public static void Add(Component target, Color color)
    {
        if (target == null || target.GetComponent<FxSparkle>() != null)
            return;
        target.gameObject.AddComponent<FxSparkle>().color = color;
    }

    void Start()
    {
        shape = FxParticles.ShapeOf(this);
        material = FxParticles.MaterialOf(this);
        accumulator = Random.value;
    }

    void Update()
    {
        if (rate <= 0f)
            return;
        accumulator += rate * WorldTime.Delta;
        if (accumulator < 1f)
            return;
        accumulator = Mathf.Min(accumulator - 1f, 1f);

        Vector3 centre = FxParticles.CentreOf(shape, transform, 0.4f);
        if (!FxParticles.OnScreen(centre))
            return;

        FxParticles.Spawn(new FxParticles.Spec
        {
            shape = Random.value < 0.6f ? FxParticles.Shape.Star : FxParticles.Shape.Dot,
            position = centre + Random.insideUnitSphere * radius,
            velocity = Vector3.up * 0.1f,
            life = Random.Range(0.45f, 0.7f),
            startColor = color,
            endColor = Color.white,
            startSize = 1f,
            endSize = 1f,
            twinkle = true,
            material = material,
        });
    }
}
