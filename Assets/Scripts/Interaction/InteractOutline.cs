using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// One-pixel outline, drawn on top like an edge detection pass, on everything the player can use, pick up or fight:
//  - Usable: gold. The one Interact would use right now: bright (only ever one)
//  - Used but still in the world (pulled lever, crafted axe stone): grey
//  - Enemies (boar, skeletons, clockwork guards): red until they die
//  - Crystals (magnet stones, the Time Crystal): cyan
//  - Villagers only while they are the selected one; things that break or die lose it once used
// Added automatically to every such object when a scene loads, so nothing needs setting up per prefab.
// How it draws: each frame the outlined renderers are drawn flat into a mask at the low render resolution
// (InteractOutlineMask), then a full-screen pass (InteractOutlineComposite) lights every pixel just outside a
// silhouette. The objects' own renderers and materials are never touched.
public class InteractOutline : MonoBehaviour
{
    static readonly Color Usable = new Color(0.95f, 0.78f, 0.36f, 0.85f);
    static readonly Color Selected = new Color(1f, 0.97f, 0.8f, 1f);
    static readonly Color Used = new Color(0.55f, 0.55f, 0.6f, 0.7f);
    static readonly Color Enemy = new Color(0.95f, 0.2f, 0.15f, 0.9f);
    static readonly Color Crystal = new Color(0.27f, 0.85f, 0.84f, 0.9f);

    const float FadeSpeed = 8f;

    static readonly List<InteractOutline> active = new List<InteractOutline>();

    Interactable interactable;
    SkeletonEnemy skeleton;
    ClockworkGuard guard;
    Renderer[] renderers;

    // Faded so the outline never pops
    Color color;
    float shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        active.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddToAll();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AddToAll();
    }

    static void AddToAll()
    {
        const FindObjectsInactive all = FindObjectsInactive.Include;
        const FindObjectsSortMode any = FindObjectsSortMode.None;
        foreach (var c in Object.FindObjectsByType<Interactable>(all, any)) Add(c);
        foreach (var c in Object.FindObjectsByType<MagneticStone>(all, any)) Add(c);
        foreach (var c in Object.FindObjectsByType<TimeCrystal>(all, any)) Add(c);
        foreach (var c in Object.FindObjectsByType<SteampunkItem>(all, any)) Add(c);
        foreach (var c in Object.FindObjectsByType<PickupItem>(all, any)) Add(c);
        foreach (var c in Object.FindObjectsByType<SkeletonEnemy>(all, any)) Add(c);
        foreach (var c in Object.FindObjectsByType<ClockworkGuard>(all, any)) Add(c);
    }

    // Call for anything spawned after the scene loaded
    public static void Add(Component target)
    {
        if (target != null && target.GetComponent<InteractOutline>() == null)
            target.gameObject.AddComponent<InteractOutline>();
    }

    void Awake()
    {
        interactable = GetComponent<Interactable>();
        skeleton = GetComponent<SkeletonEnemy>();
        guard = GetComponent<ClockworkGuard>();
        CollectRenderers();
    }

    void OnEnable()
    {
        active.Add(this);
        OutlineDrawer.Ensure();
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    void CollectRenderers()
    {
        var list = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            // Shadow-only casters, ground shadows, particles, lines and text have no outline of their own
            if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
            if (IsGroundShadow(r)) continue;
            if (r is SpriteRenderer || r is MeshRenderer && r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null)
                list.Add(r);
        }
        renderers = list.ToArray();
    }

    // GroundShadow draws on its own object and on a "Shadow Mesh" child it creates
    bool IsGroundShadow(Renderer r)
    {
        var shadow = r.GetComponentInParent<GroundShadow>(true);
        return shadow != null && (shadow.transform != transform || r.transform == shadow.transform || r.name == "Shadow Mesh");
    }

    // Target colour and whether to show at all, from the object's state this frame
    bool Wanted(out Color target)
    {
        target = Usable;
        if (skeleton != null || guard != null)
        {
            target = Enemy;
            return skeleton != null ? !skeleton.IsDead : !guard.IsDead;
        }
        if (interactable == null)
        {
            // Pickups vanish when taken; crystals match their own cyan glow
            if (GetComponent<MagneticStone>() != null || GetComponent<TimeCrystal>() != null)
                target = Crystal;
            return true;
        }

        bool usable = interactable.CanInteract;
        var interactor = MedievalInteractor.Current;
        bool selected = usable && interactor != null && interactor.Selected == interactable;

        if (interactable.IsEnemy)
        {
            target = Enemy;
            return usable;
        }
        if (interactable.OutlineOnlyInRange && !selected)
            return false;
        if (!usable)
        {
            target = Used;
            return interactable.KeepUsedOutline;
        }
        target = selected ? Selected : Usable;
        return true;
    }

    // Called by OutlineDrawer once per frame, after everything has moved
    void Tick()
    {
        bool show = Wanted(out Color target);
        float step = Time.unscaledDeltaTime * FadeSpeed;
        shown = Mathf.MoveTowards(shown, show ? 1f : 0f, step);
        color = shown <= 0f ? target : Color.Lerp(color, target, Mathf.Clamp01(step * 2f));
    }

    // Hidden helper that owns the mask and the full-screen pass
    [DefaultExecutionOrder(10000)]
    class OutlineDrawer : MonoBehaviour
    {
        static OutlineDrawer instance;

        static readonly int MaskId = Shader.PropertyToID("_OOT_OutlineMask");
        static readonly int MaskSizeId = Shader.PropertyToID("_OOT_OutlineMaskSize");
        static readonly int MaskColorId = Shader.PropertyToID("_OOT_OutlineMaskColor");

        Material maskMaterial;
        Material compositeMaterial;
        Mesh triangle;
        RenderTexture mask;
        CommandBuffer cmd;

        public static void Ensure()
        {
            if (instance != null)
                return;
            var go = new GameObject("Interact Outline Drawer") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<OutlineDrawer>();
        }

        void Awake()
        {
            var maskShader = Resources.Load<Shader>("InteractOutlineMask");
            var compositeShader = Resources.Load<Shader>("InteractOutlineComposite");
            if (maskShader == null || compositeShader == null)
            {
                Debug.LogWarning("[InteractOutline] Outline shaders missing from Resources.");
                enabled = false;
                return;
            }
            maskMaterial = new Material(maskShader) { hideFlags = HideFlags.DontSave };
            compositeMaterial = new Material(compositeShader) { hideFlags = HideFlags.DontSave };
            cmd = new CommandBuffer { name = "Interact Outline Mask" };

            // One triangle that covers the screen (positions are clip space, see the composite shader)
            triangle = new Mesh { name = "Interact Outline Triangle", hideFlags = HideFlags.DontSave };
            triangle.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(3f, -1f, 0f), new Vector3(-1f, 3f, 0f) };
            triangle.triangles = new[] { 0, 1, 2 };
            triangle.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        }

        void OnDestroy()
        {
            if (mask != null) mask.Release();
            cmd?.Release();
            if (instance == this) instance = null;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            // Same size as URP's scaled render, so one mask pixel is one game pixel
            float scale = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp ? urp.renderScale : 1f;
            int width = Mathf.Max(1, (int)(cam.pixelWidth * scale));
            int height = Mathf.Max(1, (int)(cam.pixelHeight * scale));
            if (mask == null || mask.width != width || mask.height != height)
            {
                if (mask != null) mask.Release();
                mask = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Interact Outline Mask", filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave,
                };
                mask.Create();
            }

            bool any = false;
            cmd.Clear();
            cmd.SetRenderTarget(mask);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, GL.GetGPUProjectionMatrix(cam.projectionMatrix, true));
            foreach (var outline in active)
            {
                if (outline == null)
                    continue;
                outline.Tick();
                if (outline.shown <= 0f)
                    continue;

                Color c = outline.color;
                c.a *= outline.shown;
                cmd.SetGlobalColor(MaskColorId, c);
                foreach (var r in outline.renderers)
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                        continue;
                    int subMeshes = r is MeshRenderer && r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null ? f.sharedMesh.subMeshCount : 1;
                    for (int i = 0; i < subMeshes; i++)
                        cmd.DrawRenderer(r, maskMaterial, i, 0);
                    any = true;
                }
            }
            if (!any)
                return;

            Graphics.ExecuteCommandBuffer(cmd);
            Shader.SetGlobalTexture(MaskId, mask);
            Shader.SetGlobalVector(MaskSizeId, new Vector4(width, height, 1f / width, 1f / height));

            var rp = new RenderParams(compositeMaterial)
            {
                camera = cam,
                worldBounds = new Bounds(cam.transform.position, Vector3.one * 100000f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMesh(rp, triangle, 0, Matrix4x4.identity);
        }
    }
}
