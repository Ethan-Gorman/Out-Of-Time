using UnityEngine;

[RequireComponent(typeof(SkeletonEnemy))]
public class CastleGuardCombat : Interactable
{
    [SerializeField] MedievalInteractor playerInteractor;
    [SerializeField] float attackRange = 2f;
    [SerializeField] int health = 3;
    [SerializeField] float hitRecovery = 0.25f;
    SkeletonEnemy enemy;
    float nextHit;
    float flash;
    Color originalColor;

    public override string Prompt => "Strike crystal guardian (" + health + " hits left)";
    public override bool CanInteract => enemy != null && !enemy.IsDead && Time.unscaledTime >= nextHit;

    void Awake()
    {
        enemy = GetComponent<SkeletonEnemy>();
        if (enemy.sprite != null) originalColor = enemy.sprite.color;
    }
    void Update()
    {
        if (playerInteractor != null)
        {
            Vector3 distance = playerInteractor.transform.position - transform.position;
            distance.y = 0f;
            if (enemy != null && !enemy.IsDead && distance.sqrMagnitude <= attackRange * attackRange)
                playerInteractor.Enter(this);
            else playerInteractor.Exit(this);
        }
        if (enemy != null && enemy.sprite != null)
        {
            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime);
            enemy.sprite.color = flash > 0f ? new Color(1f, 0.35f, 0.25f, originalColor.a) : originalColor;
        }
    }
    void OnDisable() { if (playerInteractor != null) playerInteractor.Exit(this); }
    public override void Interact(MedievalInteractor interactor)
    {
        if (!CanInteract) return;
        Vector3 distance = interactor.transform.position - transform.position;
        distance.y = 0f;
        if (distance.sqrMagnitude > attackRange * attackRange) return;
        nextHit = Time.unscaledTime + hitRecovery;
        flash = hitRecovery;
        health--;
        if (health <= 0) enemy.Die();
    }
}
