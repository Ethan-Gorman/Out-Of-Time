using UnityEngine;
using UnityEngine.Events;

// A lever that opens a Gate when the player interacts with it. Works once.
// Each lever has its own gate(s), so several lever/gate pairs can share a level.
public class Lever : Interactable
{
    public Gate gate;

    // Any other gates the same pull opens (e.g. a portcullis and the doors behind it)
    public Gate[] moreGates;

    public Transform handle;
    public float offAngle = 40f;
    public float onAngle = -40f;
    // Local axis the handle tips around
    public Vector3 handleAxis = Vector3.right;
    public float pullTime = 0.25f;

    // What the default prompt says it opens, e.g. "the gate" or "the vault door"
    public string opensName = "the gate";

    [TextArea] public string[] pulledLines = { "Something heavy just moved... the gate is opening!" };

    public UnityEvent onPulled;

    public bool IsPulled { get; private set; }

    float progress;
    Quaternion handleBase;

    void Awake()
    {
        if (string.IsNullOrEmpty(prompt) || prompt == "Interact")
        {
            bool opensSomething = gate != null || (moreGates != null && moreGates.Length > 0);
            prompt = opensSomething && !string.IsNullOrEmpty(opensName) ? $"Pull lever <size=24>(opens {opensName})</size>" : "Pull lever";
        }
        if (handle != null)
        {
            handleBase = handle.localRotation;
            handle.localRotation = handleBase * Quaternion.AngleAxis(offAngle, handleAxis);
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
        if (moreGates != null)
            foreach (var g in moreGates)
                if (g != null)
                    g.Open();
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
        handle.localRotation = handleBase * Quaternion.AngleAxis(Mathf.Lerp(offAngle, onAngle, progress), handleAxis);
    }
}
