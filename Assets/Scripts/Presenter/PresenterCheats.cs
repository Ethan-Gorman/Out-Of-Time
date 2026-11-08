using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Presenter cheats for demoing the game quickly (game jam presentation). Creates itself on startup, no setup needed.
// Active in the Editor and in Development builds; in a normal build, type "hacker" to unlock them.
//
//   F1-F4  jump to Caveman / Medieval / Steampunk / Future
//   N      finish this level and travel to the next era
//   O      complete the next objective (the story, dialogue and objectives still play)
//   P      teleport to the next point of interest in this level
//   T      +30 seconds on the clock
//   H      Hacker Mode: the clock stops draining and nothing can hurt you
//   `      show / hide this list
public class PresenterCheats : MonoBehaviour
{
    // Read by Clock (no drain, no time loss) and PlayerDamage (no hits)
    public static bool HackerMode { get; private set; }

    static readonly string[] EraScenes = { "Caveman", "Medieval", "Steampunk", "Future" };
    const string UnlockWord = "hacker";

    bool unlocked;
    bool showList;
    string typed = "";
    int poiIndex;
    string toast;
    float toastUntil;
    GUIStyle style, boxStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        HackerMode = false;
        if (FindAnyObjectByType<PresenterCheats>() != null)
            return;
        var go = new GameObject("PresenterCheats");
        DontDestroyOnLoad(go);
        go.AddComponent<PresenterCheats>();
    }

    void Awake()
    {
        unlocked = Application.isEditor || Debug.isDebugBuild;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Domain reload is off (fast Play mode), so static events must be unsubscribed or they pile up between sessions
    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => poiIndex = 0;

    void OnEnable()
    {
        if (Keyboard.current != null)
            Keyboard.current.onTextInput += OnText;
    }

    void OnDisable()
    {
        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= OnText;
    }

    void OnText(char c)
    {
        if (unlocked)
            return;
        typed = (typed + char.ToLowerInvariant(c));
        if (typed.Length > UnlockWord.Length)
            typed = typed.Substring(typed.Length - UnlockWord.Length);
        if (typed == UnlockWord)
        {
            unlocked = true;
            Toast("Cheats unlocked. Press ` for the list");
        }
    }

    void Update()
    {
        var k = Keyboard.current;
        if (!unlocked || k == null)
            return;

        if (k.backquoteKey.wasPressedThisFrame) showList = !showList;
        if (k.f1Key.wasPressedThisFrame) LoadEra(0);
        if (k.f2Key.wasPressedThisFrame) LoadEra(1);
        if (k.f3Key.wasPressedThisFrame) LoadEra(2);
        if (k.f4Key.wasPressedThisFrame) LoadEra(3);
        if (k.nKey.wasPressedThisFrame) NextEra();
        if (k.oKey.wasPressedThisFrame) CompleteObjective();
        if (k.pKey.wasPressedThisFrame) TeleportToNext();
        if (k.tKey.wasPressedThisFrame && Clock.Instance != null) { Clock.Instance.AddTime(30f); Toast("+30 seconds"); }
        if (k.hKey.wasPressedThisFrame) { HackerMode = !HackerMode; Toast(HackerMode ? "HACKER MODE ON: time is on your side" : "Hacker Mode off"); }
    }

    // ---------------------------------------------------------------- eras

    void LoadEra(int era)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            if (System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)) == EraScenes[era])
            {
                SceneManager.LoadScene(i);
                return;
            }
        Toast($"No {EraScenes[era]} scene in Build Settings yet");
    }

    void NextEra()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        string path = next > 0 && next < SceneManager.sceneCountInBuildSettings ? SceneUtility.GetScenePathByBuildIndex(next) : "";
        if (System.Array.IndexOf(EraScenes, System.IO.Path.GetFileNameWithoutExtension(path)) < 0)
        {
            Toast("This is the last era so far");
            return;
        }
        if (LevelManager.Instance != null) LevelManager.Instance.CompleteLevel();
        else SceneManager.LoadScene(next);
        Toast("Travelling to the next era...");
    }

    // ---------------------------------------------------------------- objectives

    void CompleteObjective()
    {
        string done =
            CavemanLevel.Instance != null ? CavemanStep() :
            MedievalLevel.Instance != null ? MedievalStep() :
            SteampunkLevel.Instance != null ? SteampunkStep() :
            MachineStep();
        Toast(done ?? "Nothing left to skip here");
    }

    string CavemanStep()
    {
        var level = CavemanLevel.Instance;
        if (!level.HasAxe)
        {
            level.AddResource(CavemanLevel.Resource.Wood, Mathf.Max(0, level.axeWoodCost - level.Wood));
            level.AddResource(CavemanLevel.Resource.Stone, Mathf.Max(0, level.axeStoneCost - level.Stone));
            level.CraftAxe();
            return "Skipped: gathered wood and stone, crafted the Stone Axe";
        }
        if (!level.BoarKilled)
        {
            var boar = FindAnyObjectByType<WildBoar>();
            for (int i = 0; boar != null && boar.CanInteract && i < 20; i++)
                boar.Interact(null);
            return "Skipped: defeated the wild boar";
        }
        if (!level.CanRepair)
        {
            foreach (var bramble in FindObjectsByType<Breakable>())
                if (bramble.requireBoarKilled) Destroy(bramble.gameObject);
            foreach (var stone in FindObjectsByType<MagneticStone>())
            {
                level.CollectMagneticStone();
                Destroy(stone.gameObject);
            }
            return "Skipped: cleared the brambles, collected the magnetic stones";
        }
        return MachineStep();
    }

    string MedievalStep()
    {
        var level = MedievalLevel.Instance;
        if (!level.HasCrystal)
        {
            foreach (var crystal in FindObjectsByType<TimeCrystal>())
                Destroy(crystal.gameObject);
            level.CollectCrystal();
            return "Skipped: collected the Time Crystal";
        }
        return MachineStep();
    }

    string SteampunkStep()
    {
        var level = SteampunkLevel.Instance;
        if (!level.HasChain) { TakeItem(SteampunkLevel.Item.Chain); return "Skipped: found the spare chain"; }
        if (level.gear1 != null && !level.gear1.IsRepaired) { level.gear1.Interact(null); return "Skipped: fitted the chain to Gear 1"; }
        if (!level.HasOil) { TakeItem(SteampunkLevel.Item.Oil); return "Skipped: found the oil can"; }
        if (level.gear2 != null && !level.gear2.IsRepaired) { level.gear2.Interact(null); return "Skipped: oiled Gear 2 (Gear 3 starts by itself)"; }
        if (level.lift != null && !level.lift.Powered) return "Gear 3 is starting up... press O again in a second";
        if (!level.ReachedFirstFloor)
        {
            Teleport(new Vector3(19f, SteampunkFactoryHeight + 0.2f, 4f));
            level.LiftArrivedTop();
            return "Skipped: rode the lift to the first floor";
        }
        return MachineStep();
    }

    // Steampunk first floor height (kept here so this runtime script doesn't depend on the Editor builder)
    const float SteampunkFactoryHeight = 4.5f;

    void TakeItem(SteampunkLevel.Item item)
    {
        foreach (var pickup in FindObjectsByType<SteampunkItem>())
            if (pickup.item == item) Destroy(pickup.gameObject);
        SteampunkLevel.Instance.Collect(item);
    }

    // The last steps are the same in every era: repair the time machine, then board it
    string MachineStep()
    {
        var machine = FindAnyObjectByType<TimeMachine>();
        if (machine == null)
            return null;
        if (machine.Current == TimeMachine.State.Broken)
        {
            Teleport(machine.transform.position + new Vector3(0f, 0.2f, -2.2f));
            // The story only allows the repair once its parts are in; make sure the earlier steps are done first
            if (!machine.CanInteract || machine.Prompt == machine.inspectPrompt)
                return "Finish the earlier objectives first (press O again)";
            machine.Interact(null);
            return "Skipped: repaired the time machine";
        }
        if (machine.Current == TimeMachine.State.Repaired)
        {
            machine.Interact(null);
            return "Boarded the time machine";
        }
        return null;
    }

    // ---------------------------------------------------------------- teleport

    void TeleportToNext()
    {
        var points = PointsOfInterest();
        if (points.Count == 0) { Toast("No points of interest here"); return; }
        poiIndex %= points.Count;
        var (name, target) = points[poiIndex++];
        Teleport(target.position + new Vector3(0f, 0.3f, -2f));
        Toast($"Teleported: {name} ({poiIndex}/{points.Count})");
    }

    // In story order for each era; whatever no longer exists (picked up, killed) is skipped
    static List<(string, Transform)> PointsOfInterest()
    {
        var list = new List<(string, Transform)>();
        void Add<T>(string name) where T : Component
        {
            foreach (var c in FindObjectsByType<T>())
                if (c != null && c.gameObject.activeInHierarchy) { list.Add((name, c.transform)); break; }
        }
        Add<CraftingStation>("crafting stone");
        Add<WildBoar>("wild boar");
        Add<MagneticStone>("magnetic stone");
        Add<Lever>("lever");
        Add<TimeCrystal>("Time Crystal");
        Add<TreasureChest>("treasure chest");
        Add<GearRepair>("gear tower");
        Add<SteampunkItem>("story item");
        Add<ConveyorBelt>("conveyor belt");
        Add<MechanicalLift>("lift");
        Add<ClockworkGuard>("Clockwork Guard");
        Add<TimeMachine>("time machine");
        return list;
    }

    static void Teleport(Vector3 position)
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
            return;
        var body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector3.zero;
        }
        player.transform.position = position;
    }

    // ---------------------------------------------------------------- on-screen text

    void Toast(string text)
    {
        toast = text;
        toastUntil = Time.unscaledTime + 2.5f;
    }

    void OnGUI()
    {
        if (!unlocked)
            return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 45), richText = true };
            style.normal.textColor = new Color(0.95f, 0.9f, 0.75f);
            boxStyle = new GUIStyle(GUI.skin.box);
        }
        float line = style.fontSize * 1.5f;

        if (HackerMode)
            GUI.Label(new Rect(Screen.width - 260, Screen.height - line * 1.5f, 250, line), "<color=#7CFF7C>HACKER MODE</color>", style);

        if (toast != null && Time.unscaledTime < toastUntil)
        {
            var size = style.CalcSize(new GUIContent(toast));
            var r = new Rect((Screen.width - size.x) * 0.5f - 12, Screen.height * 0.18f, size.x + 24, line + 8);
            GUI.Box(r, GUIContent.none, boxStyle);
            GUI.Label(new Rect(r.x + 12, r.y + 4, size.x, line), toast, style);
        }

        if (!showList)
            return;
        string[] rows =
        {
            "<b>PRESENTER CHEATS</b>",
            "F1-F4   Caveman / Medieval / Steampunk / Future",
            "N       next era",
            "O       complete next objective",
            "P       teleport to next point of interest",
            "T       +30 seconds",
            "H       Hacker Mode " + (HackerMode ? "<color=#7CFF7C>(on)</color>" : "(off)"),
            "`       hide this list",
        };
        var box = new Rect(20, Screen.height * 0.3f, style.fontSize * 24, line * rows.Length + 16);
        GUI.Box(box, GUIContent.none, boxStyle);
        GUI.Box(box, GUIContent.none, boxStyle);
        for (int i = 0; i < rows.Length; i++)
            GUI.Label(new Rect(box.x + 12, box.y + 8 + i * line, box.width - 24, line), rows[i], style);
    }
}
