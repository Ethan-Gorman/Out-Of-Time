using UnityEngine;

public class CraftingStation : Interactable
{
    [TextArea] public string[] needMaterialsLines = { "I need 2 wood and 2 stone to make an axe." };

    public override string Prompt => CavemanLevel.Instance != null && CavemanLevel.Instance.HasAxe
        ? "Stone Axe crafted"
        : "Craft Stone Axe";

    public override bool CanInteract => CavemanLevel.Instance == null || !CavemanLevel.Instance.HasAxe;

    public override void Interact(MedievalInteractor interactor)
    {
        var level = CavemanLevel.Instance;
        if (level == null)
            return;

        if (!level.CraftAxe() && MedievalUI.Instance != null)
            MedievalUI.Instance.Say(needMaterialsLines);
    }
}