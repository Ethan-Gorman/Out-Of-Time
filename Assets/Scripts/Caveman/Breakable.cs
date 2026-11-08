using UnityEngine;
using UnityEngine.Events;

// A tree or rock the player breaks for wood or stone. Each press of Interact is one hit:
// bare hands do Hand Damage, the Stone Axe does Axe Damage. Require Axe makes hands useless (brambles).
// Shakes on each hit in world time. Keep the art on the "Visual" child; needs a trigger collider for the range.
public class Breakable : Interactable
{
    public enum DestructionStyle { Fall, Crumble }

    public CavemanLevel.Resource resource = CavemanLevel.Resource.Wood;
    public int amount = 1;
    public int health = 3;
    public int handDamage = 1;
    public int axeDamage = 3;
    public bool requireAxe = false;
    public bool requireBoarKilled = false;

    public Transform visual;
    public float shakeTime = 0.2f;
    public float shakeAmount = 0.08f;
    public DestructionStyle destructionStyle = DestructionStyle.Crumble;
    public float destructionDuration = 0.55f;

    [TextArea] public string[] needAxeLines = { "Too tough for bare hands. I need a Stone Axe from the flat stone." };
    [TextArea] public string[] needBoarLines = { "The boar is guarding the cave. I have to hunt it down first." };

    public UnityEvent onBroken;

    float shakeLeft;
    Vector3 visualBase;
    Vector3 visualBaseScale;
    Quaternion visualBaseRotation;
    float destroyLeft;
    bool broken;

    bool HasAxe => CavemanLevel.Instance != null && CavemanLevel.Instance.HasAxe;

    void Awake()
    {
        if (visual != null)
        {
            visualBase = visual.localPosition;
            visualBaseScale = visual.localScale;
            visualBaseRotation = visual.localRotation;
        }
        FitSolidToArt();
    }

    // Rocks reshape their art per instance (scale, rotation, blob meshes), so the prefab's blocker can end up much
    // smaller than what you see and the player walks into the rock: grow the solid box to cover the art's footprint
    void FitSolidToArt()
    {
        var renderers = (visual != null ? visual : transform).GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0)
            return;  // sprite art (firewood, brambles) already has a hand-sized blocker

        foreach (var box in GetComponentsInChildren<BoxCollider>())
        {
            if (box.isTrigger)
                continue;

            // The art's real extent (its mesh vertices) measured along the box's own axes, since the rock may be
            // turned and stretched. Bounding-box corners would stick out far past a turned, rounded part
            bool any = false;
            Bounds art = default;
            foreach (var r in renderers)
            {
                var mesh = r.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null || !mesh.isReadable)
                    continue;
                foreach (var v in mesh.vertices)
                {
                    Vector3 p = box.transform.InverseTransformPoint(r.transform.TransformPoint(v));
                    if (!any) { art = new Bounds(p, Vector3.zero); any = true; }
                    else art.Encapsulate(p);
                }
            }
            if (!any)
                continue;

            box.size = new Vector3(Mathf.Max(box.size.x, art.size.x), box.size.y, Mathf.Max(box.size.z, art.size.z));
            box.center = new Vector3(art.center.x, box.center.y, art.center.z);
        }
    }

    bool BoarAlive => requireBoarKilled && CavemanLevel.Instance != null && !CavemanLevel.Instance.BoarKilled;

    // The action plus what it takes, e.g. "Hit rock (hands: 4 hits)", "Chop tree (axe: 1 hit)", "Cut brambles (needs Stone Axe)"
    public override string Prompt
    {
        get
        {
            string action = !string.IsNullOrEmpty(prompt) && prompt != "Interact" ? prompt
                : requireAxe ? "Cut brambles"
                : resource == CavemanLevel.Resource.Wood ? (HasAxe ? "Chop tree" : "Hit tree")
                : (HasAxe ? "Smash rock" : "Hit rock");
            return $"{action} <size=24>({Requirement})</size>";
        }
    }

    string Requirement
    {
        get
        {
            bool needAxe = requireAxe && !HasAxe;
            if (BoarAlive)
                return needAxe ? "make an axe, hunt the boar" : "hunt the boar first";
            if (needAxe)
                return "needs Stone Axe";
            int damage = HasAxe ? axeDamage : handDamage;
            int hits = damage > 0 ? Mathf.CeilToInt(health / (float)damage) : 0;
            return $"{(HasAxe ? "axe" : "hands")}: {hits} {(hits == 1 ? "hit" : "hits")}";
        }
    }

    public override bool CanInteract => !broken;
    public override bool KeepUsedOutline => false;

    public override void Interact(MedievalInteractor interactor)
    {
        if (broken)
            return;

        if (BoarAlive)
        {
            CavemanFx.Play(CavemanSfx.Sound.Deny);
            if (MedievalUI.Instance != null)
                MedievalUI.Instance.Say(needBoarLines);
            return;
        }

        int damage = HasAxe ? axeDamage : (requireAxe ? 0 : handDamage);
        if (damage <= 0)
        {
            CavemanFx.Play(CavemanSfx.Sound.Deny);
            shakeLeft = shakeTime * 0.5f;
            if (MedievalUI.Instance != null)
                MedievalUI.Instance.Say(needAxeLines);
            return;
        }

        shakeLeft = shakeTime;
        health -= damage;
        HitFeedback(health <= 0);
        if (health > 0)
            return;

        broken = true;
        destroyLeft = destructionDuration;
        foreach (var collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;
        if (amount > 0 && CavemanLevel.Instance != null)
            CavemanLevel.Instance.AddResource(resource, amount);
        onBroken.Invoke();

        if (visual == null || destructionDuration <= 0f)
        {
            Destroy(gameObject);
            return;
        }
    }

    // Sound, chips and a little shake on every hit; a bigger burst and "+1 Wood" when it breaks
    void HitFeedback(bool breaking)
    {
        bool wood = resource == CavemanLevel.Resource.Wood;
        CavemanFx.Play(wood ? CavemanSfx.Sound.Chop : CavemanSfx.Sound.Stone);

        if (materials == null)
            materials = CavemanFx.MaterialsOf(visual != null ? visual : transform);
        Vector3 center = transform.position + Vector3.up * 0.6f;
        CavemanFx.Debris(center, materials, breaking ? 10 : 3, breaking ? 0.16f : 0.1f, breaking ? 3.5f : 2.5f);
        CavemanFx.Shake(breaking ? 0.35f : (HasAxe ? 0.22f : 0.14f));

        if (breaking && amount > 0)
        {
            CavemanFx.Play(CavemanSfx.Sound.Pickup, 0.8f);
            CavemanFx.Text(center + Vector3.up * 0.6f, $"+{amount} {(wood ? "Wood" : "Stone")}",
                wood ? new Color(0.95f, 0.75f, 0.45f) : new Color(0.85f, 0.88f, 0.92f));
        }
    }

    Material[] materials;

    void Update()
    {
        if (broken)
        {
            if (visual == null || destructionDuration <= 0f)
                return;

            destroyLeft -= WorldTime.Delta;
            float t = Mathf.Clamp01(1f - destroyLeft / destructionDuration);
            visual.localPosition = visualBase + Vector3.up * (destructionStyle == DestructionStyle.Fall ? 0f : Mathf.Sin(t * Mathf.PI) * 0.16f);
            visual.localScale = visualBaseScale * (1f - t);
            visual.localRotation = destructionStyle == DestructionStyle.Fall
                ? visualBaseRotation * Quaternion.Euler(0f, 0f, -78f * t)
                : visualBaseRotation * Quaternion.Euler(0f, 300f * t, 0f);
            if (destroyLeft <= 0f)
                Destroy(gameObject);
            return;
        }

        if (visual == null || shakeLeft <= 0f)
            return;
        shakeLeft -= WorldTime.Delta;
        float x = shakeLeft > 0f ? Mathf.Sin(shakeLeft * 80f) * shakeAmount : 0f;
        visual.localPosition = visualBase + new Vector3(x, 0f, 0f);
    }
}
