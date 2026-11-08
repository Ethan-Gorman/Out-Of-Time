using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tracks the current era and handles all scene loading.
// Part of the Persistent prefab (nested in LevelEssentials). It survives scene loads,
// so the first copy lives for the whole game and the copies in later scenes remove themselves.
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    // Fired when a scene with a different era loads (not when the same era restarts)
    public static event Action<Era> EraChanged;

    // Real seconds the finished level's results stay on screen before the next level loads
    public float resultsTime = 3f;

    public Era CurrentEra { get; private set; }

    // True once this level's objective is complete (FinishLevel). Resets when a scene loads
    public bool LevelFinished { get; private set; }

    bool hasEra;
    Coroutine loadingNext;

    // Statics survive between play sessions when domain reload is off, so reset on play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        EraChanged = null;
    }

    void Awake()
    {
        // A copy already exists from an earlier scene, so this one is a duplicate
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad only works on root objects, so detach from LevelEssentials first
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // Covers the first scene, in case sceneLoaded fired before we subscribed
        RefreshEra();
        RunTimer.EnterLevel(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LevelFinished = false;
        // Any scene load (a retry, a cheat jump) replaces the level, so a pending next-era load must not fire into it
        CancelPendingLoad();
        RefreshEra();
        RunTimer.EnterLevel(scene.name);
    }

    // Finds the scene's LevelInfo and announces the era if it changed.
    // Runs on every scene load; LevelInfo also calls it when Era is edited in Play mode
    public void RefreshEra()
    {
        var info = FindAnyObjectByType<LevelInfo>();
        if (info == null)
        {
            Debug.LogWarning("[LevelManager] No LevelInfo in this scene, keeping the current era.");
            return;
        }

        if (hasEra && info.era == CurrentEra)
            return;

        CurrentEra = info.era;
        hasEra = true;
        EraChanged?.Invoke(CurrentEra);
    }

    // Reloads the current scene (with a fade, ScreenFader). Called by Clock when time runs out.
    // Also works in the Editor for scenes not in Build Settings yet
    public void RestartLevel()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
            ScreenFader.LoadScene(scene.buildIndex);
        else
            ScreenFader.LoadScene(scene.path);
    }

    // The level's objective is complete: freezes the Clock and records the time left on it (RunTimer).
    // The last era built so far (EraOrder) also ends the run.
    // Safe to call more than once. CompleteLevel calls it too
    public void FinishLevel() => FinishLevel(false);

    // lastLevel ends the run even when another era follows, e.g. a time machine that doesn't travel on
    public void FinishLevel(bool lastLevel)
    {
        if (LevelFinished)
            return;
        LevelFinished = true;

        float clockLeft = 0f;
        if (Clock.Instance != null)
        {
            Clock.Instance.Freeze();
            clockLeft = Clock.Instance.Remaining;
        }

        RunTimer.FinishLevel(clockLeft, lastLevel || !HasNextLevel());
    }

    // Finishes the level, shows its results for resultsTime, then loads the next era's scene (EraOrder)
    public void CompleteLevel()
    {
        FinishLevel();
        if (!HasNextLevel())
        {
            Debug.Log($"[LevelManager] Final era complete. Time left {RunTimer.Format(RunTimer.RunClockLeft)}, run time {RunTimer.Format(RunTimer.RunSeconds)}");
            return;
        }

        if (loadingNext == null)
            loadingNext = StartCoroutine(LoadNextLevel(EraOrder.NextBuildIndex()));
    }

    // Stops a next-era load that is still waiting out the results. Called on every scene load,
    // and by PresenterCheats before it loads a scene itself
    public void CancelPendingLoad()
    {
        if (loadingNext != null)
            StopCoroutine(loadingNext);
        loadingNext = null;
    }

    IEnumerator LoadNextLevel(int index)
    {
        // Realtime, so it doesn't depend on the world moving
        yield return new WaitForSecondsRealtime(resultsTime);
        loadingNext = null;
        ScreenFader.LoadScene(index);
    }

    // Another era's scene comes after this one. Non-era scenes (SampleScene, tests) never travel on
    static bool HasNextLevel() => EraOrder.HasNext();
}
