using UnityEngine;

// Low-shelf EQ on an AudioSource's object (Unity has no built-in bass filter).
// MusicPlayer adds one to each stem and sets gainDb per era; 0 dB passes audio through untouched.
public class BassBoost : MonoBehaviour
{
    [Range(0f, 15f)] public float gainDb;
    public float frequency = 120f;

    int sampleRate;

    // Coefficients are rebuilt on the audio thread whenever the settings change
    float builtGain = float.NaN, builtFreq = float.NaN;
    float b0, b1, b2, a1, a2, makeup = 1f;

    // Filter state per channel (stereo is plenty for music stems)
    readonly float[] x1 = new float[8], x2 = new float[8], y1 = new float[8], y2 = new float[8];

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }

    void Build(float gain, float freq)
    {
        // RBJ cookbook low shelf, shelf slope 1
        float A = Mathf.Pow(10f, gain / 40f);
        float w = 2f * Mathf.PI * freq / sampleRate;
        float cos = Mathf.Cos(w);
        float alpha = Mathf.Sin(w) / 2f * Mathf.Sqrt(2f);
        float sq = 2f * Mathf.Sqrt(A) * alpha;

        float a0 = (A + 1f) + (A - 1f) * cos + sq;
        b0 = A * ((A + 1f) - (A - 1f) * cos + sq) / a0;
        b1 = 2f * A * ((A - 1f) - (A + 1f) * cos) / a0;
        b2 = A * ((A + 1f) - (A - 1f) * cos - sq) / a0;
        a1 = -2f * ((A - 1f) + (A + 1f) * cos) / a0;
        a2 = ((A + 1f) + (A - 1f) * cos - sq) / a0;

        // Give back half the boost as headroom so loud bass doesn't clip
        makeup = Mathf.Pow(10f, -gain / 40f);

        builtGain = gain;
        builtFreq = freq;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        float gain = gainDb, freq = frequency;
        if (gain <= 0f || sampleRate == 0 || channels > x1.Length)
            return;

        if (gain != builtGain || freq != builtFreq)
            Build(gain, freq);

        for (int i = 0; i < data.Length; i += channels)
        {
            for (int c = 0; c < channels; c++)
            {
                float x = data[i + c];
                float y = b0 * x + b1 * x1[c] + b2 * x2[c] - a1 * y1[c] - a2 * y2[c];
                x2[c] = x1[c]; x1[c] = x;
                y2[c] = y1[c]; y1[c] = y;
                data[i + c] = y * makeup;
            }
        }
    }
}
