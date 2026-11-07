using System.Collections.Generic;
using UnityEngine;

// Cuts a see-through oval around the player in anything that could hide them from the camera,
// so the camera never has to move. Sits on the camera.
//
// The decision is made in ONE place, CanObscure(): the camera's sight lines to the player's head (left,
// centre and right) must ALL be blocked, i.e. the player is completely hidden; then whatever blocks them is cut.
// If any part of the head is still visible (a wall only covering one side, a divider beside the player,
// a crate or half wall in front, the floor), nothing is cut.
//
// Cut objects using URP Lit are switched to "Out Of Time/Lit Cutout" (an exact copy of Lit with the hole,
// so they look the same); objects already on Lit Cutout or the greyscale tile shader are cut in place.
// Each one fades its hole in and out on its own, through a MaterialPropertyBlock, and gets a faint grey
// "Cutout Tint" wash over the hole, plus a solid dark "Cutout Cap" on its footprint where the front is cut away. Renderers listed in an OccluderLink (e.g. a wall's tile top) are cut
// together with their object.
[RequireComponent(typeof(Camera))]
public class OccluderFader : MonoBehaviour
{
    [Tooltip("What to keep visible. Found by the Player tag if left empty")]
    public Transform target;

    [Tooltip("Centre of the hole, this far above the target's feet (roughly the middle of the character)")]
    public float targetHeight = 0.6f;

    [Tooltip("Head-level sight lines are aimed this fraction of the sprite's height below its top")]
    [Range(0f, 0.5f)] public float headInset = 0.15f;

    [Tooltip("Head-level sight lines to the left and right, as a fraction of the sprite's half width. " +
             "All of them must be blocked before anything is cut, so bigger = only cut when the player is more fully hidden")]
    [Range(0f, 1f)] public float headSpread = 0.6f;

    [Header("Hole")]
    [Tooltip("Hole radius (half its height) as a fraction of screen height")]
    [Range(0.02f, 0.5f)] public float circleRadius = 0.22f;
    [Tooltip("Width of the dithered soft edge, as a fraction of screen height")]
    [Range(0f, 0.2f)] public float circleSoftness = 0.09f;
    [Tooltip("How many times wider than tall the hole is. 1 = circle, bigger = wide oval")]
    [Range(1f, 3f)] public float circleWidth = 1.3f;
    [Tooltip("How much wall is left in the middle of the hole. 0 = fully clear, only the edge is dithered")]
    [Range(0f, 1f)] public float fadedAlpha = 0f;
    [Tooltip("Faint wash over the hole (alpha = strength in the middle). No dither")]
    public Color tintColor = new Color(0.5f, 0.5f, 0.5f, 0.05f);
    [Tooltip("Solid colour of a cut wall's footprint on the ground, seen through the hole")]
    public Color capColor = new Color(0.2f, 0.2f, 0.23f, 1f);
    [Tooltip("Seconds for the hole to open or close")]
    public float fadeTime = 0.15f;
    [Tooltip("Never cut below this height above the player's feet (the foot of a wall stays solid)")]
    public float minCutHeight = 0.3f;
    [Tooltip("Height above Min Cut Height over which the hole builds to full strength")]
    public float cutHeightRamp = 0.3f;

    [Tooltip("Only these layers can be cut")]
    public LayerMask layers = ~0;

    [Header("Shaders (assigned in the prefab so builds include them)")]
    public Shader cutoutShader;
    public Shader tintShader;
    public Shader capShader;
    [Tooltip("Materials using the shaders above, only here so builds include them. Don't remove")]
    public Material variantKeeper;
    public Material tintKeeper;
    public Material capKeeper;

    static readonly int CenterId = Shader.PropertyToID("_OOT_CutoutCenter");
    static readonly int ShapeId = Shader.PropertyToID("_OOT_CutoutShape");
    static readonly int PlayerId = Shader.PropertyToID("_OOT_CutoutPlayer");
    static readonly int StrengthId = Shader.PropertyToID("_OOT_Strength");
    static readonly int PlayerEyeDepthId = Shader.PropertyToID("_OOT_PlayerEyeDepth");
    static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    static readonly int CapColorId = Shader.PropertyToID("_CapColor");
    const string LitShaderName = "Universal Render Pipeline/Lit";
    const string GreyscaleShaderName = "Out Of Time/Greyscale Sprite";

    class Cut
    {
        public Material[] originals;
        public Material[] current;
        public float fade;
        public bool blocking;
    }

    readonly Dictionary<Renderer, Cut> cuts = new Dictionary<Renderer, Cut>();
    readonly HashSet<Renderer> occludersThisFrame = new HashSet<Renderer>();
    readonly List<Renderer> finished = new List<Renderer>();
    readonly RaycastHit[] hits = new RaycastHit[32];
    readonly HashSet<Collider> blockingColliders = new HashSet<Collider>();
    bool playerFullyHidden;
    MaterialPropertyBlock block;
    Material tintMaterial;
    Material capMaterial;
    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        block = new MaterialPropertyBlock();
        if (cutoutShader == null)
            cutoutShader = Shader.Find("Out Of Time/Lit Cutout");
        if (tintShader == null)
            tintShader = Shader.Find("Out Of Time/Cutout Tint");
        if (tintShader != null)
            tintMaterial = new Material(tintShader) { name = "Cutout Tint (runtime)" };
        if (capShader == null)
            capShader = Shader.Find("Out Of Time/Cutout Cap");
        if (capShader != null)
            capMaterial = new Material(capShader) { name = "Cutout Cap (runtime)" };
    }

    void OnDestroy()
    {
        if (tintMaterial != null)
            Destroy(tintMaterial);
        if (capMaterial != null)
            Destroy(capMaterial);
    }

    void LateUpdate()
    {
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        foreach (var c in cuts.Values)
            c.blocking = false;

        if (target != null)
        {
            FindOccluders();
            SetShaderGlobals();
        }

        if (tintMaterial != null)
            tintMaterial.SetColor(TintColorId, tintColor);
        if (capMaterial != null)
            capMaterial.SetColor(CapColorId, capColor);

        // Each cut renderer fades its own hole in or out. Real time, so it works while the world is frozen
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);
        foreach (var pair in cuts)
        {
            var c = pair.Value;
            c.fade = Mathf.MoveTowards(c.fade, c.blocking ? 1f : 0f, step);
            if (pair.Key != null)
                SetStrength(pair.Key, c.fade);
            if (!c.blocking && c.fade <= 0f)
                finished.Add(pair.Key);
        }
        foreach (var r in finished)
            Restore(r);
        finished.Clear();

        // How open the most-open hole is, for the outline pass that redraws cut walls' edges inside the hole
        float open = 0f;
        foreach (var c in cuts.Values)
            open = Mathf.Max(open, c.fade);
        Vector4 centre = Shader.GetGlobalVector(CenterId);
        centre.w = open;
        Shader.SetGlobalVector(CenterId, centre);
    }

    // ---------------------------------------------------------------- the one rule

    // True if this collider is hiding the player: every sight line to the player's head is blocked
    // (so no part of them is visible) and this collider is one of the things blocking them
    bool CanObscure(Collider col) => playerFullyHidden && blockingColliders.Contains(col);

    // Casts the camera's sight lines to the player's head (left, centre and right). Records everything
    // they hit, and whether ALL of them were blocked
    void CastHeadSightLines()
    {
        blockingColliders.Clear();
        playerFullyHidden = false;
        if (!TryGetHead(out Vector3 head, out Vector3 side))
            return;

        Vector3 from = transform.position;
        bool allBlocked = true;
        foreach (var point in new[] { head - side, head, head + side })
        {
            Vector3 dir = point - from;
            int count = Physics.RaycastNonAlloc(from, dir.normalized, hits, dir.magnitude, layers, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(target))
                    continue;
                blockingColliders.Add(hits[i].collider);
                blocked = true;
            }
            allBlocked &= blocked;
        }
        playerFullyHidden = allBlocked;
    }

    // ---------------------------------------------------------------- finding blockers

    void FindOccluders()
    {
        occludersThisFrame.Clear();
        CastHeadSightLines();

        foreach (var col in blockingColliders)
        {
            if (!CanObscure(col))
                continue;

            foreach (var r in col.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is SpriteRenderer || !occludersThisFrame.Add(r))
                    continue;
                MarkCut(r);

                // Linked renderers (e.g. the tile top on a wall block) go with their object
                var link = r.GetComponent<OccluderLink>();
                if (link != null)
                    foreach (var extra in link.alsoCut)
                        if (extra != null)
                            MarkCut(extra);
            }
        }
    }

    void MarkCut(Renderer r)
    {
        if (!cuts.TryGetValue(r, out var c))
        {
            c = StartCut(r);
            if (c == null)
                return;
            cuts.Add(r, c);
        }
        c.blocking = true;
    }

    // A point just below the top of the player's sprite, plus a sideways offset for the left/right sight lines.
    // Uses the sprite's own transform, so it follows the Billboard's tilt. Falls back to the colliders
    bool TryGetHead(out Vector3 head, out Vector3 side)
    {
        var sr = FindPlayerSprite();
        if (sr != null && sr.sprite != null)
        {
            Bounds local = sr.sprite.bounds;
            float y = local.max.y - local.size.y * headInset;
            head = sr.transform.TransformPoint(new Vector3(local.center.x, y, 0f));
            side = sr.transform.TransformVector(new Vector3(local.extents.x * headSpread, 0f, 0f));
            return true;
        }

        foreach (var col in target.GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger)
                continue;
            Bounds b = col.bounds;
            head = new Vector3(b.center.x, b.max.y - b.size.y * headInset, b.center.z);
            side = transform.right * (b.extents.x * headSpread);
            return true;
        }

        head = side = default;
        return false;
    }

    // The player's character sprite: the one on the Billboard. Read even while it is switched off
    // (PlayerDamage blinks it after a hit), so the sight lines don't jump around during the blink.
    // Cached, and found again if the target changes
    SpriteRenderer playerSprite;
    Transform playerSpriteOwner;

    SpriteRenderer FindPlayerSprite()
    {
        if (playerSprite != null && playerSpriteOwner == target)
            return playerSprite;

        playerSprite = null;
        playerSpriteOwner = target;
        foreach (var candidate in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (candidate.GetComponent<Billboard>() != null)
                return playerSprite = candidate;
            // Otherwise the first sprite that isn't a ground shadow
            if (playerSprite == null && candidate.GetComponent<GroundShadow>() == null)
                playerSprite = candidate;
        }
        return playerSprite;
    }

    // ---------------------------------------------------------------- shader state

    void SetShaderGlobals()
    {
        Vector3 viewport = cam.WorldToViewportPoint(target.position + Vector3.up * targetHeight);
        Vector3 feet = target.position;
        Shader.SetGlobalVector(CenterId, new Vector4(viewport.x, viewport.y, circleWidth, 0f));
        Shader.SetGlobalVector(ShapeId, new Vector4(circleRadius, circleSoftness, feet.y + minCutHeight, cutHeightRamp));
        Shader.SetGlobalVector(PlayerId, new Vector4(feet.x, feet.y, feet.z, 1f - fadedAlpha));
        Shader.SetGlobalFloat(PlayerEyeDepthId, viewport.z);
    }

    void SetStrength(Renderer r, float strength)
    {
        r.GetPropertyBlock(block);
        block.SetFloat(StrengthId, strength);
        r.SetPropertyBlock(block);
    }

    // Puts a renderer on cuttable materials (if it isn't already) and adds the tint wash.
    // Returns null if nothing on it can be cut
    Cut StartCut(Renderer r)
    {
        var originals = r.sharedMaterials;
        var current = new List<Material>(originals.Length + 1);
        bool cuttable = false;
        foreach (var m in originals)
        {
            if (m != null && m.shader == cutoutShader)
            {
                current.Add(m);
                cuttable = true;
            }
            else if (m != null && m.shader != null && m.shader.name == LitShaderName && cutoutShader != null)
            {
                current.Add(new Material(m) { name = m.name + " (Cutout)", shader = cutoutShader });
                cuttable = true;
            }
            else
            {
                if (m != null && m.shader != null && m.shader.name == GreyscaleShaderName)
                    cuttable = true;
                current.Add(m);
            }
        }

        if (!cuttable)
            return null;

        // Footprint cap and tint wash on 3D meshes only (tile tops are already greyscale)
        if (r is MeshRenderer)
        {
            if (capMaterial != null)
                current.Add(capMaterial);
            if (tintMaterial != null)
                current.Add(tintMaterial);
        }

        r.sharedMaterials = current.ToArray();
        SetStrength(r, 0f);
        return new Cut { originals = originals, current = current.ToArray() };
    }

    void Restore(Renderer r)
    {
        var c = cuts[r];
        if (r != null)
        {
            r.sharedMaterials = c.originals;
            SetStrength(r, 0f);
        }
        foreach (var m in c.current)
            if (m != null && m != tintMaterial && m != capMaterial && System.Array.IndexOf(c.originals, m) < 0)
                Destroy(m);
        cuts.Remove(r);
    }

    void OnDisable()
    {
        foreach (var r in new List<Renderer>(cuts.Keys))
            Restore(r);
    }
}
