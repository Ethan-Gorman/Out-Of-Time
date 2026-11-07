using UnityEngine;
using UnityEngine.Events;

// A lever that opens a Gate when the player interacts with it. Works once.
public class Lever : Interactable
{
    public Gate gate;

    [Tooltip("The part that tips over when pulled. Optional, art only.")]
    public Transform handle;
    public float offAngle = 40f;
    public float onAngle = -40f;
    public float pullTime = 0.25f;

    [TextArea] public string[] pulledLines = { "Something heavy just moved... the gate is opening!" };

    [Tooltip("Extra reactions: sounds, camera shake, other doors.")]
    public UnityEvent onPulled;

    public bool IsPulled { get; private set; }

    float progress;
    Quaternion handleBase;

    void Awake()
    {
        if (string.IsNullOrEmpty(prompt) || prompt == "Interact")
            prompt = "Pull lever";
        if (handle != null)
        {
            handleBase = handle.localRotation;
            handle.localRotation = handleBase * Quaternion.Euler(offAngle, 0f, 0f);
        }
    }

    public override bool CanInteract => !IsPulled;

    public override void Interact(MedievalInteractor interactor)
    {
        if (IsPulled)
            return;
        IsPulled = true;

        if (gate != null)
            gate.Open();
        if (MedievalUI.Instance != null)
            MedievalUI.Instance.Say(pulledLines);
        onPulled.Invoke();
    }

    void Update()
    {
        if (!IsPulled || handle == null || progress >= 1f)
            return;

        // The player pulls it, so it moves in real time
        progress = Mathf.Min(1f, progress + Time.deltaTime / Mathf.Max(0.01f, pullTime));
        handle.localRotation = handleBase * Quaternion.Euler(Mathf.Lerp(offAngle, onAngle, progress), 0f, 0f);
    }
}
