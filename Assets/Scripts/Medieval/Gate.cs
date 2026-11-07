using UnityEngine;

// A gate that slides out of the way when Open() is called (by a Lever).
// Moves in world time, so it slows down when the player stands still.
public class Gate : MonoBehaviour
{
    public Vector3 openOffset = new Vector3(0f, -2.6f, 0f);

    public float openTime = 1.2f;

    public bool IsOpen { get; private set; }

    Vector3 closedPosition;
    float progress;

    void Awake()
    {
        closedPosition = transform.localPosition;
    }

    public void Open()
    {
        IsOpen = true;
    }

    void Update()
    {
        if (!IsOpen || progress >= 1f)
            return;

        progress = Mathf.Min(1f, progress + WorldTime.Delta / Mathf.Max(0.01f, openTime));
        transform.localPosition = closedPosition + openOffset * Mathf.SmoothStep(0f, 1f, progress);
    }
}
