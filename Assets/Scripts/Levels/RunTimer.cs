using UnityEngine;

// Scores the run by the time left on the Clock when each level is finished (bonus time from kills and
// pickups included): the more left, the better. A level's best is the most time it was finished with;
// a run's best is the most time left added up over its levels.
// Also keeps real-time splits, like a speedrun timer, shown alongside: from the first level loading to the
// last level's objective being completed. Retries (running out of time) count, and so does standing still.
// Plain static class, nothing to place in a scene: LevelManager and Clock report to it.
public static class RunTimer
{
    // One finished level
    public class LevelResult
    {
        public string level;
        public float seconds;     // real time spent on this level, retries included
        public int deaths;        // times the clock ran out on this level
        public float clockLeft;   // seconds left on the Clock when it was finished: this is the score
        public float bestLeft;    // most time left this level has been finished with (this one, if it's a new best)
        public bool newBest;
        public bool cheated;      // cheat menu were used on it, so no best was saved
    }

    // True from the first level loading until the last level is finished
    public static bool Running { get; private set; }

    // True once the last level is finished: RunSeconds then holds the final time
    public static bool Complete { get; private set; }

    // The level just finished (null until one is)
    public static LevelResult LastLevel { get; private set; }

    // Clock time left added up over the levels finished this run (the run's score)
    public static float RunClockLeft { get; private set; }

    // Best full run's time left from the same starting level, whether there is one yet, and whether this run beat it
    public static float BestRunLeft { get; private set; }
    public static bool HasBestRun { get; private set; }
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
        RunClockLeft = 0f;
        BestRunLeft = 0f;
        HasBestRun = false;
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
            RunClockLeft = 0f;
            Deaths = 0;
            // Hacker Mode left on from before counts straight away
            RunCheated = CheatMenu.HackerMode;
            runStart = now;
            startLevel = level;
            HasBestRun = PlayerPrefs.HasKey(BestRunKey(level));
            BestRunLeft = PlayerPrefs.GetFloat(BestRunKey(level), 0f);
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

    // Called by CheatMenu whenever a cheat is used, so skipped or assisted times don't become bests.
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
        if (CheatMenu.HackerMode)
            MarkCheated();
        result.cheated = levelCheated;

        // More time left is better. New keys, so old real-time bests are never compared as time left
        string key = BestLevelKey(currentLevel);
        bool hasBest = PlayerPrefs.HasKey(key);
        float best = PlayerPrefs.GetFloat(key, 0f);
        result.newBest = !levelCheated && (!hasBest || clockLeft > best);
        result.bestLeft = result.newBest ? clockLeft : best;
        if (result.newBest)
            PlayerPrefs.SetFloat(key, clockLeft);
        LastLevel = result;
        RunClockLeft += clockLeft;

        if (lastLevel)
        {
            finalSeconds = now - runStart;
            Running = false;
            Complete = true;

            // Best runs are kept per starting level, so testing from a later level doesn't beat a full run
            NewBestRun = !RunCheated && (!HasBestRun || RunClockLeft > BestRunLeft);
            if (NewBestRun)
            {
                BestRunLeft = RunClockLeft;
                HasBestRun = true;
                PlayerPrefs.SetFloat(BestRunKey(startLevel), RunClockLeft);
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
        levelCheated = CheatMenu.HackerMode;
    }

    static string BestLevelKey(string level) => "OOT.BestLeft.Level." + level;
    static string BestRunKey(string firstLevel) => "OOT.BestLeft.Run." + firstLevel;
}
