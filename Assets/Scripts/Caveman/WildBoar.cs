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

    [Header("Art")]
    public Transform visual;
    public SpriteRenderer sprite;
    public Sprite[] walkFrames;
    public float animationRate = 8f;

    [TextArea] public string[] needAxeLines = { "It's too dangerous to fight bare-handed. I need a Stone Axe." };

    Rigidbody body;
    Transform player;
    float pauseLeft;
    float animationTimer;
    int animationFrame;
    bool dead;

    bool HasAxe => CavemanLevel.Instance != null && CavemanLevel.Instance.HasAxe;

    public override string Prompt => HasAxe ? "Attack wild boar" : "Wild boar";
    public override bool CanInteract => !dead;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        var playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    public override void Interact(MedievalInteractor interactor)
    {
        if (dead)
            return;

        if (!HasAxe)
        {
            if (MedievalUI.Instance != null)
                MedievalUI.Instance.Say(needAxeLines);
            return;
        }

        health -= axeDamage;
        pauseLeft = hitPause;
        if (health <= 0)
            Die();
    }

    void FixedUpdate()
    {
        if (dead || player == null)
            return;

        if (pauseLeft > 0f)
        {
            pauseLeft -= WorldTime.FixedDelta;
            return;
        }

        Vector3 toPlayer = player.position - body.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > aggroRadius * aggroRadius || toPlayer.sqrMagnitude < 0.01f)
            return;

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
        if (dead || sprite == null || walkFrames == null || walkFrames.Length == 0)
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

    void OnTriggerEnter(Collider other) => TouchPlayer(other);
    void OnTriggerStay(Collider other) => TouchPlayer(other);

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
        if (CavemanLevel.Instance != null)
            CavemanLevel.Instance.OnBoarKilled();
        Destroy(gameObject, 1f);
    }

    static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}