using UnityEngine;

// Keeps a shadow on the ground under its parent, even mid-jump.
// In a 2.5D view this is what makes height readable. It shrinks and fades as the parent rises.
// The shadow is a small grid mesh draped over whatever is below: each grid point is raycast down
// on its own, so over a box edge part of the shadow sits on the box and the rest spills onto the floor.
// Put it on a child object with a SpriteRenderer (the shadow art), rotated to lie flat (X = 90).
// The SpriteRenderer is only used for its sprite, colour, material and sorting; the mesh replaces it.
public class GroundShadow : MonoBehaviour
{
    public float maxDistance = 10f;

    public float fadeHeight = 3f;

    [Range(0f, 1f)] public float minScale = 0.5f;
    [Range(0f, 1f)] public float minAlpha = 0.3f;

    // Lift above the ground so the shadow doesn't flicker into it
    public float groundOffset = 0.02f;

    [Range(2, 16)] public int resolution = 8;

    public LayerMask groundLayers = ~0;

    Transform parent;
    SpriteRenderer sprite;
    float baseAlpha;
    Vector2 baseSize;

    Transform meshTransform;
    MeshRenderer meshRenderer;
    Mesh mesh;
    Vector3[] vertices;
    Color[] colors;

    void Awake()
    {
        parent = transform.parent;
        sprite = GetComponent<SpriteRenderer>();
        if (sprite == null || sprite.sprite == null)
        {
            enabled = false;
            return;
        }

        baseAlpha = sprite.color.a;

        // World size of the shadow art when on the ground (lying flat: sprite x -> world X, sprite y -> world Z)
        Vector3 size = sprite.sprite.bounds.size;
        Vector3 scale = transform.lossyScale;
        baseSize = new Vector2(size.x * scale.x, size.y * scale.y);

        BuildMesh();
        sprite.enabled = false;
    }

    void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }

    // A child with a flat grid mesh that uses the sprite's texture, material and sorting
    void BuildMesh()
    {
        var go = new GameObject("Shadow Mesh", typeof(MeshFilter), typeof(MeshRenderer));
        meshTransform = go.transform;
        meshTransform.SetParent(transform, false);

        int n = resolution;
        vertices = new Vector3[n * n];
        colors = new Color[n * n];
        var uvs = new Vector2[n * n];
        var triangles = new int[(n - 1) * (n - 1) * 6];

        // UVs of the sprite inside its texture, so atlas/sliced sprites work too
        Texture2D tex = sprite.sprite.texture;
        Rect r = sprite.sprite.textureRect;
        Vector2 uvMin = new Vector2(r.xMin / tex.width, r.yMin / tex.height);
        Vector2 uvMax = new Vector2(r.xMax / tex.width, r.yMax / tex.height);

        for (int j = 0, k = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++, k++)
            {
                float u = i / (n - 1f), v = j / (n - 1f);
                uvs[k] = new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v));
            }
        }

        for (int j = 0, t = 0; j < n - 1; j++)
        {
            for (int i = 0; i < n - 1; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
        }

        mesh = new Mesh { name = "Ground Shadow" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;

        meshRenderer = go.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = sprite.sharedMaterial;
        meshRenderer.sortingLayerID = sprite.sortingLayerID;
        meshRenderer.sortingOrder = sprite.sortingOrder;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        var block = new MaterialPropertyBlock();
        block.SetTexture("_MainTex", tex);
        meshRenderer.SetPropertyBlock(block);
    }

    void LateUpdate()
    {
        if (parent == null)
            return;

        // Start a little above the parent's feet so standing on the ground still hits
        float originY = parent.position.y + 0.1f;
        Vector3 centre = parent.position;
        if (!Physics.Raycast(new Vector3(centre.x, originY, centre.z), Vector3.down, out RaycastHit centreHit,
                maxDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            meshRenderer.enabled = false;
            return;
        }
        meshRenderer.enabled = true;

        // 0 on the ground, 1 at fadeHeight or higher
        float t = Mathf.Clamp01((parent.position.y - centreHit.point.y) / fadeHeight);
        Vector2 size = baseSize * Mathf.Lerp(1f, minScale, t);
        Color colour = sprite.color;
        colour.a = baseAlpha * Mathf.Lerp(1f, minAlpha, t);

        // Drape each grid point onto whatever is below it. Points over a drop land lower down,
        // which is what makes the shadow spill over edges
        int n = resolution;
        for (int j = 0, k = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++, k++)
            {
                float x = centre.x + (i / (n - 1f) - 0.5f) * size.x;
                float z = centre.z + (j / (n - 1f) - 0.5f) * size.y;

                Vector3 world;
                Color c = colour;
                if (Physics.Raycast(new Vector3(x, originY, z), Vector3.down, out RaycastHit hit,
                        maxDistance, groundLayers, QueryTriggerInteraction.Ignore))
                {
                    world = hit.point + hit.normal * groundOffset;
                }
                else
                {
                    // Nothing below this point: keep it level and invisible
                    world = new Vector3(x, centreHit.point.y + groundOffset, z);
                    c.a = 0f;
                }

                vertices[k] = meshTransform.InverseTransformPoint(world);
                colors[k] = c;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateBounds();
    }
}
