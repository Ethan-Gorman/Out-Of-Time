using UnityEngine;

// Angled follow camera. Low field of view flattens perspective so the 3D world reads as 2D.
// The camera FOV itself is set on the Camera component (20 by default).
public class CameraFollow : MonoBehaviour
{
    // What to follow, normally the Player
    public Transform target;

    // How far the camera tilts down, in degrees. 90 = straight down, lower = more side-on
    [Range(10f, 80f)] public float pitch = 35f;

    // How far back the camera sits. Paired with a low FOV, more distance = flatter look
    public float distance = 40f;

    // Roughly how long the camera takes to catch up, in seconds. 0 = locked to the target
    public float smoothTime = 0.15f;

    // Current camera speed, used internally by SmoothDamp between frames
    Vector3 velocity;

    // LateUpdate runs after the player has moved this frame, so the camera doesn't jitter
    void LateUpdate()
    {
        if (target == null)
            return;

        // Tilt down by pitch, facing along +Z (the "up" direction for the player)
        Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);

        // Step back from the target along the camera's view direction
        Vector3 desired = target.position - rotation * Vector3.forward * distance;

        // Ease towards the desired spot instead of snapping, which smooths out movement
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        transform.rotation = rotation;
    }
}
