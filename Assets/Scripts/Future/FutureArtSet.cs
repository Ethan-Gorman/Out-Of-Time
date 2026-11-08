using System;
using UnityEngine;

// The surfaces the Future level is built from. Every wall, floor, prop placeholder and gameplay prefab asks the
// art set for one of these, so retexturing the level is one material swap per role
public enum FutureSurface
{
    Floor, FloorAccent, Wall, WallPanel, Trim, Glass, Metal, MetalDark, Glow, GlowWarm, Danger, Screen, Rift,
    Hologram, Plant, CoinBronze, CoinSilver, CoinGold, Void,
}

// All of the Future era's art choices in one asset (Assets/prefabs/Future/FutureArtSet.asset), so the props can be
// swapped without touching code:
//  - Props: for a prop role (Crate, Console...) or one slot id ("CORE-06"), the prefab or sprite to put there,
//    plus a scale / offset / rotation fix. Roles with no entry get a clean placeholder made of boxes.
//  - Surfaces: which material each surface role uses. The builder fills empty ones with its placeholder materials.
// After editing it, run Tools > Out Of Time > Future > Apply Art Set (or Build Future Era again).
// Gameplay never comes from here: colliders are made from each slot's footprint, so new art can't change the route.
[CreateAssetMenu(menuName = "Out Of Time/Future Art Set")]
public class FutureArtSet : ScriptableObject
{
    [Serializable]
    public class Prop
    {
        // Dresses every slot with this role...
        public PropRole role;
        // ...or, when set, only the slot with this id (wins over a role entry). Ids are listed by
        // Tools > Out Of Time > Future > List Prop Slots
        public string slotId;
        // Matches Custom slots by their custom role name
        public string customRole;

        // A prefab (any model or prefab)...
        public GameObject prefab;
        // ...or a flat sprite that faces the camera (used when there's no prefab)
        public Sprite sprite;

        // Off: the art is scaled (keeping its proportions) to fit inside the slot's footprint.
        // On: the prefab keeps its own size
        public bool keepNativeScale;
        // On: scaled on each axis to fill the footprint exactly (ignored with Keep Native Scale)
        public bool stretchToFootprint;

        // Fixes applied after fitting: a multiplier on the size, a shift (slot space, units) and a turn (degrees),
        // e.g. a model that faces +X needs Rotation (0, -90, 0) to face the camera
        public Vector3 scale = Vector3.one;
        public Vector3 offset;
        public Vector3 rotation;

        // Off: colliders inside the art are removed (the slot's own footprint collider does the blocking)
        public bool keepArtColliders;

        public bool HasArt => prefab != null || sprite != null;
    }

    [Serializable]
    public class Surface
    {
        public FutureSurface role;
        public Material material;
    }

    public Prop[] props = new Prop[0];
    public Surface[] surfaces = new Surface[0];

    // Material for sprite props (the builder sets SpriteLit)
    public Material spriteMaterial;

    // The art for one slot: its slot id entry first, then its role, else null (the slot gets a placeholder)
    public Prop Find(string slotId, PropRole role, string customRole)
    {
        if (props == null)
            return null;
        if (!string.IsNullOrEmpty(slotId))
            foreach (var p in props)
                if (p != null && p.HasArt && p.slotId == slotId)
                    return p;
        foreach (var p in props)
        {
            if (p == null || !p.HasArt || !string.IsNullOrEmpty(p.slotId) || p.role != role)
                continue;
            if (role == PropRole.Custom && p.customRole != customRole)
                continue;
            return p;
        }
        return null;
    }

    public Material Get(FutureSurface role)
    {
        if (surfaces != null)
            foreach (var s in surfaces)
                if (s != null && s.role == role)
                    return s.material;
        return null;
    }
}
