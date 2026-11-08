using UnityEngine;

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

    [TextArea] public string[] needAxeLines = { "It's too dangerous to fight bare-handed. I need a Stone Axe." };

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

    bool HasAxe => CavemanLevel.Instance != null && CavemanLevel.Instance.HasAxe;

    public override string Prompt => "attack boar";
    public override bool CanInteract => !dead;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        var playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerInteractor = playerObject.GetComponent<MedievalInteractor>();
        }
    }

    public override void Interact(MedievalInteractor interactor)
    {
        if (dead)
            return;

        if (!HasAxe)
        {
            CavemanFx.Play(CavemanSfx.Sound.Deny);
            if (MedievalUI.Instance != null)
                MedievalUI.Instance.Say(needAxeLines);
            return;
        }

        health -= axeDamage;
        pauseLeft = hitPause;
        flashLeft = flashTime;
        if (player != null)
            knockback = Flat(body.position - player.position).normalized * knockbackSpeed;

        CavemanFx.Play(CavemanSfx.Sound.BoarHit);
        CavemanFx.Shake(health <= 0 ? 0.5f : 0.3f);
        if (health <= 0)
            Die();
        else
            CavemanFx.Play(CavemanSfx.Sound.Squeal, 0.6f);
    }

    void FixedUpdate()
    {
        if (dead || player == null)
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

        Vector3 step = Vector3.ClampMagnitude(toPlayer.normalized * speed * WorldTime.FixedDelta, toPlayer.magnitude);
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
        if (dead)
        {
            UpdateDeath();
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
        if (dead || other.attachedRigidbody == null)
            return;

        var damage = other.attachedRigidbody.GetComponent<PlayerDamage>();
        if (damage != null && damage.Hit(body.position, contactTimeCost))
            pauseLeft = hitPause;
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

    string BoarRewardText() => CavemanLevel.Instance != null ? Mathf.RoundToInt(CavemanLevel.Instance.boarReward).ToString() : "30";

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