using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// What a prop slot holds. The art for each role comes from FutureArtSet (or a placeholder when it has none)
public enum PropRole
{
    Crate, CargoStack, Console, Terminal, Pillar, WallPanel, Screen, Lamp, Plant, Planter,
    Bench, Desk, ServerRack, Shelf, Pipe, Sign, Hologram, Vent, Machine, Decal, Custom,
}

// A spot for a prop whose art isn't final. Gameplay only ever uses the footprint, so any art can go here:
//  - Function (this object): the blocking collider, made from Footprint (never from the art), and a "Function"
//    child with the role's light (lamps, screens...). Kept when the art changes.
//  - Art (the "Art" child): the only part that is ever replaced. Its pivot is moved and scaled to fit the footprint.
// Swapping by hand: delete the Placeholder under Art (inside the FutureLevel prefab), drop any prefab under Art,
// then right-click this component > Refit. Refit also records that prefab in FutureArtSet under this slot's id, so
// rebuilds keep it. Or set the art in FutureArtSet and use Re-dress From Art Set.
// Footprint is W x H x D with a bottom-centre pivot; the slot's front faces -Z (the camera) at no rotation.
[DisallowMultipleComponent]
public class PropSlot : MonoBehaviour
{
    public const string ArtName = "Art";
    public const string FunctionName = "Function";
    public const string PlaceholderName = "Placeholder";
    public const float TallHeight = 6f;

    public string id;
    public PropRole role;
    // Free text when Role is Custom (matched by FutureArtSet entries with the same Custom Role)
    public string customRole;
    public Vector3 footprint = Vector3.one;
    public bool blocks = true;
    // 6 high collider, so the player can't hop onto it to skip something
    public bool tallCollider;
    // Id of the wall this hangs on (it's parented there so the OccluderFader cuts it with the wall), or empty
    public string mountTo;
    // What a sign or screen says, for whoever draws the art
    public string label;
    public FutureArtSet artSet;

    public Transform Art => transform.Find(ArtName);

    void Reset()
    {
        footprint = FutureProps.DefaultFootprint(role);
        blocks = FutureProps.DefaultBlocks(role);
        tallCollider = role == PropRole.Pillar;
    }

    // Rebuilds the art from the art set: its prefab or sprite for this slot, else a placeholder. Safe to repeat
    public void Dress(FutureArtSet set)
    {
        if (set != null)
            artSet = set;
        ApplyCollider();
        EnsureFunction();

        var old = Art;
        if (old != null)
        {
            old.name = ArtName + " (old)";
            Kill(old.gameObject);
        }
        var art = new GameObject(ArtName).transform;
        art.SetParent(transform, false);

        var entry = artSet != null ? artSet.Find(id, role, customRole) : null;
        if (entry == null)
        {
            var holder = new GameObject(PlaceholderName).transform;
            holder.SetParent(art, false);
            FutureProps.Placeholder(role, footprint, holder, artSet);
            return;
        }

        if (entry.prefab != null)
        {
            var go = Spawn(entry.prefab, art);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(entry.rotation);
            if (!entry.keepArtColliders)
                StripPhysics(go);
        }
        else
        {
            var go = new GameObject("Sprite");
            go.transform.SetParent(art, false);
            go.transform.localRotation = Quaternion.Euler(entry.rotation);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = entry.sprite;
            if (artSet.spriteMaterial != null)
                sr.sharedMaterial = artSet.spriteMaterial;
            go.AddComponent<Billboard>();
        }
        Fit(art, entry);
    }

    // Re-fits whatever is under Art to the footprint (after a hand swap or a footprint change) and remakes the collider
    public void Refit()
    {
        ApplyCollider();
        EnsureFunction();
        var art = Art;
        if (art == null)
        {
            Dress(artSet);
            return;
        }

        // Only the placeholder there: rebuild it at the new footprint. New art next to it: hide the placeholder
        var holder = art.Find(PlaceholderName);
        if (holder != null)
        {
            bool onlyPlaceholder = true;
            foreach (Transform child in art)
                if (child != holder && child.gameObject.activeSelf)
                    onlyPlaceholder = false;
            if (onlyPlaceholder)
            {
                for (int i = holder.childCount - 1; i >= 0; i--)
                    Kill(holder.GetChild(i).gameObject);
                art.localPosition = Vector3.zero; art.localRotation = Quaternion.identity; art.localScale = Vector3.one;
                FutureProps.Placeholder(role, footprint, holder, artSet);
                return;
            }
            holder.gameObject.SetActive(false);
        }
        var entry = artSet != null ? artSet.Find(id, role, customRole) : null;
        // Hand-dropped art can bring its own colliders too: only the footprint may block
        if (entry == null || !entry.keepArtColliders)
            foreach (Transform child in art)
                if (child != holder)
                    StripPhysics(child.gameObject);
        Fit(art, entry);
    }

    // The footprint box on this object (the only collider the prop has), or none when it doesn't block
    public void ApplyCollider()
    {
        var box = GetComponent<BoxCollider>();
        if (!blocks)
        {
            if (box != null)
                Kill(box);
            return;
        }
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();
        float h = tallCollider ? Mathf.Max(TallHeight, footprint.y) : footprint.y;
        box.isTrigger = false;
        box.size = new Vector3(footprint.x, h, footprint.z);
        box.center = new Vector3(0f, h * 0.5f, 0f);
    }

    void EnsureFunction()
    {
        if (transform.Find(FunctionName) != null || !FutureProps.HasFunction(role))
            return;
        var function = new GameObject(FunctionName).transform;
        function.SetParent(transform, false);
        FutureProps.Function(role, footprint, function);
    }

    // Scales and moves Art so its renderers sit inside the footprint, standing on the slot's pivot
    void Fit(Transform art, FutureArtSet.Prop entry)
    {
        art.localPosition = Vector3.zero;
        art.localRotation = Quaternion.identity;
        art.localScale = Vector3.one;
        if (!ArtBounds(art, out Bounds b))
            return;

        Vector3 s = Vector3.one;
        bool native = entry != null && entry.keepNativeScale;
        bool stretch = entry != null && entry.stretchToFootprint;
        if (!native && stretch)
            s = new Vector3(Ratio(footprint.x, b.size.x), Ratio(footprint.y, b.size.y), Ratio(footprint.z, b.size.z));
        else if (!native)
        {
            // Uniform: the tightest axis wins. Flat footprints (decals, vents) only fit their width and depth
            float k = float.MaxValue;
            if (b.size.x > 0.0001f) k = Mathf.Min(k, footprint.x / b.size.x);
            if (b.size.z > 0.0001f) k = Mathf.Min(k, footprint.z / b.size.z);
            if (footprint.y > 0.1f && b.size.y > 0.0001f) k = Mathf.Min(k, footprint.y / b.size.y);
            s = Vector3.one * (k == float.MaxValue ? 1f : k);
        }
        if (entry != null)
            s = Vector3.Scale(s, ScaleFix(entry.scale));

        art.localScale = s;
        Vector3 bottomCentre = new Vector3(b.center.x, b.min.y, b.center.z);
        art.localPosition = -Vector3.Scale(bottomCentre, s) + (entry != null ? entry.offset : Vector3.zero);
    }

    static float Ratio(float want, float have) => have > 0.0001f ? want / have : 1f;

    // A 0 on an axis means "not set" (an entry added in the Inspector can start at 0,0,0), never "squash flat"
    static Vector3 ScaleFix(Vector3 scale) => new Vector3(
        scale.x == 0f ? 1f : scale.x, scale.y == 0f ? 1f : scale.y, scale.z == 0f ? 1f : scale.z);

    // Bounds of the art's visible renderers, in this slot's space
    bool ArtBounds(Transform art, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        Matrix4x4 toSlot = transform.worldToLocalMatrix;
        foreach (var r in art.GetComponentsInChildren<Renderer>())
        {
            // Only the solid shape counts: particles, trails and lines (a kit torch's flame) would throw the fit off
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer || r is SpriteRenderer))
                continue;
            Bounds local = r.localBounds;
            if (local.size.sqrMagnitude < 1e-8f)
                continue;
            Matrix4x4 m = toSlot * r.transform.localToWorldMatrix;
            Vector3 min = local.min, max = local.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else bounds.Encapsulate(p);
            }
        }
        return any;
    }

    static GameObject Spawn(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && PrefabUtility.IsPartOfPrefabAsset(prefab))
        {
            var go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (go != null)
                return go;
        }
#endif
        return Instantiate(prefab, parent, false);
    }

    // Kit art often carries its own colliders and bodies: the footprint collider replaces them
    static void StripPhysics(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            Kill(c);
        foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
            Kill(body);
    }

    static void Kill(Object o)
    {
        if (Application.isPlaying)
            Destroy(o);
        else
            DestroyImmediate(o);
    }

#if UNITY_EDITOR
    [ContextMenu("Refit")]
    void RefitFromMenu()
    {
        if (LockedByPrefab())
            return;
        Undo.RegisterFullObjectHierarchyUndo(gameObject, "Refit prop");
        RememberHandArt();
        Refit();
        MarkChanged();
    }

    // Apply Art Set and Build Future Era rebuild every Art child from the art set, so a prefab dropped in by hand
    // would be lost: record it as this slot's own entry (by slot id, with its turn) so every rebuild puts it back
    void RememberHandArt()
    {
        var art = Art;
        if (art == null || artSet == null)
            return;
        GameObject placed = null;
        foreach (Transform child in art)
        {
            if (child.name == PlaceholderName || !child.gameObject.activeSelf)
                continue;
            // Several pieces: no single prefab to record
            if (placed != null)
                return;
            placed = child.gameObject;
        }
        if (placed == null || !PrefabUtility.IsAnyPrefabInstanceRoot(placed))
            return;
        var source = PrefabUtility.GetCorrespondingObjectFromSource(placed);
        if (source == null)
            return;
        var current = artSet.Find(id, role, customRole);
        if (current != null && current.prefab == source)
            return;

        Undo.RecordObject(artSet, "Record hand-placed prop");
        FutureArtSet.Prop entry = null;
        if (artSet.props != null)
            foreach (var p in artSet.props)
                if (p != null && p.slotId == id)
                    entry = p;
        if (entry == null)
        {
            entry = new FutureArtSet.Prop { slotId = id };
            var list = new System.Collections.Generic.List<FutureArtSet.Prop>(artSet.props ?? new FutureArtSet.Prop[0]) { entry };
            artSet.props = list.ToArray();
        }
        entry.role = role;
        entry.customRole = customRole;
        entry.prefab = source;
        entry.sprite = null;
        entry.rotation = placed.transform.localEulerAngles;
        entry.scale = Vector3.one;
        entry.offset = Vector3.zero;
        EditorUtility.SetDirty(artSet);
        AssetDatabase.SaveAssetIfDirty(artSet);
        Debug.Log($"[PropSlot] Recorded {source.name} for slot {id} in {artSet.name}, so rebuilds keep it.", artSet);
    }

    [ContextMenu("Re-dress From Art Set")]
    void RedressFromMenu()
    {
        if (LockedByPrefab())
            return;
        Dress(artSet);
        MarkChanged();
    }

    // Objects inside a prefab instance in a scene can't be deleted by script: do it in the prefab itself
    bool LockedByPrefab()
    {
        var art = Art;
        if (art == null || !PrefabUtility.IsPartOfPrefabInstance(art.gameObject))
            return false;
        Debug.LogWarning($"[PropSlot] {name} is inside a prefab instance. Open the FutureLevel prefab and use Refit there, " +
                         "or run Tools > Out Of Time > Future > Apply Art Set.", this);
        return true;
    }

    void MarkChanged()
    {
        EditorUtility.SetDirty(gameObject);
        if (!Application.isPlaying && gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    // The footprint as a wire box (and the tall collider, fainter), coloured by role, so empty slots show up
    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Color c = Color.HSVToRGB((int)role / 21f, 0.65f, 1f);
        Gizmos.color = c;
        Gizmos.DrawWireCube(new Vector3(0f, footprint.y * 0.5f, 0f), footprint);
        if (blocks && tallCollider && footprint.y < TallHeight)
        {
            Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
            Gizmos.DrawWireCube(new Vector3(0f, TallHeight * 0.5f, 0f), new Vector3(footprint.x, TallHeight, footprint.z));
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
