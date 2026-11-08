using System.Collections.Generic;
using UnityEngine;

// The Caveman Era's wild boar. Sleeps (frozen, "z"s floating up) until the player has crafted the Stone Axe,
// then wakes up and charges whenever the player comes near. Has a solid body so the player can't walk through it.
[RequireComponent(typeof(Rigidbody))]
public class WildBoar : Interactable
{
    [Header("Movement")]
    public float speed = 2.2f;
    public float aggroRadius = 8f;
    public float contactTimeCost = -1f;

    [Header("Combat")]
    public int health = 3;
    public int axeDamage = 1;
    public float hitPause = 0.35f;
    public float knockbackSpeed = 5f;
    public float flashTime = 0.18f;
    [SerializeField, Tooltip("Distance from the player at which the attack prompt is available.")]
    float attackRange = 2f;

    [Header("Art")]
    public Transform visual;
    public SpriteRenderer sprite;
    public Sprite[] walkFrames;
    public float animationRate = 8f;

    [Header("Sleep")]
    [TextArea] public string[] sleepLines = { "It's fast asleep. I'm not waking that thing bare-handed. I need a Stone Axe first." };
    public float wakeTime = 0.7f;
    public float zInterval = 0.9f;
    public float zLife = 1.8f;
    public float zRise = 0.8f;
    public Color zColor = new Color(0.96f, 0.9f, 0.76f);

    [Header("Body")]
    // Solid body so the player can't walk through: thinner than the contact trigger, so a charge still lands
    public float solidRadius = 0.6f;
    const float SolidHeight = 6f;  // taller than any jump, like every other blocker in the cave

    struct Puff
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public float age;
        public float drift;
    }

    static Sprite zSprite;

    Rigidbody body;
    Transform player;
    MedievalInteractor playerInteractor;
    float pauseLeft;
    float animationTimer;
    int animationFrame;
    bool dead;
    bool aggro;
    Vector3 knockback;
    float flashLeft;
    Color baseColor;
    bool hasBaseColor;
    float deathTime;
    Vector3 deathBaseScale;
    bool awake;
    float wakeLeft;
    Vector3 wakeBaseScale;
    float puffTimer;
    readonly List<Puff> puffs = new List<Puff>();
    float playerRadius = 0.3f;

    bool HasAxe => CavemanLevel.Instance != null && CavemanLevel.Instance.HasAxe;

    // Asleep or still blinking awake: stands still and can't hurt the player
    bool Calm => !awake || wakeLeft > 0f;

    public override string Prompt => HasAxe ? "attack boar" : "check sleeping boar";
    public override bool CanInteract => !dead;
    public override bool KeepUsedOutline => false;
    public override bool IsEnemy => true;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        var playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerInteractor = playerObject.GetComponent<MedievalInteractor>();
            if (playerObject.TryGetComponent(out CapsuleCollider capsule) && !capsule.isTrigger)
                playerRadius = capsule.radius;
        }
        AddSolid();
        puffTimer = zInterval * 0.5f;
    }

    // A non-trigger capsule on a child, so the kinematic body blocks the player while the root's trigger
    // keeps handling contact damage and the attack range
    void AddSolid()
    {
        var go = new GameObject("Solid");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var solid = go.AddComponent<CapsuleCollider>();
        solid.radius = solidRadius;
        solid.height = SolidHeight;
        solid.center = new Vector3(0f, SolidHeight * 0.5f, 0f);
    }

    public override void Interact(MedievalInteractor interactor)
    {
        if (dead)
            return;

        if (!HasAxe)
        {
            CavemanFx.Play(CavemanSfx.Sound.Deny);
            if (MedievalUI.Instance != null)
                MedievalUI.Instance.Say(sleepLines);
            return;
        }

        // Hit before Update noticed the axe (e.g. the presenter skip): wake it on the spot
        if (!awake)
            Wake();
        if (wakeLeft > 0f && visual != null)
            visual.localScale = wakeBaseScale;
        wakeLeft = 0f;

        health -= axeDamage;
        pauseLeft = hitPause;
        flashLeft = flashTime;
        if (player != null)
            knockback = Flat(body.position - player.position).normalized * knockbackSpeed;

        CavemanFx.Play(CavemanSfx.Sound.BoarHit);
        CavemanFx.Shake(health <= 0 ? 0.5f : 0.3f);
        Vector3 hitAt = visual != null ? visual.position : transform.position;
        HitFx.Fur(this, hitAt, player != null ? hitAt - player.position : Vector3.zero, health <= 0 ? 16 : 10);
        if (health <= 0)
            Die();
        else
            CavemanFx.Play(CavemanSfx.Sound.Squeal, 0.6f);
    }

    void FixedUpdate()
    {
        if (dead || player == null || Calm)
            return;

        if (pauseLeft > 0f)
        {
            pauseLeft -= WorldTime.FixedDelta;
            // Knocked back by the axe, sliding to a stop
            if (knockback.sqrMagnitude > 0.001f)
            {
                body.MovePosition(body.position + knockback * WorldTime.FixedDelta);
                knockback = Vector3.MoveTowards(knockback, Vector3.zero, knockbackSpeed * 4f * WorldTime.FixedDelta);
            }
            return;
        }

        Vector3 toPlayer = player.position - body.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > aggroRadius * aggroRadius || toPlayer.sqrMagnitude < 0.01f)
        {
            aggro = false;
            return;
        }

        // Squeal when it first spots the player, so the charge never comes out of nowhere
        if (!aggro)
        {
            aggro = true;
            CavemanFx.Play(CavemanSfx.Sound.Squeal);
            if (visual != null)
                CavemanFx.Text(visual.position + Vector3.up * 1.2f, "!", new Color(1f, 0.35f, 0.25f));
        }

        // Stop once the bodies touch: the contact trigger is wider, so the hit still lands, and a kinematic
        // body that kept pushing would shove the player through walls or pin them in a corner
        float room = Mathf.Max(0f, toPlayer.magnitude - (solidRadius + playerRadius + 0.02f));
        Vector3 step = Vector3.ClampMagnitude(toPlayer.normalized * speed * WorldTime.FixedDelta, room);
        body.MovePosition(body.position + step);
        if (visual != null && Mathf.Abs(toPlayer.x) > 0.01f)
        {
            Vector3 scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * (toPlayer.x < 0f ? -1f : 1f);
            visual.localScale = scale;
        }
    }

    void Update()
    {
        if (playerInteractor != null)
        {
            if (!dead && player != null && Flat(player.position - transform.position).sqrMagnitude <= attackRange * attackRange)
                playerInteractor.Enter(this);
            else
                playerInteractor.Exit(this);
        }

        if (!dead || deathTime < 0.5f)
            UpdateFlash();
        UpdatePuffs(WorldTime.Delta);
        if (dead)
        {
            UpdateDeath();
            return;
        }

        if (!awake && HasAxe)
            Wake();
        if (Calm)
        {
            UpdateWake();
            if (sprite != null && walkFrames != null && walkFrames.Length > 0)
                sprite.sprite = walkFrames[0];
            return;
        }

        if (sprite == null || walkFrames == null || walkFrames.Length == 0)
            return;

        if (pauseLeft > 0f || player == null || Flat(player.position - transform.position).sqrMagnitude > aggroRadius * aggroRadius)
        {
            sprite.sprite = walkFrames[0];
            animationTimer = 0f;
            animationFrame = 0;
            return;
        }

        animationTimer += WorldTime.Delta;
        if (animationTimer >= 1f / animationRate)
        {
            animationTimer = 0f;
            animationFrame = (animationFrame + 1) % walkFrames.Length;
            sprite.sprite = walkFrames[animationFrame];
        }
    }

    protected override void OnTriggerEnter(Collider other) => TouchPlayer(other);

    // Attack range is checked above; leaving the smaller contact trigger must not hide it.
    protected override void OnTriggerExit(Collider other) { }

    void OnTriggerStay(Collider other) => TouchPlayer(other);

    void OnDisable()
    {
        if (playerInteractor != null)
            playerInteractor.Exit(this);
    }

    void TouchPlayer(Collider other)
    {
        if (dead || Calm || other.attachedRigidbody == null)
            return;

        var damage = other.attachedRigidbody.GetComponent<PlayerDamage>();
        if (damage != null && damage.Hit(body.position, contactTimeCost))
            pauseLeft = hitPause;
    }

    // The axe is crafted: a startled snort and a stretch, then it behaves as normal
    void Wake()
    {
        awake = true;
        wakeLeft = wakeTime;
        if (visual != null)
        {
            wakeBaseScale = visual.localScale;
            CavemanFx.Text(visual.position + Vector3.up * 1.2f, "?!", new Color(1f, 0.85f, 0.4f));
        }
        CavemanFx.Play(CavemanSfx.Sound.Squeal, 0.5f);
    }

    // Stretches up and settles back over the wake-up beat, in world time.
    // Scale only: Billboard owns the sprite's position and rotation
    void UpdateWake()
    {
        if (!awake || wakeLeft <= 0f)
            return;
        wakeLeft -= WorldTime.Delta;
        if (visual == null)
            return;
        float t = 1f - Mathf.Clamp01(wakeLeft / wakeTime);
        float stretch = Mathf.Sin(t * Mathf.PI) * 0.15f;
        visual.localScale = Vector3.Scale(wakeBaseScale, new Vector3(1f - stretch * 0.5f, 1f + stretch, 1f));
        if (wakeLeft <= 0f)
            visual.localScale = wakeBaseScale;
    }

    // Little "z"s that float up and fade while it sleeps, like CaveNpc's puffs over a sleeping villager.
    // Ones already in the air finish fading after it wakes
    void UpdatePuffs(float dt)
    {
        if (!awake && !dead && zInterval > 0f)
        {
            puffTimer += dt;
            if (puffTimer >= zInterval)
            {
                puffTimer -= zInterval;
                SpawnPuff();
            }
        }

        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            Puff p = puffs[i];
            p.age += dt;
            if (p.transform == null || p.age >= zLife)
            {
                if (p.transform != null)
                    Destroy(p.transform.gameObject);
                puffs.RemoveAt(i);
                continue;
            }
            float t = p.age / zLife;
            p.transform.localPosition += new Vector3(Mathf.Sin(p.age * 4f) * p.drift * dt, zRise * dt / zLife * 1.6f, 0f);
            if (Camera.main != null)
                p.transform.rotation = Camera.main.transform.rotation;
            Color c = zColor;
            c.a = t < 0.2f ? t / 0.2f : 1f - Mathf.InverseLerp(0.6f, 1f, t);
            p.renderer.color = c;
            puffs[i] = p;
        }
    }

    void SpawnPuff()
    {
        var go = new GameObject("Z");
        go.transform.SetParent(transform, false);
        float side = puffs.Count % 2 == 0 ? -0.3f : 0.3f;
        go.transform.localPosition = new Vector3(side, 1.5f, 0f);
        // Alternate big and small, like a cartoon "zZz"
        go.transform.localScale = Vector3.one * (puffs.Count % 2 == 0 ? 1f : 0.7f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = ZSprite();
        if (sprite != null)
            r.sharedMaterial = sprite.sharedMaterial;
        r.sortingOrder = 3;
        r.color = new Color(zColor.r, zColor.g, zColor.b, 0f);
        puffs.Add(new Puff { transform = go.transform, renderer = r, age = 0f, drift = Random.Range(0.2f, 0.5f) });
    }

    // There's no "z" art, so draw a tiny pixel one once (5x5 letter, 1px clear border, 16 px per unit like the rest)
    static Sprite ZSprite()
    {
        if (zSprite != null)
            return zSprite;
        string[] rows =
        {
            ".......",
            ".#####.",
            "....#..",
            "...#...",
            "..#....",
            ".#####.",
            ".......",
        };
        var texture = new Texture2D(7, 7, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
                texture.SetPixel(x, 6 - y, rows[y][x] == '#' ? Color.white : Color.clear);
        texture.Apply();
        zSprite = Sprite.Create(texture, new Rect(0, 0, 7, 7), new Vector2(0.5f, 0.5f), 16f);
        return zSprite;
    }

    void Die()
    {
        dead = true;
        foreach (var collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;
        CavemanFx.Play(CavemanSfx.Sound.Squeal);
        if (visual != null)
        {
            deathBaseScale = visual.localScale;
            CavemanFx.Text(visual.position + Vector3.up * 1.2f, $"+{BoarRewardText()}s", new Color(0.55f, 1f, 0.55f));
        }
        if (CavemanLevel.Instance != null)
            CavemanLevel.Instance.OnBoarKilled();
    }

    string BoarRewardText() => CavemanLevel.Instance != null ? Mathf.RoundToInt(CavemanLevel.Instance.boarReward).ToString() : "10";

    // Flashes red after each hit
    void UpdateFlash()
    {
        if (sprite == null)
            return;
        if (!hasBaseColor)
        {
            baseColor = sprite.color;
            hasBaseColor = true;
        }
        if (flashLeft > 0f)
            flashLeft -= WorldTime.Delta;
        float t = Mathf.Clamp01(flashLeft / flashTime);
        sprite.color = Color.Lerp(baseColor, new Color(1f, 0.25f, 0.2f, baseColor.a), t);
    }

    // Squashes flat, then fades out, in world time.
    // Scale and colour only: Billboard owns the sprite's rotation
    void UpdateDeath()
    {
        deathTime += WorldTime.Delta;
        if (visual != null)
        {
            float squash = Mathf.Clamp01(deathTime / 0.15f);
            visual.localScale = Vector3.Scale(deathBaseScale, new Vector3(1f + 0.35f * squash, 1f - 0.6f * squash, 1f));
        }
        if (sprite != null && deathTime > 0.5f)
        {
            var color = sprite.color;
            color.a = baseColor.a * Mathf.Clamp01(1f - (deathTime - 0.5f) / 0.5f);
            sprite.color = color;
        }
        if (deathTime >= 1f)
            Destroy(gameObject);
    }

    static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}