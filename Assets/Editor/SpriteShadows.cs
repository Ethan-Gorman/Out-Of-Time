using UnityEditor;
using UnityEngine;

// Builder helper: gives a standing prop sprite a real sun shadow (SpriteShadowCaster, sized by the sprite's own scale)
static class SpriteShadows
{
    const string MaterialPath = "Assets/Materials/SpriteShadowCaster.mat";

    public static void Add(GameObject sprite)
    {
        var caster = sprite.GetComponent<SpriteShadowCaster>();
        if (caster == null)
            caster = sprite.AddComponent<SpriteShadowCaster>();
        caster.shadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        caster.matchSpriteScale = true;
    }
}
