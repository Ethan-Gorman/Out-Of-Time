using UnityEngine;

// Scores the whole run in real time, like a speedrun timer: from the first level loading to the last level's
// objective being completed. Retries (running out of time) count, and so does standing still.
// Each level gets a split (real time from first entering it to finishing it), and best times are saved.
// Plain static class, nothing to place in a scene: LevelManager and Clock report to it.
public static class RunTimer
{
    // One finished level
    public class LevelResult
    {
        public string level;
        public float seconds;     // real time spent on this level, retries included
        public int deaths;        // times the clock ran out on this level
        public float clockLeft;   // seconds left on the Clock when it was finished
        public float bestSeconds; // best split for this level (this one, if it's a new best)
        public bool newBest;
        public bool cheated;      // presenter cheats were used on it, so no best time was saved
    }

    // True from the first level loading until the last level is finished
    public static bool Running { get; private set; }

    // True once the last level is finished: RunSeconds then holds the final time
    public static bool Complete { get; private set; }

    // The level just finished (null until one is)
    public static LevelResult LastLevel { get; private set; }

    // Best full run from the same starting level (0 if there is none yet), and whether this run beat it
    public static float BestRunSeconds { get; private set; }
    public static bool NewBestRun { get; private set; }

    public static int Deaths { get; private set; }

    // True once a presenter cheat was used this run: the run's best time isn't saved
    public static bool RunCheated { get; private set; }

    // Real seconds since the run started (frozen once it's complete)
    public static float RunSeconds => Running ? Time.realtimeSinceStartup - runStart : finalSeconds;

    // Real seconds on the current level so far
    public static float LevelSeconds => Running ? Time.realtimeSinceStartup - levelStart : 0f;

    static float runStart;
    static float levelStart;
    static float finalSeconds;
    static string currentLevel;
    static string startLevel;
    static int levelDeaths;
    static bool levelFinished;
    static bool levelCheated;

    // Statics survive between play sessions when domain reload is off, so reset on play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Running = false;
        Complete = false;
        LastLevel = null;
        BestRunSeconds = 0f;
        NewBestRun = false;
        Deaths = 0;
        RunCheated = false;
        finalSeconds = 0f;
        currentLevel = null;
        startLevel = null;
        levelDeaths = 0;
        levelFinished = false;
        levelCheated = false;
    }

    // Called by LevelManager whenever a level scene loads. Loading the same level again is a retry,
    // so its split keeps running; a new level starts a new split. The first level starts the run
    public static void EnterLevel(string level)
    {
        float now = Time.realtimeSinceStartup;

        // A fresh run: first level of the session, or playing again after finishing
        if (!Running)
        {
            Running = true;
            Complete = false;
            NewBestRun = false;
            LastLevel = null;
            Deaths = 0;
            // Hacker Mode left on from before counts straight away
            RunCheated = PresenterCheats.HackerMode;
            runStart = now;
            startLevel = level;
            BestRunSeconds = PlayerPrefs.GetFloat(BestRunKey(level), 0f);
            StartLevel(level, now);
            return;
        }

        // Retry of the level we're on: the split keeps counting
        if (level == currentLevel && !levelFinished)
            return;

        StartLevel(level, now);
    }

    // Called by Clock when time runs out
    public static void LevelFailed()
    {
        if (!Running || levelFinished)
            return;
        levelDeaths++;
        Deaths++;
    }

    // Called by PresenterCheats whenever a cheat is used, so skipped or assisted times don't become bests.
    // The split still shows; the flag clears when a fresh run starts
    public static void MarkCheated()
    {
        levelCheated = true;
        RunCheated = true;
    }

    // Called by LevelManager when the level's objective is complete. lastLevel ends the run
    public static LevelResult FinishLevel(float clockLeft, bool lastLevel)
    {
        if (!Running || levelFinished)
            return LastLevel;
        levelFinished = true;

        float now = Time.realtimeSinceStartup;
        var result = new LevelResult
        {
            level = currentLevel,
            seconds = now - levelStart,
            deaths = levelDeaths,
            clockLeft = clockLeft,
        };

        // Hacker Mode can stay on across levels, so it counts even without a fresh MarkCheated
        if (PresenterCheats.HackerMode)
            MarkCheated();
        result.cheated = levelCheated;

        string key = "OOT.Best.Level." + currentLevel;
        float best = PlayerPrefs.GetFloat(key, 0f);
        result.newBest = !levelCheated && (best <= 0f || result.seconds < best);
        result.bestSeconds = result.newBest ? result.seconds : best;
        if (result.newBest)
            PlayerPrefs.SetFloat(key, result.seconds);
        LastLevel = result;

        if (lastLevel)
        {
            finalSeconds = now - runStart;
            Running = false;
            Complete = true;

            // Best runs are kept per starting level, so testing from a later level doesn't beat a full run
            NewBestRun = !RunCheated && (BestRunSeconds <= 0f || finalSeconds < BestRunSeconds);
            if (NewBestRun)
            {
                BestRunSeconds = finalSeconds;
                PlayerPrefs.SetFloat(BestRunKey(startLevel), finalSeconds);
            }
        }

        PlayerPrefs.Save();
        return result;
    }

    // m:ss.ss, e.g. 1:09.42
    public static string Format(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int minutes = (int)(seconds / 60f);
        return $"{minutes}:{seconds % 60f:00.00}";
    }

    static void StartLevel(string level, float now)
    {
        currentLevel = level;
        levelStart = now;
        levelDeaths = 0;
        levelFinished = false;
        // Hacker Mode still on from the last level counts, even if it's turned off before this one ends
        levelCheated = PresenterCheats.HackerMode;
    }

    static string BestRunKey(string firstLevel) => "OOT.Best.Run." + firstLevel;
}
