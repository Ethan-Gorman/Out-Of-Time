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

    [Header("Text")]
    public string inspectPrompt = "Inspect the time machine";
    public string repairPrompt = "Fit the Time Crystal";
    [TextArea] public string[] brokenLines = { "It's completely dead. I need a Time Crystal to power it." };

    public UnityEvent onRepaired;
    public UnityEvent onBoarded;

    public State Current { get; private set; } = State.Broken;

    // The level's story (MedievalLevel, CavemanLevel...): decides when the machine can be repaired
    ITimeMachineStory story;

    bool HasParts => story != null && story.CanRepair;

    void Awake()
    {
        Refresh();
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (behaviour is ITimeMachineStory s)
            {
                story = s;
                break;
            }
    }

    public override string Prompt => Current switch
    {
        State.Broken => HasParts ? repairPrompt : inspectPrompt,
        State.Repaired => "Board the time machine",
        _ => "",
    };

    public override bool CanInteract => Current != State.Boarded;

    public override void Interact(MedievalInteractor interactor)
    {
        switch (Current)
        {
            case State.Broken when HasParts:
                Current = State.Repaired;
                Refresh();
                story?.TimeMachineRepaired();
                onRepaired.Invoke();
                break;

            case State.Broken:
                if (MedievalUI.Instance != null)
                    MedievalUI.Instance.Say(story?.NotReadyLines ?? brokenLines);
                break;

            case State.Repaired:
                Current = State.Boarded;

                // Objective complete: freezes the clock and records the level's time.
                // A machine that doesn't travel on is the end of the game so far, so it ends the run
                if (LevelManager.Instance != null)
                    LevelManager.Instance.FinishLevel(lastLevel: !loadNextEra);
                onBoarded.Invoke();
                bool travelling = loadNextEra && LevelManager.Instance != null;
                story?.TimeMachineBoarded(travelling);
                if (travelling)
                    LevelManager.Instance.CompleteLevel();
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
