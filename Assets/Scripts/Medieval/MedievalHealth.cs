using System.Collections;
using UnityEngine;

// The player's health in the Medieval Era. Skeletons and traps call Damage().
// A hit knocks the player back and makes them flash and briefly invulnerable.
// At zero the player dies and the level restarts.
// Sits on MedievalPlayer, next to PlayerMovement.
[RequireComponent(typeof(Rigidbody))]
public class MedievalHealth : MonoBehaviour
{
    public float maxHealth = 100f;

    [Tooltip("Seconds of invulnerability after a hit, so one touch doesn't drain everything.")]
    public float invulnerableTime = 1f;

    [Header("Knockback")]
    public float knockbackSpeed = 7f;
    public float knockbackUpSpeed = 3f;
    [Tooltip("Seconds the player can't steer after a hit.")]
    public float knockbackTime = 0.2f;

    [Tooltip("Real seconds between dying and the level restarting.")]
    public float restartDelay = 1.5f;

    [Tooltip("Flashes while invulnerable. Found on the 'Sprite' child if left empty.")]
    public SpriteRenderer sprite;

    public float Current { get; private set; }
    public float Fraction => maxHealth > 0f ? Current / maxHealth : 0f;
    public bool IsDead => Current <= 0f;
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    Rigidbody body;
    PlayerMovement movement;
    float invulnerableUntil;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
        Current = maxHealth;

        if (sprite == null)
        {
            var child = transform.Find("Sprite");
            if (child != null)
                sprite = child.GetComponent<SpriteRenderer>();
        }
    }

    // Returns true if the hit landed (false while invulnerable or already dead)
    public bool Damage(float amount, Vector3 sourcePosition)
    {
        if (IsDead || IsInvulnerable || amount <= 0f)
            return false;

        Current = Mathf.Max(0f, Current - amount);
        invulnerableUntil = Time.time + invulnerableTime;

        if (IsDead)
        {
            Die();
            return true;
        }

        // Push away from whatever hit us, along the ground, plus a small hop
        Vector3 away = transform.position - sourcePosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f)
            away = Vector3.back;
        StartCoroutine(Knockback(away.normalized));
        return true;
    }

    public void Heal(float amount)
    {
        if (!IsDead)
            Current = Mathf.Min(maxHealth, Current + amount);
    }

    public void HealFull() => Heal(maxHealth);

    IEnumerator Knockback(Vector3 direction)
    {
        // PlayerMovement sets the velocity every physics step, so pause it while we're pushed
        if (movement != null)
            movement.enabled = false;

        body.linearVelocity = direction * knockbackSpeed + Vector3.up * knockbackUpSpeed;
        yield return new WaitForSeconds(knockbackTime);

        if (movement != null && !IsDead)
            movement.enabled = true;
    }

    void Die()
    {
        if (movement != null)
        {
            movement.PlayDeath();
            movement.enabled = false;
        }
        body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);

        var jump = GetComponent<MedievalJump>();
        if (jump != null) jump.enabled = false;
        var interactor = GetComponent<MedievalInteractor>();
        if (interactor != null) interactor.enabled = false;

        if (MedievalUI.Instance != null)
            MedievalUI.Instance.ShowBanner("YOU DIED");

        StartCoroutine(RestartAfterDelay());
    }

    IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSecondsRealtime(restartDelay);
        MedievalLevel.RestartScene();
    }

    void Update()
    {
        // Blink while invulnerable so the player can see they're safe for a moment
        if (sprite != null)
            sprite.enabled = IsDead || !IsInvulnerable || Mathf.Repeat(Time.time * 12f, 1f) > 0.5f;
    }
}
