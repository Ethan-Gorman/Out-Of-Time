using UnityEngine;

// A grey dust puff, once, when an enemy dies (skeletons, clockwork guards, the boar).
// Added automatically by FxBootstrap; it only watches each enemy's public death flag, so their scripts are untouched.
// The dust lives in the shared FxParticles pool, so it finishes even though the body is destroyed soon after.
[DisallowMultipleComponent]
public class DeathPuff : MonoBehaviour
{
    public int count = 12;

    static readonly Color DustStart = new Color(0.78f, 0.76f, 0.72f, 0.9f);
    static readonly Color DustEnd = new Color(0.55f, 0.55f, 0.55f, 0f);

    SkeletonEnemy skeleton;
    ClockworkGuard guard;
    WildBoar boar;
    Material material;

    // Call for anything spawned after the scene loaded
    public static void Add(Component target)
    {
        if (target != null && target.GetComponent<DeathPuff>() == null)
            target.gameObject.AddComponent<DeathPuff>();
    }

    void Awake()
    {
        skeleton = GetComponent<SkeletonEnemy>();
        guard = GetComponent<ClockworkGuard>();
        boar = GetComponent<WildBoar>();
        material = FxParticles.MaterialOf(this);
    }

    // The boar has no public IsDead, but it can only stop being interactable by dying
    bool Dead()
    {
        if (skeleton != null) return skeleton.IsDead;
        if (guard != null) return guard.IsDead;
        if (boar != null) return !boar.CanInteract;
        return false;
    }

    void Update()
    {
        if (!Dead())
            return;
        // Once only: nothing left to watch
        enabled = false;
        Puff();
    }

    void Puff()
    {
        Vector3 feet = transform.position + Vector3.up * 0.3f;
        for (int i = 0; i < count; i++)
        {
            Vector2 flat = Random.insideUnitCircle.normalized;
            float speed = Random.Range(0.8f, 1.6f);
            FxParticles.Spawn(new FxParticles.Spec
            {
                shape = Random.value < 0.6f ? FxParticles.Shape.Puff : FxParticles.Shape.BigDot,
                position = feet + new Vector3(flat.x, 0f, flat.y) * 0.2f,
                velocity = new Vector3(flat.x * speed, Random.Range(0.3f, 0.8f), flat.y * speed),
                life = Random.Range(0.5f, 0.8f),
                startColor = DustStart,
                endColor = DustEnd,
                startSize = 1f,
                endSize = 1.6f,
                drag = 3f,
                material = material,
            });
        }
    }
}
