using UnityEngine;

// Extra renderers that are cut together with this object when OccluderFader decides it hides the player,
// e.g. the tile tops sitting on a border wall block. Put it on the object with the main renderer.
public class OccluderLink : MonoBehaviour
{
    public Renderer[] alsoCut;
}
