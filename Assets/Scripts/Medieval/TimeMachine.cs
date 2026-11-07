using UnityEngine;
using UnityEngine.Events;

// The player's time machine. It arrives broken; with the Time Crystal the player repairs it,
// then boards it to travel to the next era.
// The next era doesn't exist yet, so by default boarding just shows a "coming soon" banner
// and the player stays. Tick Load Next Era once the next scene is in Build Settings.
public class TimeMachine : Interactable
{
    public enum State { Broken, Repaired, Boarded }

    [Header("Art (swap freely)")]
    public GameObject brokenVisual;
    public GameObject repairedVisual;
    public Light glow;

    [Header("Travel")]
    public bool loadNextEra = false;

    [TextArea] public string[] brokenLines = { "It's completely dead. I need a Time Crystal to power it." };

    public UnityEvent onRepaired;
    public UnityEvent onBoarded;

    public State Current { get; private set; } = State.Broken;

    bool HasCrystal => MedievalLevel.Instance != null && MedievalLevel.Instance.HasCrystal;

    void Awake()
    {
        Refresh();
    }

    public override string Prompt => Current switch
    {
        State.Broken => HasCrystal ? "Repair the time machine" : "Inspect the time machine",
        State.Repaired => "Board the time machine",
        _ => "",
    };

    public override bool CanInteract => Current != State.Boarded;

    public override void Interact(MedievalInteractor interactor)
    {
        switch (Current)
        {
            case State.Broken when HasCrystal:
                Current = State.Repaired;
                Refresh();
                if (MedievalLevel.Instance != null)
                    MedievalLevel.Instance.TimeMachineRepaired();
                onRepaired.Invoke();
                break;

            case State.Broken:
                if (MedievalUI.Instance != null)
                    MedievalUI.Instance.Say(brokenLines);
                break;

            case State.Repaired:
                Current = State.Boarded;

                // Objective complete: freezes the clock and records the level's time
                if (LevelManager.Instance != null)
                    LevelManager.Instance.FinishLevel();
                onBoarded.Invoke();
                if (loadNextEra && LevelManager.Instance != null)
                    LevelManager.Instance.CompleteLevel();
                else if (MedievalLevel.Instance != null)
                    MedievalLevel.Instance.TimeMachineBoarded();
                break;
        }
    }

    void Refresh()
    {
        bool repaired = Current != State.Broken;
        if (brokenVisual != null) brokenVisual.SetActive(!repaired);
        if (repairedVisual != null) repairedVisual.SetActive(repaired);
        if (glow != null) glow.enabled = repaired;
    }
}
