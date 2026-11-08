using UnityEngine;
using UnityEngine.Events;

// An optional chest. Opening it adds time to the clock. Works once.
// Art is free to swap: a hinged Lid is swung open, and/or a Closed Visual is swapped for an Opened Visual
// (for chest meshes whose lid isn't a separate piece). Leave any of them empty.
public class TreasureChest : Interactable
{
    [Header("Art (swap freely)")]
    public Transform lid;
    public float openAngle = -110f;
    // Local axis the lid swings around (its hinge)
    public Vector3 lidAxis = Vector3.right;
    public float openTime = 0.35f;
    public GameObject closedVisual;
    public GameObject openedVisual;

    [Header("Reward")]
    public float timeBonus = 40f;
    // Where the "+40s" popup appears, above the chest
    public float rewardPopupHeight = 1.2f;

    [TextArea] public string[] openedLines = { "A royal hoard! That's a lot of time back on the clock." };

    public UnityEvent onOpened;

    public bool IsOpen { get; private set; }

    float progress;
    Quaternion lidBase;

    void Awake()
    {
        if (string.IsNullOrEmpty(prompt) || prompt == "Interact")
            prompt = "Open chest";
        if (lid != null)
            lidBase = lid.localRotation;
        ShowVisuals();
    }

    // Says what's inside, so skipping the chest is a real choice
    public override string Prompt => timeBonus > 0f ? $"{prompt} <size=24>(+{timeBonus:0}s on the clock)</size>" : prompt;

    public override bool CanInteract => !IsOpen;

    public override void Interact(MedievalInteractor interactor)
    {
        if (IsOpen)
            return;
        IsOpen = true;
        ShowVisuals();

        // Clock.AddTime shows the HUD popup and plays the gain sound. Nothing is added once the clock has stopped
        var clock = Clock.Instance;
        if (timeBonus > 0f && clock != null && !clock.IsOut && !clock.IsFrozen)
        {
            clock.AddTime(timeBonus);
            FloatText.TimeGain(transform.position + Vector3.up * rewardPopupHeight, timeBonus);
        }

        if (MedievalUI.Instance != null)
            MedievalUI.Instance.Say(openedLines);
        onOpened.Invoke();
    }

    void ShowVisuals()
    {
        if (closedVisual != null) closedVisual.SetActive(!IsOpen);
        if (openedVisual != null) openedVisual.SetActive(IsOpen);
    }

    void Update()
    {
        if (!IsOpen || lid == null || progress >= 1f)
            return;
        progress = Mathf.Min(1f, progress + Time.deltaTime / Mathf.Max(0.01f, openTime));
        lid.localRotation = lidBase * Quaternion.AngleAxis(Mathf.Lerp(0f, openAngle, progress), lidAxis);
    }
}
