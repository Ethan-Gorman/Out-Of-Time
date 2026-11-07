using System.Collections;
using UnityEngine;

// Getting hit, shared by every era: costs clock time, knocks the player back,
// and gives a moment of invulnerability (the sprite blinks).
// Sits on the base Player prefab, so every era's player variant gets it.
// Hazards and enemies call Hit(): no health of its own, the Clock is the player's health.
[RequireComponent(typeof(Rigidbody))]
public class PlayerDamage : MonoBehaviour
{
    public float secondsPerHit = 10f;

    public float invulnerableTime = 1f;

    [Header("Knockback")]
    public float knockbackSpeed = 7f;
    public float knockbackUpSpeed = 3f;
    public float knockbackTime = 0.2f;

    public SpriteRenderer sprite;

    public Behaviour[] disableOnOutOfTime;

    public bool IsInvulnerable => Time.time < invulnerableUntil;
    public bool IsOut => Clock.Instance != null && Clock.Instance.IsOut;

    Rigidbody body;
    PlayerMovement movement;
    float invulnerableUntil;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();

        if (sprite == null)
        {
            var child = transform.Find("Sprite");
            if (child != null)
                sprite = child.GetComponent<SpriteRenderer>();
        }
    }

    void Start()
    {
        if (Clock.Instance != null)
            Clock.Instance.onOutOfTime.AddListener(OnOutOfTime);
    }

    void OnDestroy()
    {
        if (Clock.Instance != null)
            Clock.Instance.onOutOfTime.RemoveListener(OnOutOfTime);
    }

    // Returns true if the hit landed (false while invulnerable or out of time).
    // seconds < 0 uses secondsPerHit
    public bool Hit(Vector3 sourcePosition, float seconds = -1f)
    {
        if (IsOut || IsInvulnerable)
            return false;

        invulnerableUntil = Time.time + invulnerableTime;
        if (Clock.Instance != null)
            Clock.Instance.RemoveTime(seconds < 0f ? secondsPerHit : seconds);

        if (IsOut)
            return true;

        // Push away from whatever hit us, along the ground, plus a small hop
        Vector3 away = transform.position - sourcePosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f)
            away = Vector3.back;
        StartCoroutine(Knockback(away.normalized));
        return true;
    }

    IEnumerator Knockback(Vector3 direction)
    {
        // PlayerMovement sets the velocity every physics step, so pause it while we're pushed
        if (movement != null)
            movement.enabled = false;

        body.linearVelocity = direction * knockbackSpeed + Vector3.up * knockbackUpSpeed;
        yield return new WaitForSeconds(knockbackTime);

        if (movement != null && !IsOut)
            movement.enabled = true;
    }

    void OnOutOfTime()
    {
        foreach (var behaviour in disableOnOutOfTime)
            if (behaviour != null)
                behaviour.enabled = false;
    }

    void Update()
    {
        // Blink while invulnerable so the player can see they're safe for a moment
        if (sprite != null)
            sprite.enabled = IsOut || !IsInvulnerable || Mathf.Repeat(Time.time * 12f, 1f) > 0.5f;
    }
}
