using System.Collections.Generic;
using UnityEngine;

// A cave villager in the Caveman Era: loops a short idle animation (drumming, dancing, grinding...), can walk
// a circle around a spot (a child running round the fire), can puff little sprites upward (music notes, "z"),
// and says one line each time the player talks to them. Everything runs on world time, so villagers freeze
// with the rest of the world when the player stands still.
// Keep the art on a child with a SpriteRenderer + Billboard; needs a trigger collider here for the talk range.
public class CaveNpc : Interactable
{
    public SpriteRenderer sprite;
    public Sprite[] frames;
    public float framesPerSecond = 3f;

    // Talking: each press shows the next line, then it wraps around
    [TextArea] public string[] lines;
    public string talkPrompt = "Talk";
    // Name on the dialogue's name plate. Empty = by era: "Caveman", "Factory Worker" (Steampunk), else "Villager"
    public string speakerName = "";

    public string SpeakerName
    {
        get
        {
            if (!string.IsNullOrEmpty(speakerName))
                return speakerName;
            var info = FindAnyObjectByType<LevelInfo>();
            Era era = info != null ? info.era
                : LevelManager.Instance != null ? LevelManager.Instance.CurrentEra : Era.Caveman;
            return era == Era.Caveman ? "Caveman" : era == Era.Steampunk ? "Factory Worker" : "Villager";
        }
    }

    // Walking a circle around Orbit Center (0 radius = stays put)
    public Vector3 orbitCenter;
    public float orbitRadius = 0f;
    public float orbitSpeed = 0.5f;

    // Little sprites that float up and fade (notes over a drummer, "z" over a sleeper)
    public Sprite[] puffSprites;
    public float puffInterval = 0f;
    public float puffLife = 1.6f;
    public float puffRise = 0.7f;
    public Color puffColor = new Color(0.96f, 0.9f, 0.76f);

    // Solid body so the player can't walk through: at least this wide. Made at runtime if the villager has none
    public float solidRadius = 0.55f;
    const float SolidHeight = 6f;  // taller than any jump, like every other blocker in the cave

    struct Puff
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public float age;
        public float drift;
    }

    readonly List<Puff> puffs = new List<Puff>();
    float frameTimer;
    int frameIndex;
    float puffTimer;
    float angle;
    int nextLine;
    Transform cam;
    Material puffMaterial;

    // A plain "Talk" names the villager from its object, e.g. "Talk to the flint knapper"
    public override string Prompt => string.IsNullOrEmpty(talkPrompt) || talkPrompt == "Talk"
        ? $"Talk to the {name.ToLowerInvariant()}"
        : talkPrompt;
    public override bool CanInteract => lines != null && lines.Length > 0;
    public override bool OutlineOnlyInRange => true;

    void Start()
    {
        if (Camera.main != null)
            cam = Camera.main.transform;
        if (sprite != null)
            puffMaterial = sprite.sharedMaterial;
        EnsureSolid();
        if (orbitRadius > 0f)
        {
            Vector3 offset = transform.localPosition - orbitCenter;
            angle = Mathf.Atan2(offset.z, offset.x);
        }
        // Start villagers at different points of their loops, so they don't move in lockstep
        frameIndex = frames != null && frames.Length > 0 ? Random.Range(0, frames.Length) : 0;
        puffTimer = puffInterval * 0.5f;
        ApplyFrame();
    }

    void EnsureSolid()
    {
        CapsuleCollider solid = null;
        foreach (var c in GetComponentsInChildren<CapsuleCollider>(true))
            if (!c.isTrigger) { solid = c; break; }

        if (solid == null)
        {
            var go = new GameObject("Solid");
            go.transform.SetParent(transform, false);
            solid = go.AddComponent<CapsuleCollider>();
            solid.height = SolidHeight;
            solid.center = new Vector3(0f, SolidHeight * 0.5f, 0f);
        }
        solid.radius = Mathf.Max(solid.radius, solidRadius);

        // A villager that walks needs a kinematic body, so its moving collider pushes the player cleanly
        if (orbitRadius > 0f && !TryGetComponent(out Rigidbody _))
        {
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
        }
    }

    public override void Interact(MedievalInteractor interactor)
    {
        if (!CanInteract || MedievalUI.Instance == null)
            return;
        MedievalUI.Instance.Talk(this, SpeakerName, lines[nextLine % lines.Length]);
        nextLine++;
    }

    // Walking away ends the conversation, so paused thoughts / story lines come back straight away
    protected override void OnTriggerExit(Collider other)
    {
        base.OnTriggerExit(other);
        if (MedievalUI.Instance != null && other.attachedRigidbody != null
            && other.attachedRigidbody.GetComponent<MedievalInteractor>() != null)
            MedievalUI.Instance.EndTalk(this);
    }

    void Update()
    {
        float dt = WorldTime.Delta;
        Animate(dt);
        Walk(dt);
        UpdatePuffs(dt);
    }

    void Animate(float dt)
    {
        if (sprite == null || frames == null || frames.Length < 2 || framesPerSecond <= 0f)
            return;
        frameTimer += dt;
        float duration = 1f / framesPerSecond;
        while (frameTimer >= duration)
        {
            frameTimer -= duration;
            frameIndex = (frameIndex + 1) % frames.Length;
            ApplyFrame();
        }
    }

    void ApplyFrame()
    {
        if (sprite != null && frames != null && frames.Length > 0)
            sprite.sprite = frames[frameIndex % frames.Length];
    }

    void Walk(float dt)
    {
        if (orbitRadius <= 0f)
            return;
        angle += orbitSpeed * dt;
        transform.localPosition = orbitCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius;
        // Moving left on screen when the tangent's x is negative
        if (sprite != null)
            sprite.flipX = -Mathf.Sin(angle) * Mathf.Sign(orbitSpeed) < 0f;
    }

    void UpdatePuffs(float dt)
    {
        if (puffSprites != null && puffSprites.Length > 0 && puffInterval > 0f)
        {
            puffTimer += dt;
            if (puffTimer >= puffInterval)
            {
                puffTimer -= puffInterval;
                SpawnPuff();
            }
        }

        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            Puff p = puffs[i];
            p.age += dt;
            if (p.transform == null || p.age >= puffLife)
            {
                if (p.transform != null)
                    Destroy(p.transform.gameObject);
                puffs.RemoveAt(i);
                continue;
            }
            float t = p.age / puffLife;
            p.transform.localPosition += new Vector3(Mathf.Sin(p.age * 4f) * p.drift * dt, puffRise * dt / puffLife * 1.6f, 0f);
            if (cam != null)
                p.transform.rotation = cam.rotation;
            Color c = puffColor;
            c.a = t < 0.2f ? t / 0.2f : 1f - Mathf.InverseLerp(0.6f, 1f, t);
            p.renderer.color = c;
            puffs[i] = p;
        }
    }

    void SpawnPuff()
    {
        var go = new GameObject("Puff");
        go.transform.SetParent(transform, false);
        float side = puffs.Count % 2 == 0 ? -0.35f : 0.35f;
        go.transform.localPosition = new Vector3(side, 1.7f, 0f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = puffSprites[Random.Range(0, puffSprites.Length)];
        if (puffMaterial != null)
            r.sharedMaterial = puffMaterial;
        r.sortingOrder = 3;
        puffs.Add(new Puff { transform = go.transform, renderer = r, age = 0f, drift = Random.Range(0.2f, 0.5f) });
    }
}
