using System.Collections.Generic;
using UnityEngine;

// A conveyor belt: carries cargo (crates, iron parts) from Start to End and nudges the player standing on it.
// Cargo is a hazard: jump it, or wait for a gap. Everything runs in world time, so standing still
// nearly stops the belt and lets the player pick a gap.
// Put it on the belt root; the deck's solid collider is a child, and a trigger on this object (just above the deck)
// is the "standing on the belt" zone.
public class ConveyorBelt : MonoBehaviour
{
    // Local positions, along the deck top
    public Vector3 start = new Vector3(0f, 0.55f, 6f);
    public Vector3 end = new Vector3(0f, 0.55f, -6f);
    public float speed = 2.2f;

    [Header("Cargo")]
    public GameObject[] cargoPrefabs;
    // World-time seconds between pieces of cargo (picked at random in this range)
    public Vector2 spawnInterval = new Vector2(1.6f, 2.6f);
    public int seed = 3;
    // Fill the belt when the level starts, so it doesn't begin empty
    public bool prewarm = true;

    [Header("Look")]
    public Renderer beltSurface;
    public float textureScrollPerUnit = 0.5f;
    public GearSpinner[] rollers;

    readonly List<Transform> cargo = new List<Transform>();
    Rigidbody playerOnBelt;
    float spawnTimer;
    float scroll;
    System.Random rng;
    MaterialPropertyBlock block;
    Vector4 baseST = new Vector4(1f, 1f, 0f, 0f);

    Vector3 Direction => transform.TransformDirection((end - start).normalized);

    void Awake()
    {
        rng = new System.Random(seed);
        block = new MaterialPropertyBlock();
        if (beltSurface != null && beltSurface.sharedMaterial != null && beltSurface.sharedMaterial.HasProperty("_BaseMap_ST"))
            baseST = beltSurface.sharedMaterial.GetVector("_BaseMap_ST");
        spawnTimer = NextInterval();
    }

    void Start()
    {
        if (!prewarm)
            return;
        float length = (end - start).magnitude;
        for (float d = length - 1f; d > 0.5f; d -= speed * NextInterval())
            Spawn(d);
    }

    float NextInterval() => Mathf.Lerp(spawnInterval.x, spawnInterval.y, (float)rng.NextDouble());

    void Spawn(float distance)
    {
        if (cargoPrefabs == null || cargoPrefabs.Length == 0)
            return;
        var prefab = cargoPrefabs[rng.Next(cargoPrefabs.Length)];
        if (prefab == null)
            return;
        var piece = Instantiate(prefab, transform).transform;
        piece.localPosition = start + (end - start).normalized * distance;
        piece.localRotation = Quaternion.Euler(0f, rng.Next(4) * 90f + (float)rng.NextDouble() * 10f - 5f, 0f);
        cargo.Add(piece);
    }

    void Update()
    {
        float dt = WorldTime.Delta;
        float step = speed * dt;

        spawnTimer -= dt;
        if (spawnTimer <= 0f)
        {
            spawnTimer += NextInterval();
            Spawn(0f);
        }

        Vector3 dir = (end - start).normalized;
        float length = (end - start).magnitude;
        for (int i = cargo.Count - 1; i >= 0; i--)
        {
            Transform c = cargo[i];
            if (c == null) { cargo.RemoveAt(i); continue; }
            c.localPosition += dir * step;
            if (Vector3.Dot(c.localPosition - start, dir) >= length)
            {
                Destroy(c.gameObject);
                cargo.RemoveAt(i);
            }
        }

        if (beltSurface != null)
        {
            scroll = (scroll + step * textureScrollPerUnit) % 1f;
            beltSurface.GetPropertyBlock(block);
            block.SetVector("_BaseMap_ST", new Vector4(baseST.x, baseST.y, baseST.z, baseST.w + scroll));
            beltSurface.SetPropertyBlock(block);
        }

        if (rollers != null)
            foreach (var r in rollers)
                if (r != null) r.degreesPerSecond = speed * 120f;
    }

    void FixedUpdate()
    {
        if (playerOnBelt != null)
            playerOnBelt.position += Direction * speed * WorldTime.FixedDelta;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))
            playerOnBelt = other.attachedRigidbody;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody != null && other.attachedRigidbody == playerOnBelt)
            playerOnBelt = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.TransformPoint(start), transform.TransformPoint(end));
    }
}
