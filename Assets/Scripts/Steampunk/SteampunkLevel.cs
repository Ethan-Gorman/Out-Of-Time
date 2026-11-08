using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs the Steampunk Era story: the fall into the factory, the chain and oil, the three tower gears,
// the lift and the time machine. All text is editable in the Inspector.
// Sits on the LevelDirector object in the Steampunk scene.
public class SteampunkLevel : MonoBehaviour, ITimeMachineStory
{
    public enum Item { Chain, Oil }

    public static SteampunkLevel Instance { get; private set; }

    [Header("Wiring (set by the builder)")]
    public GearRepair gear1;
    public GearRepair gear2;
    public GearLink gear3;
    public MechanicalLift lift;
    public TimeMachine timeMachine;

    [Header("Arrival")]
    public float introDelay = 1.4f;
    [TextArea] public string[] introLines =
    {
        "SYSTEM: \"Arrival successful. Landing procedure: unsuccessful.\"",
        "PLAYER: \"Why am I always falling?\"",
        "SYSTEM: \"Mechanical lift inactive. Three gears require maintenance.\"",
    };
    public string gearsObjective = "Repair the gear tower to power the lift";

    [Header("Items")]
    [TextArea] public string[] chainLines = { "PLAYER: \"A spare iron chain. Gear one is missing exactly this.\"" };
    [TextArea] public string[] oilLines = { "PLAYER: \"An oil can. That seized gear won't know what hit it.\"" };

    [Header("Gears")]
    [TextArea] public string[] gear1Lines = { "PLAYER: \"Chain's on. Gear one is turning!\"" };
    [TextArea] public string[] gear2WaitingLines = { "PLAYER: \"Oiled and free... but it needs gear one to drive it.\"" };
    [TextArea] public string[] gear2Lines = { "PLAYER: \"Gear two is moving. And the rod is pulling on gear three...\"" };
    [TextArea] public string[] liftLines =
    {
        "SYSTEM: \"Gear assembly operational. Mechanical lift online.\"",
        "PLAYER: \"Finally. Going up.\"",
    };
    public string liftObjective = "Ride the lift to the first floor";

    [Header("First floor")]
    [TextArea] public string[] firstFloorLines = { "PLAYER: \"The time machine is at the far end. Past all of... that.\"" };
    public string machineObjective = "Reach the time machine";

    [Header("Ending")]
    [TextArea] public string[] repairedLines =
    {
        "SYSTEM: \"Time machine condition critical. Chrono Regulator malfunction detected.\"",
        "PLAYER: \"You have got to be kidding me.\"",
        "SYSTEM: \"Repairs complete. Next destination: Future Era.\"",
    };
    public string boardObjective = "Board the time machine";
    [TextArea] public string nextEraBanner = "FUTURE ERA\n<size=36>Coming soon</size>";
    // The scene loaded after this one must have this in its name, otherwise boarding shows the banner instead
    public string nextSceneName = "Future";

    public bool HasChain { get; private set; }
    public bool HasOil { get; private set; }
    public bool ReachedFirstFloor { get; private set; }

    void Awake()
    {
        Instance = this;
        // Only travel when the Future scene really is next in Build Settings (not SampleScene or nothing)
        if (timeMachine != null)
            timeMachine.loadNextEra = NextSceneIsFuture();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(introDelay);
        Say(introLines);
        RefreshObjective();
    }

    public void Collect(Item item)
    {
        if (item == Item.Chain && !HasChain) { HasChain = true; Say(chainLines); }
        if (item == Item.Oil && !HasOil) { HasOil = true; Say(oilLines); }
        RefreshObjective();
    }

    public bool Has(Item item) => item == Item.Chain ? HasChain : HasOil;

    // A gear was fixed by the player (chain attached / oil applied)
    public void GearRepaired(GearRepair gear)
    {
        if (gear == gear1)
        {
            Say(gear1Lines);
            // Gear 2 may already be oiled and waiting for drive
            if (gear2 != null && gear2.IsRepaired)
                gear2.TryStart();
        }
        else if (gear == gear2)
        {
            if (gear2.IsRunning) Say(gear2Lines);
            else Say(gear2WaitingLines);
        }
        RefreshObjective();
    }

    // Gear 2 started turning (straight away, or once gear 1 began driving it)
    public void Gear2Started()
    {
        if (gear3 != null)
            gear3.Release();
        RefreshObjective();
    }

    // Gear 3 is free and turning: the tower is whole, so the lift gets power
    public void Gear3Started()
    {
        if (lift != null)
            lift.PowerOn();
        Say(liftLines);
        RefreshObjective();
    }

    public void LiftArrivedTop()
    {
        if (ReachedFirstFloor)
            return;
        ReachedFirstFloor = true;
        Say(firstFloorLines);
        RefreshObjective();
    }

    void RefreshObjective()
    {
        var ui = MedievalUI.Instance;
        if (ui == null)
            return;
        if (timeMachine != null && timeMachine.Current != TimeMachine.State.Broken)
            return;
        if (ReachedFirstFloor) { ui.SetObjective($"{machineObjective}\nIt's at the far end of the first floor"); return; }
        if (lift != null && lift.Powered) { ui.SetObjective($"{liftObjective}\nStand on the lift and press E"); return; }

        int turning = (gear1 != null && gear1.IsRunning ? 1 : 0) + (gear2 != null && gear2.IsRunning ? 1 : 0) + (gear3 != null && gear3.IsRunning ? 1 : 0);
        string next =
            gear1 != null && !gear1.IsRepaired ? (HasChain ? "Fit the chain to Gear 1" : "Find a spare chain in the storage area") :
            gear2 != null && !gear2.IsRepaired ? (HasOil ? "Oil the seized Gear 2" : "Find oil near the mechanic's workbench") :
            "Wait for Gear 3's brake to let go";
        ui.SetObjective($"{gearsObjective} ({turning}/3)\n{next}");
    }

    static void Say(string[] lines)
    {
        if (MedievalUI.Instance != null)
            MedievalUI.Instance.Say(lines);
    }

    bool NextSceneIsFuture()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next <= 0 || next >= SceneManager.sceneCountInBuildSettings)
            return false;
        string path = SceneUtility.GetScenePathByBuildIndex(next);
        return !string.IsNullOrEmpty(nextSceneName) && path.Contains(nextSceneName);
    }

    // ITimeMachineStory
    public bool CanRepair => ReachedFirstFloor;
    public string[] NotReadyLines => null;

    public void TimeMachineRepaired()
    {
        Say(repairedLines);
        if (MedievalUI.Instance != null)
            MedievalUI.Instance.SetObjective(boardObjective);
    }

    public void TimeMachineBoarded(bool travelling)
    {
        var ui = MedievalUI.Instance;
        if (ui == null)
            return;
        ui.SetObjective("");
        if (!travelling)
            ui.ShowBanner(nextEraBanner);
    }
}
