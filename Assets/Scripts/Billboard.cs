using UnityEngine;

// Keeps a sprite facing the camera so flat 2D art reads correctly in the 3D world.
public class Billboard : MonoBehaviour
{
    [Tooltip("Only rotate around Y so the sprite stays upright. Off = tilt to fully face the camera.")]
    public bool lockUpright = false;

    Transform cam;

    void Start()
    {
        if (Camera.main != null)
            cam = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (cam == null)
            return;

        if (lockUpright)
        {
            Vector3 forward = cam.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(forward);
        }
        else
        {
            transform.rotation = cam.rotation;
        }
    }
}
