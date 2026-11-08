using System.Collections.Generic;
using UnityEngine;

// A small pixel-art puff emitter for steam, smoke and sparks (the project has the Particle System module off).
// Puffs are pooled sprites that rise, grow and fade, all in world time, so they hang in the air when the
// player stands still. Set Rate for a steady stream, or call Burst(count) for a one-off cloud.
public class SteamEmitter : MonoBehaviour
{
    public Sprite sprite;
    public Material material;

    // Puffs per second while emitting (0 = only bursts)
    public float rate = 0f;
    public int burstCount = 12;

    [Header("Each puff")]
    public float lifetime = 1.1f;
    public float speed = 1.5f;
    public Vector3 direction = Vector3.up;
    [Range(0f, 1f)] public float spread = 0.25f;
    public float startSize = 0.35f;
    public float endSize = 0.9f;
    public Color startColor = new Color(0.95f, 0.95f, 0.92f, 0.85f);
    public Color endColor = new Color(0.8f, 0.8f, 0.8f, 0f);
    public float gravity = 0f;
    public int sortingOrder = 2;
    public int maxPuffs = 60;

    struct Puff
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 velocity;
        public float age;
        public float life;
    }

    readonly List<Puff> live = new List<Puff>();
    readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
    float emitAccumulator;
    Transform cam;

    void Start()
    {
        if (Camera.main != null)
            cam = Camera.main.transform;
    }

    public void Burst() => Burst(burstCount);

    public void Burst(int count)
    {
        for (int i = 0; i < count; i++)
            Spawn();
    }

    void Spawn()
    {
        if (sprite == null || live.Count >= maxPuffs)
            return;
        SpriteRenderer r = pool.Count > 0 ? pool.Pop() : CreatePuff();
        r.gameObject.SetActive(true);
        Vector3 dir = (transform.TransformDirection(direction.normalized) + Random.insideUnitSphere * spread).normalized;
        r.transform.position = transform.position + Random.insideUnitSphere * 0.08f;
        live.Add(new Puff
        {
            transform = r.transform,
            renderer = r,
            velocity = dir * speed * Random.Range(0.75f, 1.15f),
            age = 0f,
            life = lifetime * Random.Range(0.8f, 1.2f),
        });
    }

    SpriteRenderer CreatePuff()
    {
        var go = new GameObject("Puff");
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        if (material != null)
            r.sharedMaterial = material;
        r.sortingOrder = sortingOrder;
        return r;
    }

    void Update()
    {
        float dt = WorldTime.Delta;
        if (rate > 0f)
        {
            emitAccumulator += rate * dt;
            while (emitAccumulator >= 1f)
            {
                emitAccumulator -= 1f;
                Spawn();
            }
        }
        else
            emitAccumulator = 0f;

        Quaternion facing = cam != null ? cam.rotation : Quaternion.identity;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            Puff p = live[i];
            p.age += dt;
            if (p.age >= p.life)
            {
                p.renderer.gameObject.SetActive(false);
                pool.Push(p.renderer);
                live.RemoveAt(i);
                continue;
            }
            float t = p.age / p.life;
            p.velocity += Vector3.down * gravity * dt;
            p.velocity *= 1f - Mathf.Clamp01(1.6f * dt);
            p.transform.position += p.velocity * dt;
            p.transform.rotation = facing;
            p.transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, t);
            p.renderer.color = Color.Lerp(startColor, endColor, t);
            live[i] = p;
        }
    }
}
