using UnityEngine;

public class CastleRunePuzzle : Interactable
{
    [SerializeField] Gate gate;
    [SerializeField] GameObject panel;
    [SerializeField] TMPro.TMP_Text status;
    [SerializeField] UnityEngine.UI.Button[] runeButtons;
    [SerializeField] UnityEngine.UI.Button closeButton;
    [SerializeField] PlayerMovement movement;
    [SerializeField] MedievalJump jump;
    [SerializeField] MedievalInteractor playerInteractor;
    [SerializeField] Rigidbody playerBody;
    [SerializeField] float interactionRange = 2.8f;
    readonly int[] solution = { 0, 1, 2 };
    int progress;
    bool movementWasEnabled;
    bool jumpWasEnabled;
    UnityEngine.InputSystem.InputAction closeAction;

    public bool Solved { get; private set; }
    public bool IsShowing => panel != null && panel.activeSelf;
    public override string Prompt => "Solve the castle rune lock";
    public override bool CanInteract => !Solved && !IsShowing;

    void Awake()
    {
        for (int i = 0; i < runeButtons.Length; i++)
        {
            int rune = i;
            runeButtons[i].onClick.AddListener(() => ChooseRune(rune));
        }
        closeButton.onClick.AddListener(Close);
        panel.SetActive(false);
        closeAction = new UnityEngine.InputSystem.InputAction("Close rune puzzle", binding: "<Keyboard>/escape");
        UnityEngine.InputSystem.InputActionSetupExtensions.AddBinding(closeAction, "<Gamepad>/buttonEast");
        closeAction.performed += OnClose;
    }

    void OnEnable() { if (closeAction != null) closeAction.Enable(); }
    void OnDisable()
    {
        Close();
        if (playerInteractor != null) playerInteractor.Exit(this);
        if (closeAction != null) closeAction.Disable();
    }
    void OnDestroy() { if (closeAction != null) closeAction.Dispose(); }
    void OnClose(UnityEngine.InputSystem.InputAction.CallbackContext context) { if (IsShowing) Close(); }

    void Update()
    {
        if (playerInteractor == null) return;
        Vector3 distance = playerInteractor.transform.position - transform.position;
        distance.y = 0f;
        if (!Solved && distance.sqrMagnitude <= interactionRange * interactionRange) playerInteractor.Enter(this);
        else playerInteractor.Exit(this);
    }

    public override void Interact(MedievalInteractor interactor)
    {
        if (!CanInteract) return;
        progress = 0;
        status.text = "Choose three seals in the order of the inscription.";
        movementWasEnabled = movement != null && movement.enabled;
        jumpWasEnabled = jump != null && jump.enabled;
        if (movement != null) movement.enabled = false;
        if (jump != null) jump.enabled = false;
        if (playerBody != null) playerBody.linearVelocity = Vector3.zero;
        panel.SetActive(true);
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(runeButtons[0].gameObject);
    }

    public void ChooseRune(int rune)
    {
        if (!IsShowing || Solved) return;
        if (rune != solution[progress])
        {
            progress = 0;
            status.text = "The seals reset. Read the inscription and try again.";
            return;
        }
        progress++;
        status.text = "Correct seals: " + progress + " / 3";
        if (progress < solution.Length) return;
        Solved = true;
        if (gate != null) gate.Open();
        Close();
        if (MedievalUI.Instance != null)
        {
            MedievalUI.Instance.Say("The rune lock opens. The way into the keep is clear.");
            MedievalUI.Instance.SetObjective("Navigate the traps and defeat the three crystal guardians");
        }
    }

    public void Close()
    {
        if (!IsShowing) return;
        panel.SetActive(false);
        if (movement != null) movement.enabled = movementWasEnabled;
        if (jump != null) jump.enabled = jumpWasEnabled;
    }
}
