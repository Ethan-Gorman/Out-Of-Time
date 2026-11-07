using UnityEngine;

// Angled follow camera. Low field of view flattens perspective so the 3D world reads as 2D.
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    [Range(10f, 80f)] public float pitch = 35f;
    public float distance = 40f;
    public float smoothTime = 0.15f;

    Vector3 velocity;

    void LateUpdate()
    {
        if (target == null)
            return;

        Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 desired = target.position - rotation * Vector3.forward * distance;

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        transform.rotation = rotation;
    }
}
