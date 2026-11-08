using System.Collections;
using System.Linq;
using UnityEngine;

// Runs the Future Era story (Chronos Lab, the last era): the keycard, the Core Hall door, the two Chrono Stabilisers,
// the core coming online (rifts close, the concourse barrier opens), then charging and boarding the time machine,
// which ends the run. All text is editable in the Inspector. Sits on the LevelDirector object in the Future level.
public class FutureLevel : MonoBehaviour, ITimeMachineStory
{
    public enum Item { Keycard }

    public static FutureLevel Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    [Header("Wiring (set by the builder)")]
    public KeycardDoor securityDoor;
    public ChronoStabiliser[] stabilisers;
    public Gate coreBarrier;
    public TimeMachine timeMachine;
    public TimeRift[] rifts;
    public GameObject[] coreOfflineVisuals;
    public GameObject[] coreOnlineVisuals;
    public Light coreLight;
    public Color coreOnlineColor = new Color(0.35f, 0.9f, 1f);
    public float coreOnlineIntensity = 3f;
    // Burst from the machine's pad when the player boards
    public SteamEmitter boardBurst;

    [Header("Arrival")]
    public float introDelay = 1.4f;
    [TextArea] public string[] introLines =
    {
        "SYSTEM: \"Arrival: Chronos Lab. Local year: unstable.\"",
        "PLAYER: \"The future. It's... stuttering.\"",
        "SYSTEM: \"Temporal charge 0%. Lab Chrono Core offline.\"",
    };
    public string keycardObjective = "Find a security keycard in the Archive";
    public string keycardHint = "Through the doorway east of the time machine";

    [Header("Keycard and door")]
    [TextArea] public string[] keycardLines = { "PLAYER: \"A keycard. 'CORE HALL: AUTHORISED STAFF.' Close enough.\"" };
    public string doorObjective = "Open the Core Hall door with the keycard";
    public string doorHint = "North-east corner of the Archive";
    [TextArea] public string[] doorLines =
    {
        "SYSTEM: \"Access granted.\"",
        "SYSTEM: \"Warning: temporal rifts detected. Contact costs time.\"",
    };

    [Header("Stabilisers")]
    public string stabiliserObjective = "Restart the Chrono Stabilisers";
    public string stabiliserHint = "One past the rift field, one in the Calibration Bay";
    [TextArea] public string[] firstStabiliserLines = { "PLAYER: \"One stabiliser running. The core's flickering...\"" };
    [TextArea] public string[] coreOnlineLines =
    {
        "SYSTEM: \"Chrono Core online. Timeline stabilising.\"",
        "PLAYER: \"The rifts are closing! And the barrier to the concourse is open.\"",
    };
    public string returnObjective = "Return to the time machine";
    public string returnHint = "Shortcut: the barrier into the concourse";

    [Header("Time machine")]
    [TextArea] public string[] notReadyLines = { "PLAYER: \"Temporal charge 0%. I need the lab's Chrono Core online.\"" };
    [TextArea] public string[] notReadyWithCardLines = { "PLAYER: \"Temporal charge 0%. The Core Hall should be through that Archive.\"" };
    [TextArea] public string[] repairedLines =
    {
        "SYSTEM: \"Charging from Chrono Core... 100%.\"",
        "SYSTEM: \"Destination: home. The present.\"",
        "PLAYER: \"Finally. No more falling.\"",
    };
    public string boardObjective = "Board the time machine";
    [TextArea] public string endBanner = "BACK TO THE PRESENT\n<size=36>Run complete</size>";
    // The traveller is "in" the machine: hide the player and stop them walking
    public bool hidePlayerOnBoard = true;
    // Real seconds after the end before a fresh run starts from the first era (0 = never; handy for a showcase machine)
    public float restartRunAfter = 0f;

    public bool HasKeycard { get; private set; }
    public bool DoorOpen { get; private set; }
    public int StabilisersOnline => stabilisers == null ? 0 : stabilisers.Count(s => s != null && s.IsOnline);
    public bool CoreOnline { get; private set; }

    void Awake()
    {
        Instance = this;
        // The last era today, so boarding ends the run; if another era is added after this one, the machine travels on
        if (timeMachine != null)
            timeMachine.loadNextEra = EraOrder.HasNext();
        SetCore(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(introDelay);
        Say(introLines);
        RefreshObjective();
    }

    public void Collect(Item item)
    {
        if (item == Item.Keycard && !HasKeycard)
        {
            HasKeycard = true;
            Say(keycardLines);
        }
        RefreshObjective();
    }

    public bool Has(Item item) => item == Item.Keycard && HasKeycard;

    public void DoorOpened(KeycardDoor door)
    {
        if (DoorOpen)
            return;
        DoorOpen = true;
        // Cheats can open it without the card: count it as held from here on
        HasKeycard = true;
        Say(doorLines);
        RefreshObjective();
    }

    public void StabiliserStarted(ChronoStabiliser stabiliser)
    {
        string[] own = stabiliser != null && stabiliser.startLines != null ? stabiliser.startLines : new string[0];
        int total = stabilisers != null ? stabilisers.Count(s => s != null) : 0;
        if (!CoreOnline && StabilisersOnline >= total)
        {
            BringCoreOnline();
            Say(own.Concat(coreOnlineLines).ToArray());
        }
        else
            Say(own.Concat(firstStabiliserLines).ToArray());
        RefreshObjective();
    }

    void BringCoreOnline()
    {
        CoreOnline = true;
        SetCore(true);
        if (rifts != null)
            foreach (var rift in rifts)
                if (rift != null)
                    rift.Close();
        if (coreBarrier != null)
            coreBarrier.Open();
    }

    void SetCore(bool online)
    {
        if (coreOfflineVisuals != null)
            foreach (var go in coreOfflineVisuals)
                if (go != null) go.SetActive(!online);
        if (coreOnlineVisuals != null)
            foreach (var go in coreOnlineVisuals)
                if (go != null) go.SetActive(online);
        if (online && coreLight != null)
        {
            coreLight.color = coreOnlineColor;
            coreLight.intensity = coreOnlineIntensity;
        }
    }

    void RefreshObjective()
    {
        var ui = MedievalUI.Instance;
        if (ui == null)
            return;
        // Once the machine is charged its own objective stays
        if (timeMachine != null && timeMachine.Current != TimeMachine.State.Broken)
            return;

        if (CoreOnline) { ui.SetObjective($"{returnObjective}\n{returnHint}"); return; }
        if (DoorOpen)
        {
            int online = StabilisersOnline, total = stabilisers != null ? stabilisers.Count(s => s != null) : 0;
            string hint = stabiliserHint;
            if (online > 0)
            {
                var left = stabilisers.FirstOrDefault(s => s != null && !s.IsOnline);
                if (left != null && !string.IsNullOrEmpty(left.hint))
                    hint = left.hint;
            }
            ui.SetObjective($"{stabiliserObjective} ({online}/{total})\n{hint}");
            return;
        }
        if (HasKeycard) { ui.SetObjective($"{doorObjective}\n{doorHint}"); return; }
        ui.SetObjective($"{keycardObjective}\n{keycardHint}");
    }

    static void Say(string[] lines)
    {
        if (MedievalUI.Instance != null)
            MedievalUI.Instance.Say(lines);
    }

    // ITimeMachineStory
    public bool CanRepair => CoreOnline;
    public string[] NotReadyLines => HasKeycard ? notReadyWithCardLines : notReadyLines;

    public void TimeMachineRepaired()
    {
        Say(repairedLines);
        if (MedievalUI.Instance != null)
            MedievalUI.Instance.SetObjective(boardObjective);
    }

    public void TimeMachineBoarded(bool travelling)
    {
        var ui = MedievalUI.Instance;
        if (ui != null)
        {
            ui.SetObjective("");
            if (!travelling)
                ui.ShowBanner(endBanner);
        }
        if (travelling)
            return;
        if (hidePlayerOnBoard)
            HidePlayer();
        if (boardBurst != null)
            boardBurst.Burst();
        if (timeMachine != null && timeMachine.glow != null)
            StartCoroutine(Flare(timeMachine.glow));
        if (restartRunAfter > 0f)
            StartCoroutine(RestartRun());
    }

    // Like running out of time, but happy: the controller stops, so world time runs on and the burst plays
    static void HidePlayer()
    {
        var player = FindAnyObjectByType<PlayerMovement>();
        if (player == null)
            return;
        var controller = player.GetComponent<TimeController>();
        if (controller != null)
            controller.enabled = false;
        player.enabled = false;
        var body = player.GetComponent<Rigidbody>();
        if (body != null)
            body.linearVelocity = Vector3.zero;
        var sprite = player.transform.Find("Sprite");
        if (sprite != null)
            sprite.gameObject.SetActive(false);
    }

    // The machine glows up to full, in real time
    static IEnumerator Flare(Light glow)
    {
        glow.enabled = true;
        float start = glow.intensity, peak = Mathf.Max(start * 3f, 4f);
        for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
        {
            glow.intensity = Mathf.Lerp(start, peak, Mathf.Sin(t / 1.2f * Mathf.PI * 0.5f));
            yield return null;
        }
        glow.intensity = peak;
    }

    IEnumerator RestartRun()
    {
        yield return new WaitForSecondsRealtime(restartRunAfter);
        ScreenFader.LoadScene(0);
    }
}
