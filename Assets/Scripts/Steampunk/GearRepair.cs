using UnityEngine;

// One of the tower gears the player fixes with an item: Gear 1 takes the spare chain, Gear 2 takes oil.
// Once repaired it starts its GearSpinner, unless it needs another gear to drive it first (Driven By).
// Needs a trigger collider on the same object for the "in range" zone.
public class GearRepair : Interactable
{
    public SteampunkLevel.Item needs;
    public GearSpinner spinner;

    // Gear 2 turns only once Gear 1 is driving it
    public GearRepair drivenBy;

    [Header("Shown once repaired (the fitted chain, a drip of oil...)")]
    public GameObject repairedVisual;
    public GameObject brokenVisual;
    public SteamEmitter repairPuff;

    [Header("Text")]
    public string repairPrompt = "Fit the chain";
    public string inspectPrompt = "Inspect the gear";
    [TextArea] public string[] missingItemLines = { "PLAYER: \"The chain's snapped. I need a spare one.\"" };

    public bool IsRepaired { get; private set; }
    public bool IsRunning { get; private set; }

    SteampunkLevel Level => SteampunkLevel.Instance;

    public override string Prompt => Level != null && Level.Has(needs) ? repairPrompt : inspectPrompt;
    public override bool CanInteract => !IsRepaired;

    void Awake()
    {
        if (spinner != null)
            spinner.SetRunning(false);
        Refresh();
    }

    public override void Interact(MedievalInteractor interactor)
    {
        if (IsRepaired)
            return;
        if (Level == null || !Level.Has(needs))
        {
            if (MedievalUI.Instance != null)
                MedievalUI.Instance.Say(missingItemLines);
            return;
        }

        IsRepaired = true;
        Refresh();
        if (repairPuff != null)
            repairPuff.Burst();
        TryStart();
        Level.GearRepaired(this);
        if (interactor != null)
            interactor.Exit(this);
    }

    // Starts turning if repaired and (when driven) the driving gear is running. Safe to call again
    public void TryStart()
    {
        if (IsRunning || !IsRepaired || (drivenBy != null && !drivenBy.IsRunning))
            return;
        IsRunning = true;
        if (spinner != null)
            spinner.SetRunning(true);
        if (Level != null && Level.gear2 == this)
            Level.Gear2Started();
    }

    void Refresh()
    {
        if (repairedVisual != null) repairedVisual.SetActive(IsRepaired);
        if (brokenVisual != null) brokenVisual.SetActive(!IsRepaired);
    }
}
