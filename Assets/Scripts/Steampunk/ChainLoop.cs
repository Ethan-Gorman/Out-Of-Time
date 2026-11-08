using System.Collections.Generic;
using UnityEngine;

// A drive chain looped round two sprockets (same radius), in this object's local XY plane.
// Builds its links at start and slides them round the loop as the driving GearSpinner turns,
// so the chain visibly moves with the gear (and stops, in world time, when the gear does).
public class ChainLoop : MonoBehaviour
{
    public GearSpinner drive;
    public Vector2 centerA;
    public Vector2 centerB = new Vector2(3f, 0f);
    public float radius = 0.5f;
    public int linkCount = 30;
    public Vector3 linkSize = new Vector3(0.2f, 0.09f, 0.12f);
    public Material material;

    readonly List<Transform> links = new List<Transform>();
    float offset;

    float Straight => Vector2.Distance(centerA, centerB);
    float Length => 2f * Straight + 2f * Mathf.PI * radius;

    void Start()
    {
        for (int i = 0; i < linkCount; i++)
        {
            var link = GameObject.CreatePrimitive(PrimitiveType.Cube);
            link.name = "Link";
            Destroy(link.GetComponent<Collider>());
            link.transform.SetParent(transform, false);
            link.transform.localScale = linkSize;
            if (material != null)
                link.GetComponent<Renderer>().sharedMaterial = material;
            links.Add(link.transform);
        }
        Place();
    }

    void Update()
    {
        if (drive != null)
            offset += drive.DeltaDegrees * Mathf.Deg2Rad * radius;
        Place();
    }

    void Place()
    {
        float length = Length;
        for (int i = 0; i < links.Count; i++)
        {
            float s = Mathf.Repeat(offset + i * length / links.Count, length);
            Sample(s, out Vector2 p, out Vector2 tangent);
            links[i].localPosition = new Vector3(p.x, p.y, 0f);
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            // Alternate links turn 90 degrees, like a real chain
            links[i].localRotation = Quaternion.Euler(0f, 0f, angle) * Quaternion.Euler(i % 2 == 0 ? 0f : 90f, 0f, 0f);
        }
    }

    // Walks the loop: straight A->B along the top, round B, straight back along the bottom, round A
    void Sample(float s, out Vector2 p, out Vector2 tangent)
    {
        Vector2 axis = (centerB - centerA).normalized;
        Vector2 normal = new Vector2(-axis.y, axis.x);
        float straight = Straight, arc = Mathf.PI * radius;

        if (s < straight)
        {
            p = centerA + normal * radius + axis * s;
            tangent = axis;
            return;
        }
        s -= straight;
        if (s < arc)
        {
            float a = s / radius;
            Vector2 dir = normal * Mathf.Cos(a) + axis * Mathf.Sin(a);
            p = centerB + dir * radius;
            tangent = -normal * Mathf.Sin(a) + axis * Mathf.Cos(a);
            return;
        }
        s -= arc;
        if (s < straight)
        {
            p = centerB - normal * radius - axis * s;
            tangent = -axis;
            return;
        }
        s -= straight;
        {
            float a = s / radius;
            Vector2 dir = -normal * Mathf.Cos(a) - axis * Mathf.Sin(a);
            p = centerA + dir * radius;
            tangent = normal * Mathf.Sin(a) - axis * Mathf.Cos(a);
        }
    }
}
