using UnityEngine;

// A Time Coin: walk into it for bonus seconds on the Clock (ClockUI pops up "+5s").
// Spins and bobs in world time, so it freezes with the rest of the world. Works in any era: drop a prefab in.
// Keep the art on the "Visual" child and swap it freely; the trigger collider sits on this object.
[RequireComponent(typeof(Collider))]
public class TimeCoin : MonoBehaviour
{
    public float seconds = 5f;
    public Transform visual;
    public float spinDegreesPerSecond = 140f;
    public float bobHeight = 0.12f;
    public float bobSpeed = 2.4f;

    // Optional: a puff of sparks left behind on pickup
    public SteamEmitter collectBurst;

    // Optional: the pickup sound. Empty uses the shared synth blip (CavemanSfx), so no audio file is needed
    public AudioClip collectSound;
    [Range(0f, 1f)] public float volume = 0.5f;

    Vector3 visualBase;
    float phase;
    bool taken;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        if (visual != null)
            visualBase = visual.localPosition;
        // Coins next to each other shouldn't bob in lockstep
        phase = Random.value * Mathf.PI * 2f;
        // Coins dropped in after the scene loaded still get their outline
        InteractOutline.Add(this);
    }

    void Update()
    {
        if (visual == null)
            return;
        phase += WorldTime.Delta * bobSpeed;
        visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(phase) * bobHeight);
        visual.Rotate(0f, spinDegreesPerSecond * WorldTime.Delta, 0f, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        // attachedRigidbody, so it still works when the player's collider sits on a child
        if (taken || other.attachedRigidbody == null || !other.attachedRigidbody.CompareTag("Player"))
            return;
        var clock = Clock.Instance;
        // Out of time, or the level is already finished: leave it
        if (clock == null || clock.IsOut || clock.IsFrozen)
            return;

        taken = true;
        clock.AddTime(seconds);
        PlaySound();
        if (collectBurst != null)
        {
            collectBurst.transform.SetParent(null, true);
            collectBurst.Burst();
            Destroy(collectBurst.gameObject, 3f);
        }
        Destroy(gameObject);
    }

    // A flat (2D) one-shot: the camera is too far away for a 3D sound at the coin
    void PlaySound()
    {
        var clip = collectSound != null ? collectSound : CavemanSfx.Get(CavemanSfx.Sound.Pickup);
        if (clip == null)
            return;
        var go = new GameObject("Time coin sound");
        var source = go.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.volume = volume;
        source.pitch = Mathf.Lerp(0.85f, 1f, WorldTime.Scale) * Random.Range(0.96f, 1.06f);
        source.PlayOneShot(clip);
        Destroy(go, clip.length / Mathf.Max(0.1f, source.pitch) + 0.1f);
    }
}
