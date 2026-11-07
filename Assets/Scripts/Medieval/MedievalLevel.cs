using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

// Runs the Medieval Era story: intro dialogue, objectives, and what happens when the
// Time Crystal is collected and the time machine is repaired. All text is editable in the Inspector.
// Sits on the LevelDirector object in the Medieval scene.
public class MedievalLevel : MonoBehaviour
{
    public static MedievalLevel Instance { get; private set; }

    public float introDelay = 0.6f;

    [TextArea] public string[] introLines =
    {
        "Ugh... where am I? This isn't the Stone Age any more.",
        "The time machine broke on the way here! It needs a Time Crystal to run.",
        "There must be one somewhere inside this castle.",
    };
    public string startObjective = "Find the Time Crystal in the castle";

    [TextArea] public string[] crystalLines =
    {
        "A Time Crystal! This should power the time machine.",
        "Now back the way I came.",
    };
    public string returnObjective = "Return to the time machine";

    [TextArea] public string[] repairedLines =
    {
        "It's working! The time machine is repaired.",
        "Next stop: the Industrial Era.",
    };
    public string boardObjective = "Board the time machine";

    [TextArea] public string[] departLines =
    {
        "Hmm... the dial won't go past the Medieval Era yet.",
    };
    [TextArea] public string nextEraBanner = "INDUSTRIAL ERA\n<size=36>Coming soon</size>";

    public bool HasCrystal { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(introDelay);
        var ui = MedievalUI.Instance;
        if (ui == null)
            yield break;
        ui.Say(introLines);
        ui.SetObjective(startObjective);
    }

    public void CollectCrystal()
    {
        if (HasCrystal)
            return;
        HasCrystal = true;

        var ui = MedievalUI.Instance;
        if (ui == null)
            return;
        ui.Say(crystalLines);
        ui.SetObjective(returnObjective);
    }

    public void TimeMachineRepaired()
    {
        var ui = MedievalUI.Instance;
        if (ui == null)
            return;
        ui.Say(repairedLines);
        ui.SetObjective(boardObjective);
    }

    // The next era doesn't exist yet, so the player stays here with a "coming soon" banner
    public void TimeMachineBoarded()
    {
        var ui = MedievalUI.Instance;
        if (ui == null)
            return;
        ui.SetObjective("");
        ui.Say(departLines);
        ui.ShowBanner(nextEraBanner);
    }

    // Reloads the current scene. Works even when the scene isn't in Build Settings yet (Editor only)
    public static void RestartScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.RestartLevel();
            else
                SceneManager.LoadScene(scene.buildIndex);
            return;
        }

#if UNITY_EDITOR
        EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.name);
#endif
    }
}
