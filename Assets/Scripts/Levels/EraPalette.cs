using UnityEngine;

// Picks the colour palette for the current era and hands it to the Era Palette full-screen shader.
// Part of the Persistent prefab, next to LevelManager. Each palette is a 1-pixel-tall strip of colours.
public class EraPalette : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public Era era;
        public Texture2D palette;
    }

    public Entry[] palettes;

    [Range(0f, 1f)] public float strength = 0.35f;

    static readonly int PaletteId = Shader.PropertyToID("_OOT_Palette");
    static readonly int SizeId = Shader.PropertyToID("_OOT_PaletteSize");
    static readonly int StrengthId = Shader.PropertyToID("_OOT_PaletteStrength");

    Texture2D current;

    void Awake()
    {
        LevelManager.EraChanged += Apply;
        if (LevelManager.Instance != null)
            Apply(LevelManager.Instance.CurrentEra);
    }

    void OnDestroy()
    {
        LevelManager.EraChanged -= Apply;
    }

    // Strength can be tweaked live in the Inspector
    void Update()
    {
        Shader.SetGlobalFloat(StrengthId, current != null ? strength : 0f);
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(StrengthId, 0f);
    }

    void Apply(Era era)
    {
        current = null;
        foreach (var entry in palettes)
            if (entry.era == era && entry.palette != null)
                current = entry.palette;

        if (current == null)
        {
            Shader.SetGlobalFloat(StrengthId, 0f);
            return;
        }

        Shader.SetGlobalTexture(PaletteId, current);
        Shader.SetGlobalFloat(SizeId, current.width);
        Shader.SetGlobalFloat(StrengthId, strength);
    }
}
