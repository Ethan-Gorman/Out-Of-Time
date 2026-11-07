using System;
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

    public Era CurrentEra { get; private set; }

    bool hasEra;

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
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshEra();
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

    // Reloads the current scene. Called by Clock when time runs out
    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Loads the next scene in Build Settings, so era scenes must be listed in play order.
    // Not hooked up yet: call this from whatever ends a level
    public void CompleteLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            Debug.Log("[LevelManager] Final era complete.");
    }
}
