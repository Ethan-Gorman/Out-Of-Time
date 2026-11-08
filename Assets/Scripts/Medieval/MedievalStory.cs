// The Medieval Era's revamped story text in one place, so the scene builder and the scripts' defaults agree.
// Scene values still win: the builder copies these onto the StoryTriggers, levers, chest and LevelDirector.
public static class MedievalStory
{
    public static readonly string[] EnterCourtyardLines =
    {
        "Skeletons! Their touch hurts... but I bet I could jump on their heads.",
        "Every one I stomp buys back five seconds.",
    };
    public const string EnterCourtyardObjective = "Get through the castle gate";

    public static readonly string[] GateHintLines = { "The gate is shut tight. There must be a lever somewhere." };

    public static readonly string[] GatePulledLines = { "Something heavy just moved... the gate is opening!" };

    public static readonly string[] EnterGreatHallLines = { "The crystal's glow is coming from the far end of the hall." };
    public const string EnterGreatHallObjective = "Grab the Time Crystal at the end of the hall";

    public static readonly string[] CrystalLines =
    {
        "A Time Crystal! This should power the time machine.",
        "Back to the time machine... unless that vault is worth the risk.",
    };

    public static readonly string[] VaultHintLines =
    {
        "A treasure vault! That chest must be worth a lot of time.",
        "Traps everywhere... if I stop moving, they slow down too.",
    };

    public static readonly string[] VaultDoorHintLines = { "Barred from this side. There must be another way in." };

    public const string VaultLeverName = "the vault door";
    public static readonly string[] VaultPulledLines = { "The vault door is opening. A shortcut back to the gate!" };

    public const float ChestTimeBonus = 40f;
    public static readonly string[] ChestOpenedLines = { "A royal hoard! That's a lot of time back on the clock." };

    public const float SkeletonKillReward = 5f;

    public static readonly string[] RepairedLines =
    {
        "It's working! The time machine is repaired.",
        "Next stop: the Steampunk Era.",
    };
}
