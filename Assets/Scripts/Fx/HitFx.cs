using UnityEngine;

// A quick impact burst when something takes a hit: a white flash star, then tufts flung away from the attacker
// (brown fur for animals). Runs on world time like the rest of the effects, so the burst hangs in the air
// if the player freezes the world straight after the hit.
public static class HitFx
{
    static readonly Color Flash = new Color(1f, 0.97f, 0.85f, 1f);
    static readonly Color FlashEnd = new Color(1f, 0.85f, 0.5f, 0f);
    static readonly Color FurLight = new Color(0.62f, 0.43f, 0.27f, 1f);
    static readonly Color FurDark = new Color(0.36f, 0.23f, 0.14f, 1f);

    // away: the direction the hit pushes the target (from the attacker towards it)
    public static void Fur(Component target, Vector3 position, Vector3 away, int count = 10)
    {
        if (!FxParticles.OnScreen(position))
            return;
        Material material = FxParticles.MaterialOf(target);
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.zero;

        FxParticles.Spawn(new FxParticles.Spec
        {
            shape = FxParticles.Shape.Star,
            position = position,
            life = 0.18f,
            startColor = Flash,
            endColor = FlashEnd,
            startSize = 1.6f,
            endSize = 2.4f,
            material = material,
        });

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = (away + Random.insideUnitSphere * 0.8f).normalized;
            FxParticles.Spawn(new FxParticles.Spec
            {
                shape = Random.value < 0.3f ? FxParticles.Shape.BigDot : FxParticles.Shape.Dot,
                position = position + Random.insideUnitSphere * 0.15f,
                velocity = dir * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(0.5f, 1.5f),
                life = Random.Range(0.35f, 0.6f),
                startColor = Random.value < 0.5f ? FurLight : FurDark,
                endColor = new Color(FurDark.r, FurDark.g, FurDark.b, 0f),
                startSize = 1f,
                endSize = 0.8f,
                gravity = 6f,
                drag = 2f,
                material = material,
            });
        }
    }
}
