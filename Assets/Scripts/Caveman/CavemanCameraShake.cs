using UnityEngine;

// Trauma-based screen shake, added to the camera at runtime by CavemanFx.Shake.
// Runs after CameraFollow (which rewrites the camera position every LateUpdate),
// so the offset never builds up. Uses real time so hits still shake while the world is frozen.
[DefaultExecutionOrder(10000)]
public class CavemanCameraShake : MonoBehaviour
{
    public float maxOffset = 0.18f;
    public float decayPerSecond = 2.2f;
    public float frequency = 22f;

    float trauma;
    float seed;

    void Awake() => seed = Random.value * 100f;

    public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + amount);

    void LateUpdate()
    {
        if (trauma <= 0f)
            return;

        // Squared trauma: small hits barely move, big hits really kick
        float strength = trauma * trauma * maxOffset;
        float time = Time.unscaledTime * frequency;
        float x = (Mathf.PerlinNoise(seed, time) * 2f - 1f) * strength;
        float y = (Mathf.PerlinNoise(seed + 50f, time) * 2f - 1f) * strength;
        transform.position += transform.right * x + transform.up * y;

        trauma = Mathf.Max(0f, trauma - decayPerSecond * Time.unscaledDeltaTime);
    }
}
