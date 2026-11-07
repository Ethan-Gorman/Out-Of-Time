using System.Collections.Generic;
using UnityEngine;

// Cuts a see-through circle around the player in any wall or prop that blocks the camera's view,
// so the camera never has to move. Sits on the camera.
// Each frame it sphere-casts from the camera to the player. Meshes that actually cross that line and use
// URP Lit are switched to "Out Of Time/Lit Cutout" (an exact copy of Lit with the cut added, so they look
// the same), and each one fades its cut in on its own. Only the part of a wall in front of the player,
// above their feet and near them on screen is cut, with a dithered pixel pattern. When a mesh stops
// blocking, its cut fades out and its original materials come back.
// No per-wall setup: use Layers to exclude things that should never be cut.
[RequireComponent(typeof(Camera))]
public class OccluderFader : MonoBehaviour
{
    [Tooltip("What to keep visible. Found by the Player tag if left empty")]
    public Transform target;

    [Tooltip("Aim this far above the target's feet (roughly the middle of the character)")]
    public float targetHeight = 0.6f;

    [Tooltip("Width of the line-of-sight check. Bigger cuts walls that are only near the line")]
    public float radius = 0.5f;

    [Header("Circle")]
    [Tooltip("Circle radius as a fraction of screen height")]
    [Range(0.02f, 0.5f)] public float circleRadius = 0.12f;
    [Tooltip("Width of the dithered soft edge, as a fraction of screen height")]
    [Range(0f, 0.2f)] public float circleSoftness = 0.04f;
    [Tooltip("How see-through the middle of the circle gets. 0.3 = 30% of the wall left")]
    [Range(0f, 1f)] public float fadedAlpha = 0.3f;
    [Tooltip("Seconds for the circle to open or close")]
    public float fadeTime = 0.15f;
    [Tooltip("Only cut surfaces at least this far above the player's feet, so floors, paths and " +
             "low steps in front of the player stay solid")]
    public float minCutHeight = 0.3f;
    [Tooltip("Height above Min Cut Height over which the cut builds to full strength. " +
             "Bigger = low objects like crates are cut more lightly")]
    public float cutHeightRamp = 1f;

    [Tooltip("Only these layers can be cut")]
    public LayerMask layers = ~0;

    [Tooltip("The Out Of Time/Lit Cutout shader")]
    public Shader cutoutShader;

    [Tooltip("A material using Lit Cutout, only here so builds include the shader. Don't remove")]
    public Material variantKeeper;

    static readonly int CenterId = Shader.PropertyToID("_OOT_CutoutCenter");
    static readonly int ShapeId = Shader.PropertyToID("_OOT_CutoutShape");
    static readonly int PlayerId = Shader.PropertyToID("_OOT_CutoutPlayer");
    static readonly int StrengthId = Shader.PropertyToID("_OOT_Strength");
    const string LitShaderName = "Universal Render Pipeline/Lit";

    class Swapped
    {
        public Material[] originals;
        public Material[] cutouts;
        public float fade;
        public bool blocking;
    }

    readonly Dictionary<Renderer, Swapped> swapped = new Dictionary<Renderer, Swapped>();
    readonly List<Renderer> finished = new List<Renderer>();
    readonly RaycastHit[] hits = new RaycastHit[32];
    Camera cam;
    float spriteStrength;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cutoutShader == null)
            cutoutShader = Shader.Find("Out Of Time/Lit Cutout");
    }

    void LateUpdate()
    {
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        foreach (var s in swapped.Values)
            s.blocking = false;

        bool anyBlocking = false;
        if (target != null)
        {
            // Everything between the camera and the player
            Vector3 from = transform.position;
            Vector3 to = target.position + Vector3.up * targetHeight;
            Vector3 dir = to - from;
            float length = dir.magnitude;
            float distance = length - radius;
            if (distance > 0f)
            {
                var ray = new Ray(from, dir / length);
                int count = Physics.SphereCastNonAlloc(ray, radius, hits, distance, layers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    anyBlocking |= MarkBlocking(hits[i].collider, ray, length);
            }

            // Where the player is, for the shader
            Vector3 viewport = cam.WorldToViewportPoint(to);
            Vector3 feet = target.position;
            Shader.SetGlobalVector(ShapeId, new Vector4(circleRadius, circleSoftness, feet.y + minCutHeight, cutHeightRamp));
            Shader.SetGlobalVector(PlayerId, new Vector4(feet.x, feet.y, feet.z, 1f - fadedAlpha));
            Shader.SetGlobalVector(CenterId, new Vector4(viewport.x, viewport.y, 0f, 0f));
        }

        // Each swapped mesh fades its own cut in or out. Real time, so it works while the world is frozen
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);
        foreach (var pair in swapped)
        {
            var s = pair.Value;
            s.fade = Mathf.MoveTowards(s.fade, s.blocking ? 1f : 0f, step);
            foreach (var m in s.cutouts)
                if (m != null && m.shader == cutoutShader)
                    m.SetFloat(StrengthId, s.fade);
            if (!s.blocking && s.fade <= 0f)
                finished.Add(pair.Key);
        }
        foreach (var r in finished)
            Restore(r);
        finished.Clear();

        // Tile sprites (e.g. the border's wall tops) can't be swapped, so they follow a shared strength
        spriteStrength = Mathf.MoveTowards(spriteStrength, anyBlocking ? 1f : 0f, step);
        Vector4 centre = Shader.GetGlobalVector(CenterId);
        centre.w = spriteStrength * (1f - fadedAlpha);
        Shader.SetGlobalVector(CenterId, centre);
    }

    // Returns true if this collider has a mesh that blocks the line of sight and can be cut
    bool MarkBlocking(Collider col, Ray ray, float length)
    {
        // Never cut the player or anything attached to it
        if (col.transform.IsChildOf(target) || cutoutShader == null)
            return false;

        bool any = false;
        foreach (var r in col.GetComponentsInChildren<MeshRenderer>())
        {
            if (!r.enabled)
                continue;

            // Only meshes that actually cross the line, not every child of the hit object
            // (a building's back wall shouldn't be cut because its front wall was hit)
            if (r.gameObject != col.gameObject)
            {
                Bounds b = r.bounds;
                b.Expand(radius * 2f);
                if (!b.IntersectRay(ray, out float enter) || enter > length)
                    continue;
            }

            if (!swapped.TryGetValue(r, out var s))
            {
                s = Swap(r);
                if (s == null)
                    continue;
                swapped.Add(r, s);
            }
            s.blocking = true;
            any = true;
        }
        return any;
    }

    // Copies each URP Lit material onto the cutout shader. Same properties and keywords, so it looks identical
    Swapped Swap(Renderer r)
    {
        var originals = r.sharedMaterials;
        var cutouts = new Material[originals.Length];
        bool anyLit = false;
        for (int i = 0; i < originals.Length; i++)
        {
            var m = originals[i];
            if (m != null && m.shader != null && m.shader.name == LitShaderName)
            {
                cutouts[i] = new Material(m) { name = m.name + " (Cutout)", shader = cutoutShader };
                cutouts[i].SetFloat(StrengthId, 0f);
                anyLit = true;
            }
            else
            {
                cutouts[i] = m;
            }
        }

        if (!anyLit)
            return null;

        r.sharedMaterials = cutouts;
        return new Swapped { originals = originals, cutouts = cutouts };
    }

    void Restore(Renderer r)
    {
        var s = swapped[r];
        if (r != null)
            r.sharedMaterials = s.originals;
        for (int i = 0; i < s.cutouts.Length; i++)
            if (s.cutouts[i] != null && s.cutouts[i] != s.originals[i])
                Destroy(s.cutouts[i]);
        swapped.Remove(r);
    }

    void OnDisable()
    {
        foreach (var r in new List<Renderer>(swapped.Keys))
            Restore(r);
        spriteStrength = 0f;
        Shader.SetGlobalVector(CenterId, Vector4.zero);
    }
}
