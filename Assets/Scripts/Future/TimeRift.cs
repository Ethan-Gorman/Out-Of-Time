using System.Collections;
using UnityEngine;

// A tear in time on the floor: touching it costs a few seconds (its Hazard). When the Chrono Core comes online
// FutureLevel closes every rift: a burst of sparks, then it's gone.
public class TimeRift : MonoBehaviour
{
    public Hazard hazard;
    public SteamEmitter closeBurst;

    // Real seconds before it disappears after closing (the world may be frozen while the player reads)
    public float closeDelay = 0.4f;

    public bool IsClosed { get; private set; }

    public void Close()
    {
        if (IsClosed || !gameObject.activeInHierarchy)
            return;
        IsClosed = true;
        if (hazard != null)
            hazard.armed = false;
        if (closeBurst != null)
        {
            // Leaves the sparks behind, so they don't vanish with the rift
            closeBurst.transform.SetParent(null, true);
            closeBurst.Burst();
            Destroy(closeBurst.gameObject, 4f);
        }
        StartCoroutine(Hide());
    }

    IEnumerator Hide()
    {
        yield return new WaitForSecondsRealtime(closeDelay);
        gameObject.SetActive(false);
    }
}
