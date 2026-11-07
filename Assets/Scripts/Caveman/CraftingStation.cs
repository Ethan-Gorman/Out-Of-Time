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
        if (MedievalUI.Instance != null)
            MedievalUI.Instance.Say(needMaterialsLines);
    }
}