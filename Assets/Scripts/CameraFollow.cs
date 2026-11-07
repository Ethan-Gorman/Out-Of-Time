using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Angled follow camera. Low field of view flattens perspective so the 3D world reads as 2D.
// The camera FOV itself is set on the Camera component (20 by default).
// Pixel snap moves the camera in whole low-res pixels, so pixel-art edges don't crawl as it moves.
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

    public bool pixelSnap = true;

    // Current camera speed, used internally by SmoothDamp between frames
    Vector3 velocity;

    // The smooth, unsnapped position. Snapping is applied on top each frame, so following stays fluid
    Vector3 smoothPosition;
    bool hasSmoothPosition;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

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
        if (!hasSmoothPosition)
        {
            smoothPosition = transform.position;
            hasSmoothPosition = true;
        }
        smoothPosition = Vector3.SmoothDamp(smoothPosition, desired, ref velocity, smoothTime);

        transform.rotation = rotation;
        transform.position = pixelSnap ? SnapToPixels(smoothPosition, rotation) : smoothPosition;
    }

    // Snaps any world point to the same pixel grid as the camera. Billboard sprites use this so characters
    // step in whole pixels together with the world, instead of jiggling a pixel against it
    public Vector3 SnapWorldPoint(Vector3 point)
    {
        return pixelSnap ? SnapToPixels(point, transform.rotation) : point;
    }

    // Rounds the position to the low-res pixel grid, measured in the camera's own right/up plane
    Vector3 SnapToPixels(Vector3 position, Quaternion rotation)
    {
        float pixel = WorldUnitsPerPixel();
        if (pixel <= 0f)
            return position;

        Vector3 local = Quaternion.Inverse(rotation) * position;
        local.x = Mathf.Round(local.x / pixel) * pixel;
        local.y = Mathf.Round(local.y / pixel) * pixel;
        return rotation * local;
    }

    // Size of one rendered pixel at the player's distance: view height / rendered pixel rows.
    // URP's Render Scale (0.25 here) makes the rendered image smaller than the screen
    float WorldUnitsPerPixel()
    {
        if (cam == null || cam.orthographic)
            return 0f;

        float renderScale = 1f;
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            renderScale = urp.renderScale;

        float rows = cam.pixelHeight * renderScale;
        if (rows < 1f)
            return 0f;

        float viewHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return viewHeight / rows;
    }
}
