using UnityEngine;

// Keeps a sprite facing the camera so flat 2D art reads correctly in the 3D world.
// Put it on the child object that holds the SpriteRenderer, not on the parent with the Rigidbody.
public class Billboard : MonoBehaviour
{
    [Tooltip("Only rotate around Y so the sprite stays upright. Off = tilt to fully face the camera.")]
    public bool lockUpright = false;

    // Cached so we don't look up Camera.main every frame
    Transform cam;

    void Start()
    {
        if (Camera.main != null)
            cam = Camera.main.transform;
    }

    // LateUpdate runs after the camera has moved this frame (CameraFollow also uses LateUpdate),
    // so the sprite never lags a frame behind
    void LateUpdate()
    {
        if (cam == null)
            return;

        if (lockUpright)
        {
            // Flatten the camera's forward onto the ground so the sprite only turns left/right
            Vector3 forward = cam.forward;
            forward.y = 0f;

            // Guard against a camera looking straight down, where the flattened vector is zero
            if (forward.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(forward);
        }
        else
        {
            // Copy the camera's rotation exactly, so the sprite is always flat-on to the screen
            // and shows no perspective distortion
            transform.rotation = cam.rotation;
        }
    }
}
