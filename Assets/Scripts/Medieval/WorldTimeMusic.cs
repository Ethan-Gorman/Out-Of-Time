using UnityEngine;

// Bends the music's pitch with world time: it slows and deepens when the player stands still.
// Put it on an object with a looping AudioSource.
[RequireComponent(typeof(AudioSource))]
public class WorldTimeMusic : MonoBehaviour
{
    [Tooltip("Pitch when the world is at its slowest. 1 = no change.")]
    [Range(0.1f, 1f)] public float minPitch = 0.6f;

    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    void Update()
    {
        source.pitch = Mathf.Lerp(minPitch, 1f, WorldTime.Scale);
    }
}
