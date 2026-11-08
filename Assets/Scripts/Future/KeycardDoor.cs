using UnityEngine;

// A locked lab door: swipe the keycard and its Gate slides open. Without the card it just says it's locked.
// Needs a trigger collider on this object for the "in range" zone; the door leaves (and their colliders) live on the Gate.
public class KeycardDoor : Interactable
{
    public Gate gate;
    public FutureLevel.Item needs = FutureLevel.Item.Keycard;

    // Lamp by the door: red while locked, green once open (optional)
    public Renderer statusLamp;
    public Color lockedColor = new Color(1f, 0.25f, 0.3f);
    public Color openColor = new Color(0.3f, 1f, 0.55f);

    [Header("Text")]
    public string openPrompt = "Swipe the keycard";
    public string lockedPrompt = "Locked door";
    [TextArea] public string[] lockedLines = { "SYSTEM: \"Access denied. Authorised staff only.\"" };

    public bool IsOpen { get; private set; }

    MaterialPropertyBlock block;

    FutureLevel Level => FutureLevel.Instance;
    bool HasCard => Level != null && Level.Has(needs);

    // Without the card, the prompt says what's missing: "Locked door (needs a keycard)"
    public override string Prompt => HasCard ? openPrompt : $"{lockedPrompt} <size=24>(needs a keycard)</size>";
    public override bool CanInteract => !IsOpen;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        SetLamp(lockedColor);
    }

    // Interactor may be null (cheat menu open it directly)
    public override void Interact(MedievalInteractor interactor)
    {
        if (IsOpen)
            return;
        if (!HasCard && interactor != null)
        {
            if (MedievalUI.Instance != null)
                MedievalUI.Instance.Say(lockedLines);
            return;
        }

        IsOpen = true;
        if (gate != null)
            gate.Open();
        SetLamp(openColor);
        if (Level != null)
            Level.DoorOpened(this);
        if (interactor != null)
            interactor.Exit(this);
    }

    void SetLamp(Color c)
    {
        if (statusLamp == null)
            return;
        statusLamp.GetPropertyBlock(block);
        block.SetColor("_BaseColor", c);
        block.SetColor("_EmissionColor", c * 1.5f);
        statusLamp.SetPropertyBlock(block);
    }
}
