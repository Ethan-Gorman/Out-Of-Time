using UnityEngine;

// Gear 3: no item, no interaction. It is held by a brake until Gear 2 turns; then the brake lifts and the
// connecting rod between the two gears' crank pins drags Gear 3 round, so the tower reads as one machine.
// Also keeps the rod between the two crank pins every frame.
public class GearLink : MonoBehaviour
{
    public GearSpinner gear2;
    public GearSpinner gear3;

    [Header("Connecting rod")]
    public Transform rod;
    public Transform pin2;
    public Transform pin3;

    [Header("Brake")]
    public Transform brake;
    public Vector3 brakeReleasedEuler = new Vector3(0f, 0f, 55f);
    public float brakeTime = 0.6f;
    // Real seconds between Gear 2 starting and the brake letting go
    public float releaseDelay = 1.2f;
    public SteamEmitter releasePuff;

    public bool IsRunning { get; private set; }

    bool released;
    float timer;
    Quaternion brakeLocked;

    void Awake()
    {
        if (gear3 != null)
            gear3.SetRunning(false);
        if (brake != null)
            brakeLocked = brake.localRotation;
    }

    // Called by SteampunkLevel when Gear 2 starts turning
    public void Release()
    {
        released = true;
    }

    void Update()
    {
        if (released && !IsRunning)
        {
            // Real time: the player usually stands still to watch this, which would otherwise freeze the moment
            timer += Time.deltaTime;
            if (brake != null)
            {
                float t = Mathf.Clamp01((timer - releaseDelay + brakeTime) / brakeTime);
                brake.localRotation = brakeLocked * Quaternion.Euler(brakeReleasedEuler * t);
            }
            if (timer >= releaseDelay)
            {
                IsRunning = true;
                if (gear3 != null && gear2 != null)
                {
                    // The rod jolts Gear 3 into step with Gear 2, then they turn together (same crank angle,
                    // so the rod keeps its length like a locomotive's coupling rod)
                    gear3.transform.localRotation = gear2.transform.localRotation;
                    gear3.follow = gear2;
                    gear3.ratio = 1f;
                    gear3.SetRunning(true);
                }
                if (releasePuff != null) releasePuff.Burst();
                if (SteampunkLevel.Instance != null) SteampunkLevel.Instance.Gear3Started();
            }
        }
    }

    void LateUpdate()
    {
        if (rod == null || pin2 == null || pin3 == null)
            return;
        Vector3 a = pin2.position, b = pin3.position;
        Vector3 d = b - a;
        if (d.sqrMagnitude < 0.0001f)
            return;
        rod.position = (a + b) * 0.5f;
        rod.rotation = Quaternion.LookRotation(Vector3.forward, d.normalized);
        Vector3 s = rod.localScale;
        s.y = d.magnitude;
        rod.localScale = s;
    }
}
