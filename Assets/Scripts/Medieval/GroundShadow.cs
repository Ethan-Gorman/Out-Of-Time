using UnityEngine;

// Keeps a flat shadow sprite on the ground under its parent, even mid-jump.
// In a 2.5D view this is what makes height readable. It shrinks and fades as the parent rises.
// Put it on a child object with a SpriteRenderer, rotated to lie flat (X = 90).
public class GroundShadow : MonoBehaviour
{
    [Tooltip("How far down to look for ground.")]
    public float maxDistance = 10f;

    [Tooltip("At this height above the ground the shadow is at its smallest and faintest.")]
    public float fadeHeight = 3f;

    [Range(0f, 1f)] public float minScale = 0.5f;
    [Range(0f, 1f)] public float minAlpha = 0.3f;

    // Lift above the ground so the shadow doesn't flicker into it
    public float groundOffset = 0.02f;

    Transform parent;
    SpriteRenderer sprite;
    Vector3 baseScale;
    float baseAlpha;

    void Awake()
    {
        parent = transform.parent;
        sprite = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        if (sprite != null)
            baseAlpha = sprite.color.a;
    }

    void LateUpdate()
    {
        if (parent == null)
            return;

        // Start a little above the parent's feet so standing on the ground still hits
        Vector3 origin = parent.position + Vector3.up * 0.1f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (sprite != null) sprite.enabled = false;
            return;
        }

        if (sprite != null) sprite.enabled = true;
        transform.position = hit.point + Vector3.up * groundOffset;

        // 0 on the ground, 1 at fadeHeight or higher
        float t = Mathf.Clamp01((parent.position.y - hit.point.y) / fadeHeight);
        transform.localScale = baseScale * Mathf.Lerp(1f, minScale, t);
        if (sprite != null)
        {
            Color c = sprite.color;
            c.a = baseAlpha * Mathf.Lerp(1f, minAlpha, t);
            sprite.color = c;
        }
    }
}
