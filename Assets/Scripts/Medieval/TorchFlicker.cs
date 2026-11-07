using UnityEngine;

// Makes a torch's light flicker like a flame. Put it on the torch, with the Light on it or a child.
public class TorchFlicker : MonoBehaviour
{
    public Light flame;
    [Range(0f, 1f)] public float flickerAmount = 0.25f;
    public float flickerSpeed = 8f;

    float baseIntensity;
    float seed;

    void Awake()
    {
        if (flame == null)
            flame = GetComponentInChildren<Light>();
        if (flame != null)
            baseIntensity = flame.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        if (flame == null)
            return;
        float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed) * 2f - 1f;
        flame.intensity = baseIntensity * (1f + noise * flickerAmount);
    }
}
