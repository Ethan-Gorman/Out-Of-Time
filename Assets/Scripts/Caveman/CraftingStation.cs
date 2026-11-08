using UnityEngine;

public class CraftingStation : Interactable
{
    // Said after the exact "I need 1 more wood" line, to point at where the materials come from
    [TextArea] public string[] needMaterialsLines = { "Firewood stacks give wood, rocks give stone." };

    public override string Prompt
    {
        get
        {
            var level = CavemanLevel.Instance;
            if (level == null)
                return "Craft Stone Axe";
            if (level.HasAxe)
                return "Stone Axe crafted";
            if (level.CanCraftAxe)
                return "Craft Stone Axe";
            // Show the recipe progress so the player knows what's still missing
            return $"Craft axe <size=24>(wood {Mathf.Min(level.Wood, level.axeWoodCost)}/{level.axeWoodCost}, " +
                   $"stone {Mathf.Min(level.Stone, level.axeStoneCost)}/{level.axeStoneCost})</size>";
        }
    }

    public override bool CanInteract => CavemanLevel.Instance == null || !CavemanLevel.Instance.HasAxe;

    public override void Interact(MedievalInteractor interactor)
    {
        var level = CavemanLevel.Instance;
        if (level == null)
            return;

        if (level.CraftAxe())
        {
            Vector3 top = transform.position + Vector3.up * 0.5f;
            CavemanFx.Play(CavemanSfx.Sound.Craft);
            CavemanFx.Shake(0.3f);
            CavemanFx.Debris(top, CavemanFx.MaterialsOf(this), 8, 0.1f, 2.5f);
            CavemanFx.Text(top + Vector3.up * 0.8f, "STONE AXE!", new Color(1f, 0.85f, 0.4f));
            return;
        }

        CavemanFx.Play(CavemanSfx.Sound.Deny);
        if (MedievalUI.Instance == null)
            return;
        var lines = new string[1 + (needMaterialsLines != null ? needMaterialsLines.Length : 0)];
        lines[0] = MissingLine(level);
        needMaterialsLines?.CopyTo(lines, 1);
        MedievalUI.Instance.Say(lines);
    }

    // "I need 1 more wood and 2 more stone for an axe."
    static string MissingLine(CavemanLevel level)
    {
        int wood = Mathf.Max(0, level.axeWoodCost - level.Wood);
        int stone = Mathf.Max(0, level.axeStoneCost - level.Stone);
        string missing = wood > 0 && stone > 0 ? $"{wood} more wood and {stone} more stone"
            : wood > 0 ? $"{wood} more wood" : $"{stone} more stone";
        return $"I need {missing} for an axe.";
    }
}
