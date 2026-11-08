using UnityEngine;

// One of the Chrono Stabilisers the player restarts in the Core Hall. Restarting both brings the Chrono Core online
// (FutureLevel). It can also power down hazards when it starts (the Calibration Bay's laser grid).
// Needs a trigger collider on this object for the "in range" zone. Keep solid colliders and art on children.
public class ChronoStabiliser : Interactable
{
    [Header("Art (swap freely)")]
    public GameObject offVisual;
    public GameObject onVisual;
    public Light glow;
    public GearSpinner[] spinners;
    public SteamEmitter startPuff;

    [Header("When it starts")]
    // Laser fences switched off, and any other objects hidden
    public LaserFence[] powerOff;
    public GameObject[] disableOnStart;
    [TextArea] public string[] startLines;

    // Objective hint while this one is still off, e.g. "Past the rift field"
    public string hint = "";

    public bool IsOnline { get; private set; }

    public override bool CanInteract => !IsOnline;

    void Reset()
    {
        prompt = "Restart the stabiliser";
    }

    void Awake()
    {
        if (spinners != null)
            foreach (var s in spinners)
                if (s != null)
                    s.SetRunning(false);
        Refresh();
    }

    // Interactor may be null (cheat menu start it directly)
    public override void Interact(MedievalInteractor interactor)
    {
        if (IsOnline)
            return;
        IsOnline = true;
        Refresh();
        if (spinners != null)
            foreach (var s in spinners)
                if (s != null)
                    s.SetRunning(true);
        if (startPuff != null)
            startPuff.Burst();
        if (powerOff != null)
            foreach (var fence in powerOff)
                if (fence != null)
                    fence.PowerOff();
        if (disableOnStart != null)
            foreach (var go in disableOnStart)
                if (go != null)
                    go.SetActive(false);

        if (FutureLevel.Instance != null)
            FutureLevel.Instance.StabiliserStarted(this);
        else if (MedievalUI.Instance != null)
            MedievalUI.Instance.Say(startLines);
        if (interactor != null)
            interactor.Exit(this);
    }

    void Refresh()
    {
        if (offVisual != null) offVisual.SetActive(!IsOnline);
        if (onVisual != null) onVisual.SetActive(IsOnline);
        if (glow != null) glow.enabled = IsOnline;
    }
}
