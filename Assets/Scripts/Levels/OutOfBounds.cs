using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Lays a big flat out-of-bounds ground under and around the whole level, so the camera never looks past
// the edge of the floor into the background. Also gives the camera a dark solid background colour,
// so any gap that is still left reads as darkness instead of blue sky.
// Sits on each era's Essentials prefab (configured per era). Like LevelBorder, nothing it builds is
// saved into the scene: it rebuilds on load, and in the editor when its settings change.
//
// The ground is purely visual: no collider (so it never blocks the player, physics, or the
// OccluderFader's sight lines, which are physics raycasts), no shadow casting or receiving, no probes.
[ExecuteAlways]
public class OutOfBounds : MonoBehaviour
{
    [Header("Ground")]
    // An existing era floor material. "Out Of Time/Lit Cutout" materials with World Space UVs on map
    // the texture in world space themselves; for the rest the mesh's own UVs are in world units
    public Material material;

    // Multiplied into the material's colour, so the out-of-bounds ground reads a little darker than the floor
    public Color tint = new Color(0.6f, 0.6f, 0.6f, 1f);

    // World height of the ground. Keep it a little below the lowest playable floor, so it never z-fights
    public float height = -0.05f;

    // How far past the level's edges the ground reaches, in world units
    public float margin = 150f;

    // Texture repeats per world unit for materials without World Space UVs (multiplied by the material's Tiling)
    public float uvScale = 1f;

    public bool receiveShadows = false;

    [Header("Hole (optional)")]
    // On: the ground covers everything outside a rounded rectangle and sits at Hole Height instead, i.e. on top
    // of any floor that pokes out past the level's walls. Put the hole's edge under the walls (Caveman: the
    // mountain ring's centre line), so only the outside gets covered
    public bool cutHole = false;
    public Vector2 holeCenter = Vector2.zero;
    public Vector2 holeHalfSize = new Vector2(29f, 14.6f);
    // Superellipse exponent: 1 = diamond, small (0.25) = rectangle with rounded corners (same as CavemanNature)
    [Range(0.05f, 1f)] public float holeRoundness = 0.25f;
    public float holeHeight = 0.02f;
    const int HoleSteps = 64;

    [Header("Camera")]
    // In play mode, switch the level camera to a solid colour background in this colour
    public bool setCameraBackground = true;
    public Color background = new Color(0.08f, 0.07f, 0.06f, 1f);

    const string RootName = "OutOfBounds (generated)";
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    GameObject root;
    Mesh mesh;
    Material instance;
#if UNITY_EDITOR
    bool rebuildQueued;
#endif

    void OnEnable()
    {
        Build();
        ApplyBackground();
    }

    void OnDisable()
    {
        Clear();
    }

#if UNITY_EDITOR
    // Inspector edits: rebuild once Unity has finished applying them
    void OnValidate()
    {
        if (rebuildQueued)
            return;

        rebuildQueued = true;
        EditorApplication.delayCall += () =>
        {
            rebuildQueued = false;
            if (this != null && isActiveAndEnabled)
            {
                Build();
                ApplyBackground();
            }
        };
    }
#endif

    [ContextMenu("Rebuild")]
    public void Build()
    {
        Clear();
        if (material == null || !gameObject.scene.IsValid())
            return;

        Bounds b = LevelBounds();
        float minX = b.min.x - margin, maxX = b.max.x + margin;
        float minZ = b.min.z - margin, maxZ = b.max.z + margin;

        root = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        // Flat and facing up, built straight in world space: one quad, or a ring around the hole
        mesh = new Mesh { name = "OutOfBounds Ground", hideFlags = HideFlags.DontSave };
        Vector3[] vertices;
        int[] triangles;
        if (cutHole)
            BuildRing(minX, maxX, minZ, maxZ, out vertices, out triangles);
        else
        {
            vertices = new[]
            {
                new Vector3(minX, height, minZ), new Vector3(minX, height, maxZ),
                new Vector3(maxX, height, maxZ), new Vector3(maxX, height, minZ),
            };
            triangles = new[] { 0, 1, 2, 0, 2, 3 };
        }
        var uvs = new Vector2[vertices.Length];
        var normals = new Vector3[vertices.Length];
        var tangents = new Vector4[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z) * uvScale;
            normals[i] = Vector3.up;
            tangents[i] = new Vector4(1, 0, 0, -1);
        }
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        instance = new Material(material) { name = material.name + " (Out Of Bounds)", hideFlags = HideFlags.DontSave };
        if (instance.HasProperty(BaseColorId))
            instance.SetColor(BaseColorId, instance.GetColor(BaseColorId) * tint);

        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var meshRenderer = root.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = instance;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = receiveShadows;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.allowOcclusionWhenDynamic = false;
    }

    // A strip between the hole's rounded edge and the outer rectangle: each hole point pairs with the point
    // where the ray from the hole's centre through it leaves the rectangle
    void BuildRing(float minX, float maxX, float minZ, float maxZ, out Vector3[] vertices, out int[] triangles)
    {
        vertices = new Vector3[HoleSteps * 2];
        triangles = new int[HoleSteps * 6];
        for (int i = 0; i < HoleSteps; i++)
        {
            float a = i / (float)HoleSteps * Mathf.PI * 2f;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), holeRoundness) * holeHalfSize.x;
            float z = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), holeRoundness) * holeHalfSize.y;
            Vector2 inner = holeCenter + new Vector2(x, z);

            Vector2 dir = new Vector2(x, z).normalized;
            float tx = dir.x > 0f ? (maxX - holeCenter.x) / dir.x : dir.x < 0f ? (minX - holeCenter.x) / dir.x : float.MaxValue;
            float tz = dir.y > 0f ? (maxZ - holeCenter.y) / dir.y : dir.y < 0f ? (minZ - holeCenter.y) / dir.y : float.MaxValue;
            Vector2 outer = holeCenter + dir * Mathf.Min(tx, tz);

            vertices[i * 2] = new Vector3(inner.x, holeHeight, inner.y);
            vertices[i * 2 + 1] = new Vector3(outer.x, holeHeight, outer.y);

            // Counter-clockwise round the loop seen from above, so these wind clockwise: facing up
            int a0 = i * 2, a1 = a0 + 1, b0 = (i + 1) % HoleSteps * 2, b1 = b0 + 1;
            triangles[i * 6] = a0; triangles[i * 6 + 1] = b0; triangles[i * 6 + 2] = a1;
            triangles[i * 6 + 3] = a1; triangles[i * 6 + 4] = b0; triangles[i * 6 + 5] = b1;
        }
    }

    // Everything solid in our scene (meshes and tilemaps), so the ground is centred on and covers the level
    Bounds LevelBounds()
    {
        bool any = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r.gameObject.scene != gameObject.scene || !(r is MeshRenderer || r is TilemapRenderer))
                continue;
            if (r.gameObject.name == RootName)
                continue;

            if (!any)
                b = r.bounds;
            else
                b.Encapsulate(r.bounds);
            any = true;
        }
        return b;
    }

    void ApplyBackground()
    {
        // Play mode only, so the editor never dirties the camera in the prefab or scene
        if (!Application.isPlaying || !setCameraBackground)
            return;

        var cam = GetComponentInChildren<Camera>();
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
    }

    void Clear()
    {
        if (root != null)
            DestroyObject(root);
        root = null;
        if (mesh != null)
            DestroyObject(mesh);
        mesh = null;
        if (instance != null)
            DestroyObject(instance);
        instance = null;

        // After a script recompile the references are lost, so also sweep up by name
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            return;
        foreach (var go in gameObject.scene.GetRootGameObjects())
            if (go.name == RootName)
                DestroyObject(go);
    }

    static void DestroyObject(Object o)
    {
        if (Application.isPlaying)
            Destroy(o);
        else
            DestroyImmediate(o);
    }
}
