using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// The player's remaining time. It is both the timer and the health bar.
// Drains with world time, so standing still slows the drain (but never stops it).
// Lives on the GameManager object.
public class Clock : MonoBehaviour
{
    // Lets any script reach the clock with Clock.Instance, e.g. Clock.Instance.AddTime(5)
    public static Clock Instance { get; private set; }

    // Fired by AddTime (+seconds) and RemoveTime (-seconds), not by the normal drain.
    // ClockUI uses it for the "+5s" / "-10s" popups and the red flash
    public static event Action<float> TimeChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        TimeChanged = null;
    }

    // Time on the clock when the level starts
    public float startSeconds = 60f;

    [Range(0f, 1f)] public float idleDrain = 0.1f;

    // Seconds to show "OUT OF TIME" before the level restarts
    public float restartDelay = 2f;

    // Hook up extra reactions in the Inspector (sound, screen shake, game-over screen)
    public UnityEvent onOutOfTime;

    // Seconds left. Read-only from outside: change it with AddTime/RemoveTime
    public float Remaining { get; private set; }
    public bool IsOut => Remaining <= 0f;

    // True once the level's objective is complete: the clock stops and can't gain or lose time
    public bool IsFrozen { get; private set; }

    void Awake()
    {
        Instance = this;
        Remaining = startSeconds;
    }

    void Update()
    {
        if (IsOut || IsFrozen)
            return;

        // Drain at world speed, but never slower than idleDrain,
        // so standing still to plan still costs something
        Remaining -= Time.deltaTime * Mathf.Max(WorldTime.Scale, idleDrain) * WorldTime.ClockCost;

        if (Remaining <= 0f)
            OutOfTime();
    }

    // Call from pickups and enemy kills
    public void AddTime(float seconds)
    {
        if (IsOut || IsFrozen || seconds <= 0f)
            return;

        Remaining += seconds;
        TimeChanged?.Invoke(seconds);
    }

    // Call when the player gets hit (PlayerDamage does this)
    public void RemoveTime(float seconds)
    {
        if (IsOut || IsFrozen || seconds <= 0f)
            return;

        Remaining -= seconds;
        TimeChanged?.Invoke(-seconds);
        if (Remaining <= 0f)
            OutOfTime();
    }

    // Stops the clock for good (until the level reloads). Called by LevelManager.FinishLevel
    public void Freeze()
    {
        if (!IsOut)
            IsFrozen = true;
    }

    void OutOfTime()
    {
        Remaining = 0f;
        RunTimer.LevelFailed();

        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if (player != null)
        {
            // Stop the controller driving world time, and stop the player moving
            var controller = player.GetComponent<TimeController>();
            if (controller != null)
                controller.enabled = false;
            player.enabled = false;
            player.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            player.PlayDeath();
        }

        // Set after disabling the controller, since its OnDisable resets the scale to 1
        WorldTime.Scale = 0f;

        onOutOfTime.Invoke();
        StartCoroutine(Restart());
    }

    IEnumerator Restart()
    {
        // Realtime, because world time is frozen
        yield return new WaitForSecondsRealtime(restartDelay);
        WorldTime.Scale = 1f;

        // Reload the current scene to start the level again.
        // Goes through LevelManager so all scene loading lives in one place
        if (LevelManager.Instance != null)
            LevelManager.Instance.RestartLevel();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
