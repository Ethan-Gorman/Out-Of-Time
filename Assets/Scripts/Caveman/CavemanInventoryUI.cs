using UnityEngine;
using UnityEngine.UI;

// The Caveman resource line (wood, stone, axe, magnets) on a panel under the clock.
// The panel is built at runtime and wears the era's UITheme
public class CavemanInventoryUI : MonoBehaviour
{
    public Text inventoryText;
    // Kept small so the popped text stays inside its panel
    public float popScale = 1.08f;
    public float popTime = 0.25f;

    float popLeft;
    string lastText;
    Image panel;

    void Awake()
    {
        if (inventoryText == null)
            return;

        // Just before the text in the hierarchy, so it draws behind it
        var go = new GameObject("Inventory Panel", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(inventoryText.transform.parent, false);
        rect.SetSiblingIndex(inventoryText.transform.GetSiblingIndex());
        UILayout.TopLeft(rect, new Vector2(UILayout.Margin, 160f), new Vector2(UILayout.PanelWidth, 64f));
        panel = go.GetComponent<Image>();
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = UISprites.PixelMultiplier;
        panel.raycastTarget = false;
    }

    void OnEnable()
    {
        CavemanLevel.InventoryChanged += Refresh;
        UITheme.Changed += ApplyTheme;
        ApplyTheme(UITheme.Current);
        Refresh();
    }

    void OnDisable()
    {
        CavemanLevel.InventoryChanged -= Refresh;
        UITheme.Changed -= ApplyTheme;
        if (inventoryText != null)
            inventoryText.rectTransform.localScale = Vector3.one;
    }

    void ApplyTheme(UITheme t)
    {
        if (panel != null)
            panel.sprite = UISprites.Panel(t);
        UIThemer.StyleText(inventoryText, t.text);
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
        bool show = clock == null || !clock.IsFrozen;
        inventoryText.enabled = show;
        if (panel != null)
            panel.enabled = show;

        if (popLeft <= 0f)
            return;
        popLeft = Mathf.Max(0f, popLeft - Time.deltaTime);
        float t = popLeft / popTime;
        inventoryText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, popScale, t * t);
    }
}
