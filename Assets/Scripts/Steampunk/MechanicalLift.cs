using UnityEngine;

// The factory's chain lift between the ground floor and the first floor. Dead until the gear tower
// powers it (PowerOn), then the player stands on it and presses Interact to ride.
//
// The ride runs in REAL time, not world time: the player stands still while riding, which would
// otherwise freeze the world and leave the lift hanging halfway. The player's movement is switched off
// for the ride and they are carried with the platform, so they can't step off mid-air.
// Once the player has stepped off at the top, the lift goes back down on its own, so it's always
// waiting on the ground floor. Sits on the moving platform, next to a kinematic Rigidbody,
// a solid collider (the deck) and a trigger collider (the "in range" zone).
[RequireComponent(typeof(Rigidbody))]
public class MechanicalLift : Interactable
{
    public enum State { Unpowered, Bottom, Rising, Top, Lowering }

    public float travelHeight = 4.5f;
    public float speed = 1.6f;
    public float returnDelay = 2.5f;

    [Header("Machinery that runs once powered")]
    public GearSpinner[] machinery;
    public GearSpinner[] rideWheels;
    public SteamEmitter startPuff;
    public Light statusLamp;
    public Color unpoweredLamp = new Color(0.9f, 0.2f, 0.12f);
    public Color poweredLamp = new Color(0.4f, 1f, 0.35f);

    [Header("Text")]
    [TextArea] public string[] unpoweredLines = { "PLAYER: \"The lift's dead. The gear tower must drive it.\"" };
    public string upPrompt = "Ride the lift up";
    public string downPrompt = "Ride the lift down";
    public string stepOnPrompt = "Step onto the lift";

    public State Current { get; private set; } = State.Unpowered;
    public bool Powered => Current != State.Unpowered;

    Rigidbody body;
    Vector3 bottom;
    Rigidbody rider;
    PlayerMovement riderMovement;
    float idleAtTop;

    public override string Prompt => Current switch
    {
        State.Unpowered => "Inspect the lift <size=24>(no power)</size>",
        // The "in range" trigger is wider than the deck, so the player can be beside the lift, not on it
        State.Bottom => PlayerAboard() ? upPrompt : stepOnPrompt,
        State.Top => PlayerAboard() ? downPrompt : stepOnPrompt,
        _ => "",
    };

    static bool OutOfTime => Clock.Instance != null && Clock.Instance.IsOut;

    public override bool CanInteract => Current == State.Unpowered || Current == State.Bottom || Current == State.Top;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        bottom = transform.position;
        SetMachinery(false);
    }

    public void PowerOn()
    {
        if (Powered)
            return;
        Current = State.Bottom;
        SetMachinery(true);
        if (startPuff != null)
            startPuff.Burst();
    }

    void SetMachinery(bool on)
    {
        if (machinery != null)
            foreach (var m in machinery)
                if (m != null) m.SetRunning(on);
        if (rideWheels != null)
            foreach (var w in rideWheels)
                if (w != null) w.SetRunning(false);
        if (statusLamp != null)
            statusLamp.color = on ? poweredLamp : unpoweredLamp;
    }

    public override void Interact(MedievalInteractor interactor)
    {
        if (Current == State.Unpowered)
        {
            if (MedievalUI.Instance != null)
                MedievalUI.Instance.Say(unpoweredLines);
            return;
        }
        if (Current != State.Bottom && Current != State.Top)
            return;
        // Only ride with the player standing on the deck, otherwise they'd float up outside the shaft
        if (!PlayerAboard() || !CanBoard(interactor))
            return;

        Board(interactor.GetComponent<Rigidbody>());
        Current = Current == State.Bottom ? State.Rising : State.Lowering;
        SetRideWheels(true);
    }

    // Not while out of time, or mid knockback (PlayerDamage would switch movement back on during the ride)
    static bool CanBoard(MedievalInteractor interactor)
    {
        if (interactor == null || OutOfTime)
            return false;
        var damage = interactor.GetComponent<PlayerDamage>();
        if (damage != null && damage.IsInvulnerable)
            return false;
        var movement = interactor.GetComponent<PlayerMovement>();
        return movement == null || movement.enabled;
    }

    void Board(Rigidbody player)
    {
        rider = player;
        if (rider == null)
            return;
        riderMovement = rider.GetComponent<PlayerMovement>();
        if (riderMovement != null)
            riderMovement.enabled = false;
        rider.linearVelocity = new Vector3(0f, rider.linearVelocity.y, 0f);
    }

    void Unboard()
    {
        // Movement stays off once the clock has run out
        if (riderMovement != null && !OutOfTime)
            riderMovement.enabled = true;
        rider = null;
        riderMovement = null;
    }

    void SetRideWheels(bool on)
    {
        if (rideWheels == null)
            return;
        foreach (var w in rideWheels)
            if (w != null)
            {
                w.SetRunning(on);
                w.degreesPerSecond = Mathf.Abs(w.degreesPerSecond) * (Current == State.Lowering ? -1f : 1f);
            }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        switch (Current)
        {
            case State.Rising:
                if (Move(bottom + Vector3.up * travelHeight, dt))
                {
                    Current = State.Top;
                    idleAtTop = 0f;
                    SetRideWheels(false);
                    bool arrived = rider != null && PlayerAboard() && !OutOfTime;
                    Unboard();
                    if (arrived && SteampunkLevel.Instance != null)
                        SteampunkLevel.Instance.LiftArrivedTop();
                }
                break;

            case State.Lowering:
                if (Move(bottom, dt))
                {
                    Current = State.Bottom;
                    SetRideWheels(false);
                    Unboard();
                }
                break;

            case State.Top:
                // Nobody aboard: go back down so the lift waits on the ground floor
                if (!PlayerAboard())
                {
                    idleAtTop += dt;
                    if (idleAtTop >= returnDelay)
                    {
                        Current = State.Lowering;
                        SetRideWheels(true);
                    }
                }
                else
                    idleAtTop = 0f;
                break;
        }
    }

    // Moves the deck towards the target and carries the rider with it. True on arrival
    bool Move(Vector3 target, float dt)
    {
        Vector3 from = body.position;
        Vector3 to = Vector3.MoveTowards(from, target, speed * dt);
        body.MovePosition(to);
        if (rider != null)
        {
            Vector3 delta = to - from;
            rider.position += delta;
            rider.linearVelocity = Vector3.zero;
            // A hit's knockback switches movement back on when it ends: keep it off until we arrive
            if (riderMovement != null && riderMovement.enabled)
                riderMovement.enabled = false;
        }
        return (to - target).sqrMagnitude < 0.0001f;
    }

    bool PlayerAboard()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
            return false;
        Vector3 local = transform.InverseTransformPoint(player.transform.position);
        return Mathf.Abs(local.x) < 1.6f && Mathf.Abs(local.z) < 1.6f && local.y > -0.2f && local.y < 2.5f;
    }
}
