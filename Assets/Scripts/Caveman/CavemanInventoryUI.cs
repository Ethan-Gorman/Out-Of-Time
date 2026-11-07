using UnityEngine;
using UnityEngine.UI;

public class CavemanInventoryUI : MonoBehaviour
{
    public Text inventoryText;

    void OnEnable()
    {
        CavemanLevel.InventoryChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        CavemanLevel.InventoryChanged -= Refresh;
    }

    void Refresh()
    {
        var level = CavemanLevel.Instance;
        if (inventoryText == null || level == null)
            return;

        string axe = level.HasAxe ? "READY" : "--";
        inventoryText.text = $"WOOD {level.Wood}   STONE {level.Stone}   AXE {axe}   MAGNETS {level.MagneticStones}/{level.magneticStonesNeeded}";
    }
}