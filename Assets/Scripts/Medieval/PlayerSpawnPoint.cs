using UnityEngine;

// Where the player starts the level. Moves the player (tagged "Player") here as the scene loads, and
// puts the follow camera straight onto them so the level doesn't open with the camera sliding over.
// Place it beside the time machine; the player prefab's own position then doesn't matter.
// Runs before other scripts, so nothing sees the player at their old spot (e.g. a StoryTrigger on the first step).
[DefaultExecutionOrder(-100)]
public class PlayerSpawnPoint : MonoBehaviour
{
    // Height above this point the player's feet start at, so they drop onto the floor instead of into it
    public float lift = 0.1f;

    public bool alignCamera = true;

    void Awake()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        Vector3 at = transform.position + Vector3.up * lift;
        player.transform.position = at;
        if (player.TryGetComponent(out Rigidbody body))
        {
            body.position = at;
            if (!body.isKinematic)
                body.linearVelocity = Vector3.zero;
        }
        Physics.SyncTransforms();

        if (alignCamera)
            AlignCamera(player.transform);
    }

    static void AlignCamera(Transform target)
    {
        var cam = Camera.main;
        var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (follow == null)
        {
            var all = FindObjectsByType<CameraFollow>(FindObjectsSortMode.None);
            if (all.Length == 0)
                return;
            follow = all[0];
        }

        // Same framing CameraFollow eases towards, so its first frame starts there
        Quaternion rotation = Quaternion.Euler(follow.pitch, 0f, 0f);
        follow.transform.rotation = rotation;
        follow.transform.position = target.position - rotation * Vector3.forward * follow.distance;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.75f, 0.4f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.forward * 0.8f);
    }
}
