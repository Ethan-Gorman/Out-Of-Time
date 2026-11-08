using System;
using System.Collections;
using UnityEngine;

// Runs the Caveman Era story and the player's inventory: wood and stone from trees and rocks,
// the Stone Axe, and the three magnetic stones that repair the time machine's temporal compass.
// Reuses MedievalUI for dialogue and objectives. Sits on the LevelDirector object in the Caveman scene.
public class CavemanLevel : MonoBehaviour, ITimeMachineStory
{
    public enum Resource { Wood, Stone }

    public static CavemanLevel Instance { get; private set; }

    // Fired whenever wood, stone, the axe or the magnetic stones change (for a HUD)
    public static event Action InventoryChanged;

    public int axeWoodCost = 2;
    public int axeStoneCost = 2;
    public int magneticStonesNeeded = 3;
    public float boarReward = 30f;
    public float introDelay = 0.6f;

    [TextArea] public string[] introLines =
    {
        "The time machine's temporal compass is cracked. It can't find its way through time.",
        "Magnetic stones could fix it... but first I need tools. An axe: 2 wood, 2 stone.",
    };
    // The live wood/stone counts are added on a second line
    public string gatherObjective = "Hit firewood stacks and rocks for an axe";

    [TextArea] public string[] canCraftLines = { "That's enough for a Stone Axe. To the flat stone!" };
    public string craftObjective = "Craft a Stone Axe at the flat crafting stone";

    [TextArea] public string[] axeLines = { "A Stone Axe! Now I can deal with that boar.", "And cut through the brambles to the cave." };
    public string boarObjective = "Hunt the wild boar with your axe";

    [TextArea] public string[] boarLines = { "The boar is down. That bought me some time!" };
    // The live stone count is added after it
    public string caveObjective = "Find the magnetic stones in the dark cave";
    public string brambleHint = "Chop through the brambles at the cave mouth";

    [TextArea] public string[] stoneLines = { "A glowing magnetic stone!" };
    [TextArea] public string[] allStonesLines = { "That's all three. Back to the time machine!" };
    public string returnObjective = "Bring the stones back to the time machine";

    [TextArea] public string[] repairedLines = { "The compass spins true! The time machine is repaired.", "Next stop: the Medieval Era." };
    public string boardObjective = "Board the time machine";
    [TextArea] public string nextEraBanner = "MEDIEVAL ERA";

    public int Wood { get; private set; }
    public int Stone { get; private set; }
    public bool HasAxe { get; private set; }
    public int MagneticStones { get; private set; }
    public bool BoarKilled { get; private set; }

    public bool CanCraftAxe => !HasAxe && Wood >= axeWoodCost && Stone >= axeStoneCost;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        InventoryChanged = null;
    }

    void Awake()
    {
        Instance = this;
        CavemanFx.Ensure();
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
        Objective(GatherText);
        InventoryChanged?.Invoke();
    }

    public void AddResource(Resource type, int amount)
    {
        bool couldCraft = CanCraftAxe;
        if (type == Resource.Wood) Wood += amount;
        else Stone += amount;
        InventoryChanged?.Invoke();

        if (!couldCraft && CanCraftAxe)
        {
            Say(canCraftLines);
            Objective(craftObjective);
        }
        else if (!HasAxe && !CanCraftAxe)
            Objective(GatherText);
    }

    // "Hit firewood stacks and rocks for an axe" with the counts underneath
    string GatherText => $"{gatherObjective}\nWood {Mathf.Min(Wood, axeWoodCost)}/{axeWoodCost}   Stone {Mathf.Min(Stone, axeStoneCost)}/{axeStoneCost}";

    // "Find the magnetic stones in the dark cave (1/3)", plus the bramble hint until the first stone
    string CaveText => MagneticStones == 0 && !string.IsNullOrEmpty(brambleHint)
        ? $"{caveObjective} (0/{magneticStonesNeeded})\n{brambleHint}"
        : $"{caveObjective} ({MagneticStones}/{magneticStonesNeeded})";

    // Spends the materials. Returns false if there aren't enough
    public bool CraftAxe()
    {
        if (!CanCraftAxe)
            return false;
        Wood -= axeWoodCost;
        Stone -= axeStoneCost;
        HasAxe = true;
        InventoryChanged?.Invoke();
        Say(axeLines);
        Objective(BoarKilled ? CaveText : boarObjective);
        return true;
    }

    // Called by the boar when it dies: rewards clock time
    public void OnBoarKilled()
    {
        if (BoarKilled)
            return;
        BoarKilled = true;
        if (Clock.Instance != null)
            Clock.Instance.AddTime(boarReward);
        Say(boarLines);
        if (MagneticStones < magneticStonesNeeded)
            Objective(CaveText);
    }

    public void CollectMagneticStone()
    {
        if (MagneticStones >= magneticStonesNeeded)
            return;

        MagneticStones++;
        InventoryChanged?.Invoke();
        if (MagneticStones >= magneticStonesNeeded)
        {
            Say(allStonesLines);
            Objective(returnObjective);
        }
        else
        {
            Say(stoneLines);
            Objective(CaveText);
        }
    }

    // ITimeMachineStory
    public bool CanRepair => MagneticStones >= magneticStonesNeeded;

    public string[] NotReadyLines
    {
        get
        {
            int left = magneticStonesNeeded - MagneticStones;
            return new[] { $"The compass is still cracked. I need {left} more magnetic {(left == 1 ? "stone" : "stones")} from the cave." };
        }
    }

    public void TimeMachineRepaired()
    {
        Say(repairedLines);
        Objective(boardObjective);
    }

    public void TimeMachineBoarded(bool travelling)
    {
        Objective("");
        if (MedievalUI.Instance != null)
            MedievalUI.Instance.ShowBanner(nextEraBanner);
    }

    static void Say(string[] lines)
    {
        if (MedievalUI.Instance != null)
            MedievalUI.Instance.Say(lines);
    }

    static void Objective(string text)
    {
        if (MedievalUI.Instance != null)
            MedievalUI.Instance.SetObjective(text);
    }
}
