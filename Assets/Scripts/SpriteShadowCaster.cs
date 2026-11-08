using UnityEngine;

// Lets a sprite character cast a real shadow from the sun. Sprite shaders can't cast shadows, so this adds an
// invisible "shadows only" quad that shows the sprite's current frame (alpha-clipped) and stands upright facing
// the sun, so the shadow is the character's full silhouette. It follows animation frames and flipping.
// Put it on the object with the SpriteRenderer (e.g. Player > Sprite). Needs a material using
// "Out Of Time/Lit Cutout" with Alpha Clipping on and Cull Off (Materials/SpriteShadowCaster.mat).
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteShadowCaster : MonoBehaviour
{
    public Material shadowMaterial;

    public Light sun;

    // On for props, which scale their sprite object (villagers scale their root, which the caster already follows)
    public bool matchSpriteScale;

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    static Mesh quad;

    SpriteRenderer sprite;
    Transform caster;
    MeshRenderer casterRenderer;
    MaterialPropertyBlock block;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        block = new MaterialPropertyBlock();
        if (shadowMaterial == null)
        {
            enabled = false;
            return;
        }

        // The caster hangs off the character's root, not the billboard (which tilts towards the camera)
        var go = new GameObject("Shadow Caster", typeof(MeshFilter), typeof(MeshRenderer));
        caster = go.transform;
        caster.SetParent(transform.parent != null ? transform.parent : transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = GetQuad();
        casterRenderer = go.GetComponent<MeshRenderer>();
        casterRenderer.sharedMaterial = shadowMaterial;
        casterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        casterRenderer.receiveShadows = false;
    }

    void OnDestroy()
    {
        if (caster != null)
            Destroy(caster.gameObject);
    }

    void LateUpdate()
    {
        var s = sprite.sprite;
        if (s == null)
        {
            casterRenderer.enabled = false;
            return;
        }
        casterRenderer.enabled = true;

        if (sun == null)
            sun = FindSun();

        // Size and position: the sprite's real size, standing on the character's feet
        Vector2 size = s.rect.size / s.pixelsPerUnit;
        if (matchSpriteScale)
            size = Vector2.Scale(size, transform.localScale);
        Vector3 feet = caster.parent.position;
        caster.position = feet;
        caster.localScale = new Vector3(size.x, size.y, 1f);

        // Upright, turned to face the sun, so the shadow is the whole silhouette
        if (sun != null)
        {
            Vector3 towardsSun = -sun.transform.forward;
            towardsSun.y = 0f;
            if (towardsSun.sqrMagnitude > 0.0001f)
                caster.rotation = Quaternion.LookRotation(-towardsSun.normalized, Vector3.up);
        }

        // Show just this frame from the sprite sheet (mirrored when the sprite is flipped)
        Texture2D tex = s.texture;
        Rect r = s.textureRect;
        var st = new Vector4(r.width / tex.width, r.height / tex.height, r.x / tex.width, r.y / tex.height);
        if (sprite.flipX)
        {
            st.z += st.x;
            st.x = -st.x;
        }
        casterRenderer.GetPropertyBlock(block);
        block.SetTexture(BaseMapId, tex);
        block.SetVector(BaseMapStId, st);
        casterRenderer.SetPropertyBlock(block);
    }

    static Light FindSun()
    {
        if (RenderSettings.sun != null)
            return RenderSettings.sun;
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional)
                return light;
        return null;
    }

    // A 1x1 quad standing on its bottom edge (pivot at the feet), UVs 0..1
    static Mesh GetQuad()
    {
        if (quad != null)
            return quad;
        quad = new Mesh { name = "Sprite Shadow Quad" };
        quad.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f) };
        quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateNormals();
        quad.RecalculateBounds();
        return quad;
    }
}
