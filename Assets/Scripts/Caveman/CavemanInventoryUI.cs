using UnityEngine;
using UnityEngine.UI;

public class CavemanInventoryUI : MonoBehaviour
{
    public Text inventoryText;
    public float popScale = 1.25f;
    public float popTime = 0.25f;

    float popLeft;
    string lastText;

    void OnEnable()
    {
        CavemanLevel.InventoryChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        CavemanLevel.InventoryChanged -= Refresh;
        if (inventoryText != null)
            inventoryText.rectTransform.localScale = Vector3.one;
    }

    void Refresh()
    {
        var level = CavemanLevel.Instance;
        if (inventoryText == null || level == null)
            return;

        string axe = level.HasAxe ? "READY" : "--";
        string text = $"WOOD {level.Wood}   STONE {level.Stone}   AXE {axe}   MAGNETS {level.MagneticStones}/{level.magneticStonesNeeded}";

        // Pop when something actually changed (not on the first fill)
        if (lastText != null && text != lastText)
            popLeft = popTime;
        lastText = text;
        inventoryText.text = text;
    }

    void Update()
    {
        if (inventoryText == null)
            return;

        // Hidden once the level is finished, so the clock's results have the corner to themselves
        var clock = Clock.Instance;
        inventoryText.enabled = clock == null || !clock.IsFrozen;

        if (popLeft <= 0f)
            return;
        popLeft = Mathf.Max(0f, popLeft - Time.deltaTime);
        float t = popLeft / popTime;
        inventoryText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, popScale, t * t);
    }
}
