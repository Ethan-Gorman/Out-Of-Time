using UnityEngine;
using UnityEngine.Events;

// An optional chest. Opening it adds time to the clock. Works once.
public class TreasureChest : Interactable
{
    public Transform lid;
    public float openAngle = -110f;
    public float openTime = 0.35f;

    public float timeBonus = 15f;

    [TextArea] public string[] openedLines = { "A healing potion! I feel much better." };

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
    }

    public override bool CanInteract => !IsOpen;

    public override void Interact(MedievalInteractor interactor)
    {
        if (IsOpen)
            return;
        IsOpen = true;

        if (Clock.Instance != null)
            Clock.Instance.AddTime(timeBonus);

        if (MedievalUI.Instance != null)
            MedievalUI.Instance.Say(openedLines);
        onOpened.Invoke();
    }

    void Update()
    {
        if (!IsOpen || lid == null || progress >= 1f)
            return;
        progress = Mathf.Min(1f, progress + Time.deltaTime / Mathf.Max(0.01f, openTime));
        lid.localRotation = lidBase * Quaternion.Euler(Mathf.Lerp(0f, openAngle, progress), 0f, 0f);
    }
}
