using UnityEngine;

// The Future era's placeholder props (plain boxes in the art set's surface materials) and the default size of
// each prop role. PropSlot uses it when the art set has no art for a slot, and the builder for its shapes.
// Every placeholder fills its footprint (W x H x D, bottom-centre pivot, front facing -Z), so the slot looks
// the size it plays at.
public static class FutureProps
{
    public static Vector3 DefaultFootprint(PropRole role) => role switch
    {
        PropRole.Crate => new Vector3(1f, 1f, 1f),
        PropRole.CargoStack => new Vector3(1.2f, 2.2f, 1.2f),
        PropRole.Console => new Vector3(1.8f, 1.2f, 0.8f),
        PropRole.Terminal => new Vector3(0.6f, 1.5f, 0.5f),
        PropRole.Pillar => new Vector3(1f, 6f, 1f),
        PropRole.WallPanel => new Vector3(2f, 3f, 0.15f),
        PropRole.Screen => new Vector3(2.4f, 1.4f, 0.1f),
        PropRole.Lamp => new Vector3(0.6f, 0.3f, 0.6f),
        PropRole.Plant => new Vector3(0.8f, 1.4f, 0.8f),
        PropRole.Planter => new Vector3(2.4f, 0.8f, 1f),
        PropRole.Bench => new Vector3(2f, 0.5f, 0.7f),
        PropRole.Desk => new Vector3(2f, 0.8f, 1f),
        PropRole.ServerRack => new Vector3(1.2f, 3f, 2f),
        PropRole.Shelf => new Vector3(2.4f, 3f, 0.8f),
        PropRole.Pipe => new Vector3(0.3f, 0.3f, 2f),
        PropRole.Sign => new Vector3(3f, 0.8f, 0.1f),
        PropRole.Hologram => new Vector3(1.2f, 1.6f, 1.2f),
        PropRole.Vent => new Vector3(1.2f, 0.05f, 1.2f),
        PropRole.Machine => new Vector3(2f, 2f, 2f),
        PropRole.Decal => new Vector3(1f, 0.02f, 1f),
        _ => Vector3.one,
    };

    public static bool DefaultBlocks(PropRole role) => role switch
    {
        PropRole.WallPanel or PropRole.Screen or PropRole.Lamp or PropRole.Pipe or PropRole.Sign
            or PropRole.Hologram or PropRole.Vent or PropRole.Decal => false,
        _ => true,
    };

    public static bool HasFunction(PropRole role) =>
        role == PropRole.Console || role == PropRole.Screen || role == PropRole.Lamp || role == PropRole.Hologram;

    // The role's light, on the slot's Function child, so it stays when the art is swapped
    public static void Function(PropRole role, Vector3 f, Transform parent)
    {
        switch (role)
        {
            case PropRole.Console: Light("Glow", parent, new Vector3(0f, f.y + 0.3f, -f.z), new Color(0.3f, 0.9f, 1f), 0.8f, 3f); break;
            case PropRole.Screen: Light("Glow", parent, new Vector3(0f, f.y * 0.5f, -0.6f), new Color(0.35f, 0.55f, 1f), 0.6f, 3f); break;
            case PropRole.Lamp: Light("Light", parent, new Vector3(0f, -0.4f, 0f), new Color(0.45f, 0.9f, 1f), 2f, 9f); break;
            case PropRole.Hologram: Light("Glow", parent, new Vector3(0f, f.y * 0.5f, 0f), new Color(0.4f, 0.95f, 1f), 1f, 3f); break;
        }
    }

    // Boxes in the art set's materials (or Unity's default material without one)
    public static void Placeholder(PropRole role, Vector3 f, Transform p, FutureArtSet set)
    {
        Material M(FutureSurface s) => set != null ? set.Get(s) : null;
        float w = f.x, h = f.y, d = f.z, front = -d * 0.5f - 0.01f;
        switch (role)
        {
            case PropRole.Crate:
                Box("Body", p, new Vector3(0, h * 0.5f, 0), f * 0.94f, M(FutureSurface.Metal));
                foreach (float x in new[] { -0.46f, 0.46f }) foreach (float z in new[] { -0.46f, 0.46f })
                    Box("Edge", p, new Vector3(x * w, h * 0.5f, z * d), new Vector3(w * 0.1f, h, d * 0.1f), M(FutureSurface.Trim));
                break;
            case PropRole.CargoStack:
                Box("Lower", p, new Vector3(0, h * 0.25f, 0), new Vector3(w, h * 0.5f, d), M(FutureSurface.Metal));
                Box("Upper", p, new Vector3(0, h * 0.74f, 0), new Vector3(w * 0.9f, h * 0.48f, d * 0.9f), M(FutureSurface.MetalDark));
                Box("Glow band", p, new Vector3(0, h * 0.5f, 0), new Vector3(w * 1.01f, 0.06f, d * 1.01f), M(FutureSurface.Glow));
                break;
            case PropRole.Console:
                Box("Body", p, new Vector3(0, h * 0.4f, 0), new Vector3(w, h * 0.8f, d), M(FutureSurface.MetalDark));
                Box("Panel", p, new Vector3(0, h * 0.86f, 0), new Vector3(w * 0.92f, 0.08f, d * 0.8f), M(FutureSurface.Screen)).transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                break;
            case PropRole.Terminal:
                Box("Post", p, new Vector3(0, h * 0.35f, 0), new Vector3(w * 0.5f, h * 0.7f, d * 0.5f), M(FutureSurface.Metal));
                Box("Screen", p, new Vector3(0, h * 0.82f, 0), new Vector3(w, h * 0.36f, d * 0.4f), M(FutureSurface.Screen));
                break;
            case PropRole.Pillar:
                Box("Column", p, new Vector3(0, h * 0.5f, 0), f, M(FutureSurface.Trim));
                foreach (float y in new[] { 0.3f, 0.75f })
                    Box("Glow band", p, new Vector3(0, h * y, 0), new Vector3(w * 1.04f, 0.12f, d * 1.04f), M(FutureSurface.Glow));
                break;
            case PropRole.WallPanel:
                Box("Panel", p, new Vector3(0, h * 0.5f, 0), f, M(FutureSurface.WallPanel));
                Box("Glow seam", p, new Vector3(0, h * 0.5f, front), new Vector3(0.06f, h * 0.9f, 0.02f), M(FutureSurface.Glow));
                break;
            case PropRole.Screen:
                Box("Frame", p, new Vector3(0, h * 0.5f, 0), f, M(FutureSurface.MetalDark));
                Box("Display", p, new Vector3(0, h * 0.5f, front), new Vector3(w * 0.88f, h * 0.8f, 0.02f), M(FutureSurface.Screen));
                break;
            case PropRole.Lamp:
                Cyl("Housing", p, new Vector3(0, h * 0.6f, 0), w, h * 0.8f, M(FutureSurface.Trim));
                Cyl("Glow disc", p, new Vector3(0, 0.03f, 0), w * 0.8f, 0.06f, M(FutureSurface.Glow));
                break;
            case PropRole.Plant:
                Cyl("Pot", p, new Vector3(0, h * 0.2f, 0), w * 0.7f, h * 0.4f, M(FutureSurface.Trim));
                Sphere("Leaves", p, new Vector3(0, h * 0.65f, 0), new Vector3(w, h * 0.7f, d), M(FutureSurface.Plant));
                break;
            case PropRole.Planter:
                Box("Trough", p, new Vector3(0, h * 0.35f, 0), new Vector3(w, h * 0.7f, d), M(FutureSurface.Trim));
                foreach (float x in new[] { -1f / 3f, 0f, 1f / 3f })
                    Sphere("Leaves", p, new Vector3(x * w, h * 0.72f, 0), new Vector3(w * 0.32f, h * 0.56f, d * 0.8f), M(FutureSurface.Plant));
                break;
            case PropRole.Bench:
                Box("Seat", p, new Vector3(0, h * 0.8f, 0), new Vector3(w, h * 0.25f, d), M(FutureSurface.Metal));
                foreach (float x in new[] { -0.4f, 0.4f })
                    Box("Leg", p, new Vector3(x * w, h * 0.35f, 0), new Vector3(0.12f, h * 0.7f, d * 0.8f), M(FutureSurface.MetalDark));
                break;
            case PropRole.Desk:
                Box("Top", p, new Vector3(0, h * 0.94f, 0), new Vector3(w, h * 0.12f, d), M(FutureSurface.Trim));
                Box("Body", p, new Vector3(0, h * 0.44f, d * 0.05f), new Vector3(w * 0.94f, h * 0.88f, d * 0.85f), M(FutureSurface.MetalDark));
                break;
            case PropRole.ServerRack:
                Box("Body", p, new Vector3(0, h * 0.5f, 0), f, M(FutureSurface.MetalDark));
                // Status lights on the front, mostly cyan with a few amber
                for (int row = 0; row < 6; row++) for (int col = 0; col < 3; col++)
                    Box("Status light", p, new Vector3((col - 1) * w * 0.28f, h * (0.15f + row * 0.13f), front), new Vector3(0.1f, 0.06f, 0.02f),
                        (row * 7 + col * 3) % 4 == 0 ? M(FutureSurface.GlowWarm) : M(FutureSurface.Glow));
                break;
            case PropRole.Shelf:
                foreach (float x in new[] { -0.48f, 0.48f }) foreach (float z in new[] { -0.45f, 0.45f })
                    Box("Post", p, new Vector3(x * w, h * 0.5f, z * d), new Vector3(0.08f, h, 0.08f), M(FutureSurface.MetalDark));
                for (int i = 0; i < 3; i++)
                {
                    float y = h * (0.05f + i * 0.33f);
                    Box("Shelf", p, new Vector3(0, y, 0), new Vector3(w, 0.08f, d), M(FutureSurface.Metal));
                    Box("Box", p, new Vector3((i - 1) * w * 0.25f, y + 0.25f, 0), new Vector3(w * 0.22f, 0.42f, d * 0.7f), M(i % 2 == 0 ? FutureSurface.MetalDark : FutureSurface.Trim));
                }
                break;
            case PropRole.Pipe:
                Cyl("Pipe", p, new Vector3(0, h * 0.5f, 0), w, d, M(FutureSurface.Metal)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            case PropRole.Sign:
                Box("Board", p, new Vector3(0, h * 0.5f, 0), f, M(FutureSurface.MetalDark));
                Box("Face", p, new Vector3(0, h * 0.5f, front), new Vector3(w * 0.9f, h * 0.6f, 0.02f), M(FutureSurface.Glow));
                break;
            case PropRole.Hologram:
                Cyl("Base", p, new Vector3(0, 0.03f, 0), w, 0.06f, M(FutureSurface.MetalDark));
                var holo = Cyl("Hologram", p, new Vector3(0, h * 0.5f, 0), w * 0.8f, h, M(FutureSurface.Hologram));
                var spin = holo.AddComponent<GearSpinner>(); spin.axis = Vector3.up; spin.degreesPerSecond = 20f;
                break;
            case PropRole.Vent:
                Box("Frame", p, new Vector3(0, h * 0.5f, 0), f, M(FutureSurface.MetalDark));
                for (int i = 0; i < 4; i++)
                    Box("Slat", p, new Vector3(0, h + 0.005f, (i - 1.5f) * d * 0.22f), new Vector3(w * 0.85f, 0.01f, d * 0.08f), M(FutureSurface.Metal));
                break;
            case PropRole.Machine:
                Box("Body", p, new Vector3(0, h * 0.5f, 0), f, M(FutureSurface.Metal));
                Box("Glow band", p, new Vector3(0, h * 0.66f, 0), new Vector3(w * 1.02f, Mathf.Min(0.2f, h * 0.05f), d * 1.02f), M(FutureSurface.Glow));
                break;
            case PropRole.Decal:
                Box("Decal", p, new Vector3(0, h * 0.5f, 0), f, M(FutureSurface.FloorAccent));
                break;
            default:
                Box("Body", p, new Vector3(0, h * 0.5f, 0), f, M(FutureSurface.Metal));
                break;
        }
    }

    // ---- Shapes (no colliders: blocking comes from footprints and the builder's own collider boxes)

    public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        var go = Primitive(PrimitiveType.Cube, name, parent, pos, mat);
        go.transform.localScale = size;
        return go;
    }

    // A cylinder standing up, by diameter and full height (Unity's cylinder is 2 high at scale 1)
    public static GameObject Cyl(string name, Transform parent, Vector3 pos, float diameter, float height, Material mat)
    {
        var go = Primitive(PrimitiveType.Cylinder, name, parent, pos, mat);
        go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
        return go;
    }

    public static GameObject Sphere(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        var go = Primitive(PrimitiveType.Sphere, name, parent, pos, mat);
        go.transform.localScale = size;
        return go;
    }

    public static Light Light(string name, Transform parent, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }

    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        if (mat != null)
            go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}
