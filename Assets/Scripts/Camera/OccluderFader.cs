using System.Collections.Generic;
using UnityEngine;

// Cuts a see-through circle around the player in any wall or prop that blocks the camera's view,
// so the camera never has to move. Sits on the camera.
// Each frame it sphere-casts from the camera to the player. Blocking renderers using URP Lit are
// switched to "Out Of Time/Lit Cutout" (an exact copy of Lit with the cut added, so they look the same),
// and the circle fades in. Only the part of a wall in front of the player and near them is cut, with a
// dithered pixel pattern. Once nothing blocks, the circle fades out and the original materials come back.
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

    [Tooltip("Only these layers can be cut")]
    public LayerMask layers = ~0;

    [Tooltip("The Out Of Time/Lit Cutout shader")]
    public Shader cutoutShader;

    [Tooltip("A material using Lit Cutout, only here so builds include the shader. Don't remove")]
    public Material variantKeeper;

    static readonly int CenterId = Shader.PropertyToID("_OOT_CutoutCenter");
    static readonly int ShapeId = Shader.PropertyToID("_OOT_CutoutShape");
    const string LitShaderName = "Universal Render Pipeline/Lit";

    class Swapped
    {
        public Material[] originals;
        public Material[] cutouts;
        public bool blocking;
    }

    readonly Dictionary<Renderer, Swapped> swapped = new Dictionary<Renderer, Swapped>();
    readonly List<Renderer> finished = new List<Renderer>();
    readonly RaycastHit[] hits = new RaycastHit[32];
    Camera cam;
    float strength;

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
            if (player == null)
                return;
            target = player.transform;
        }

        foreach (var s in swapped.Values)
            s.blocking = false;

        // Everything between the camera and the player
        Vector3 from = transform.position;
        Vector3 to = target.position + Vector3.up * targetHeight;
        Vector3 dir = to - from;
        float distance = dir.magnitude - radius;
        bool anyBlocking = false;
        if (distance > 0f)
        {
            int count = Physics.SphereCastNonAlloc(from, radius, dir / dir.magnitude, hits, distance, layers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                anyBlocking |= MarkBlocking(hits[i].collider);
        }

        // Open or close the circle. Real time, so it works while the world is frozen
        strength = Mathf.MoveTowards(strength, anyBlocking ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));

        // Where the player is on screen and how far away, for the shader
        Vector3 viewport = cam.WorldToViewportPoint(to);
        Shader.SetGlobalVector(CenterId, new Vector4(viewport.x, viewport.y, viewport.z, strength * (1f - fadedAlpha)));
        Shader.SetGlobalVector(ShapeId, new Vector4(circleRadius, circleSoftness, target.position.y + minCutHeight, 0f));

        // Once the circle has closed, give walls that no longer block their original materials back
        if (strength <= 0f)
        {
            foreach (var pair in swapped)
                if (!pair.Value.blocking)
                    finished.Add(pair.Key);
            foreach (var r in finished)
                Restore(r);
            finished.Clear();
        }
    }

    // Returns true if this collider has something that can be cut
    bool MarkBlocking(Collider col)
    {
        // Never cut the player or anything attached to it
        if (col.transform.IsChildOf(target) || cutoutShader == null)
            return false;

        bool any = false;
        foreach (var r in col.GetComponentsInChildren<Renderer>())
        {
            // Sprites and tilemaps are already see-through around their shapes; only cut 3D meshes
            if (!(r is MeshRenderer) || !r.enabled)
                continue;

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

    // Copies each URP Lit material onto the cutout shader. Same properties, so it looks identical
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
        strength = 0f;
        Shader.SetGlobalVector(CenterId, Vector4.zero);
    }
}
