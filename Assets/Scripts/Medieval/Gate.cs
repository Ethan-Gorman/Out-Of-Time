using UnityEngine;
using UnityEngine.Events;

// A gate that opens when Open() is called (by a Lever, or any UnityEvent).
// Moves in world time, so it slows down when the player stands still.
// The gate itself slides by Open Offset (a portcullis). Extra Parts move or turn with it, so any art works:
// door leaves that swing on their hinges, a bar that lifts, arch pieces that slide apart.
// Parts that are children of the gate also ride along with its slide, so set Open Offset to zero for swinging doors.
// Give each moving piece its own collider, or list fixed blockers under Disable When Open.
// Every gate works on its own, so a level can have as many as it likes.
public class Gate : MonoBehaviour
{
    [System.Serializable]
    public class Part
    {
        public Transform part;
        // Local offset and rotation (degrees) when fully open, added to where the part starts.
        // A part turns around its own pivot, so a swinging door leaf needs its pivot on the hinge edge
        // (parent the mesh under an empty placed at the hinge)
        public Vector3 openOffset;
        public Vector3 openRotation;
    }

    public Vector3 openOffset = new Vector3(0f, -2.6f, 0f);

    public float openTime = 1.2f;

    public Part[] parts;

    // Colliders switched off once the gate is fully open (e.g. a blocker that doesn't move with the art)
    public Collider[] disableWhenOpen;

    // On: the gate begins open (handy while testing a layout)
    public bool startOpen = false;

    // Optional: played when the gate starts to open
    public AudioSource openSound;

    public UnityEvent onOpening;
    public UnityEvent onFullyOpen;

    public bool IsOpen { get; private set; }
    public bool IsFullyOpen => progress >= 1f;

    Vector3 closedPosition;
    Vector3[] partPositions;
    Quaternion[] partRotations;
    float progress;

    void Awake()
    {
        closedPosition = transform.localPosition;

        int count = parts != null ? parts.Length : 0;
        partPositions = new Vector3[count];
        partRotations = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            if (parts[i] == null || parts[i].part == null)
                continue;
            partPositions[i] = parts[i].part.localPosition;
            partRotations[i] = parts[i].part.localRotation;
        }

        if (startOpen)
        {
            IsOpen = true;
            progress = 1f;
            Apply(1f);
            SetBlockers(false);
        }
    }

    public void Open()
    {
        if (IsOpen)
            return;
        IsOpen = true;
        if (openSound != null)
            openSound.Play();
        onOpening?.Invoke();
    }

    void Update()
    {
        if (!IsOpen || progress >= 1f)
            return;

        progress = Mathf.Min(1f, progress + WorldTime.Delta / Mathf.Max(0.01f, openTime));
        Apply(Mathf.SmoothStep(0f, 1f, progress));

        if (progress >= 1f)
        {
            SetBlockers(false);
            onFullyOpen?.Invoke();
        }
    }

    void Apply(float t)
    {
        transform.localPosition = closedPosition + openOffset * t;

        if (parts == null)
            return;
        for (int i = 0; i < parts.Length && i < partPositions.Length; i++)
        {
            var p = parts[i];
            if (p == null || p.part == null)
                continue;
            p.part.localPosition = partPositions[i] + p.openOffset * t;
            p.part.localRotation = partRotations[i] * Quaternion.Euler(p.openRotation * t);
        }
    }

    void SetBlockers(bool on)
    {
        if (disableWhenOpen == null)
            return;
        foreach (var c in disableWhenOpen)
            if (c != null)
                c.enabled = on;
    }
}
