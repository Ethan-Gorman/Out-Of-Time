using System;
using UnityEngine;

// Caveman sound effects, synthesised in code at first use: no audio files, no packages.
// Each sound is a few lines of oscillator + noise maths. Play them with CavemanFx.Play(...).
public static class CavemanSfx
{
    public enum Sound { Chop, Stone, Pickup, Craft, Crystal, BoarHit, Squeal, Hurt, Gain, Deny }

    const int Rate = 44100;
    const float TwoPi = Mathf.PI * 2f;

    static AudioClip[] clips;

    public static AudioClip Get(Sound sound)
    {
        if (clips == null)
            Build();
        return clips[(int)sound];
    }

    static void Build()
    {
        clips = new AudioClip[Enum.GetValues(typeof(Sound)).Length];
        clips[(int)Sound.Chop] = Chop();
        clips[(int)Sound.Stone] = StoneHit();
        clips[(int)Sound.Pickup] = Pickup();
        clips[(int)Sound.Craft] = Craft();
        clips[(int)Sound.Crystal] = Crystal();
        clips[(int)Sound.BoarHit] = BoarHit();
        clips[(int)Sound.Squeal] = Squeal();
        clips[(int)Sound.Hurt] = Hurt();
        clips[(int)Sound.Gain] = Gain();
        clips[(int)Sound.Deny] = Deny();
    }

    // Wooden thunk: a falling low sine plus a burst of muffled noise
    static AudioClip Chop()
    {
        var rng = new System.Random(1);
        float phase = 0f, lp = 0f;
        return Make("Chop", 0.18f, t =>
        {
            phase += TwoPi * (60f + 160f * Mathf.Exp(-t * 12f)) / Rate;
            lp += (Noise(rng) - lp) * 0.25f;
            return Mathf.Sin(phase) * Mathf.Exp(-t * 22f) * 0.9f + lp * Mathf.Exp(-t * 35f) * 1.4f;
        });
    }

    // Stone click: bright noise snap plus a short ring
    static AudioClip StoneHit()
    {
        var rng = new System.Random(2);
        return Make("Stone", 0.14f, t =>
            Noise(rng) * Mathf.Exp(-t * 60f) * 0.5f
            + Mathf.Sin(TwoPi * 880f * t) * Mathf.Exp(-t * 40f) * 0.35f
            + Mathf.Sin(TwoPi * 2310f * t) * Mathf.Exp(-t * 70f) * 0.2f);
    }

    // Rising square blip
    static AudioClip Pickup()
    {
        float phase = 0f;
        return Make("Pickup", 0.16f, t =>
        {
            phase += TwoPi * (520f + 6000f * t) / Rate;
            return Mathf.Sign(Mathf.Sin(phase)) * 0.18f * Mathf.Exp(-t * 10f);
        });
    }

    // Three-note arpeggio for crafting the axe
    static AudioClip Craft()
    {
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        return Make("Craft", 0.7f, t =>
        {
            int i = Mathf.Min((int)(t / 0.1f), notes.Length - 1);
            float local = t - i * 0.1f;
            float f = notes[i];
            float env = Mathf.Exp(-local * (i == notes.Length - 1 ? 5f : 14f));
            return (Mathf.Sin(TwoPi * f * t) + 0.3f * Mathf.Sin(TwoPi * f * 2f * t)) * 0.3f * env;
        });
    }

    // Glassy bell for the magnetic stones
    static AudioClip Crystal()
    {
        return Make("Crystal", 0.9f, t =>
        {
            float shimmer = 1f + 0.15f * Mathf.Sin(TwoPi * 7f * t);
            return (Mathf.Sin(TwoPi * 1318.5f * t) * Mathf.Exp(-t * 5f)
                  + Mathf.Sin(TwoPi * 1975.5f * t) * Mathf.Exp(-t * 7f) * 0.6f
                  + Mathf.Sin(TwoPi * 2637f * t) * Mathf.Exp(-t * 10f) * 0.4f) * 0.22f * shimmer;
        });
    }

    // Meaty thud for hitting the boar
    static AudioClip BoarHit()
    {
        var rng = new System.Random(3);
        float phase = 0f, lp = 0f;
        return Make("BoarHit", 0.2f, t =>
        {
            phase += TwoPi * (90f + 140f * Mathf.Exp(-t * 15f)) / Rate;
            lp += (Noise(rng) - lp) * 0.35f;
            return Mathf.Sin(phase) * Mathf.Exp(-t * 16f) * 0.7f + lp * Mathf.Exp(-t * 28f) * 1.2f;
        });
    }

    // Wobbly falling saw wave: the boar's squeal
    static AudioClip Squeal()
    {
        float phase = 0f, lp = 0f;
        return Make("Squeal", 0.42f, t =>
        {
            float f = 760f + 220f * Mathf.Sin(TwoPi * 11f * t) - 650f * t;
            phase += f / Rate;
            float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
            lp += (saw - lp) * 0.3f;
            float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t * 4f);
            return lp * 0.3f * env;
        });
    }

    // Low falling square: the player loses time
    static AudioClip Hurt()
    {
        float phase = 0f;
        return Make("Hurt", 0.32f, t =>
        {
            phase += TwoPi * (230f - 420f * t) / Rate;
            return Mathf.Sign(Mathf.Sin(phase)) * 0.16f * Mathf.Exp(-t * 6f);
        });
    }

    // Rising sweep with an octave: the player gains time
    static AudioClip Gain()
    {
        float phase = 0f;
        return Make("Gain", 0.45f, t =>
        {
            phase += TwoPi * 440f * Mathf.Pow(2f, t * 3f) / Rate;
            return (Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 2f)) * 0.22f * Mathf.Exp(-t * 3f);
        });
    }

    // Two dull low blips: "can't do that"
    static AudioClip Deny()
    {
        return Make("Deny", 0.2f, t =>
        {
            bool on = t < 0.07f || (t > 0.1f && t < 0.17f);
            return on ? Mathf.Sign(Mathf.Sin(TwoPi * 140f * t)) * 0.13f : 0f;
        });
    }

    static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

    // Renders the sample function into a clip, with tiny fades so it never clicks
    static AudioClip Make(string name, float seconds, Func<float, float> sample)
    {
        int count = Mathf.CeilToInt(seconds * Rate);
        var data = new float[count];
        int fade = Rate / 500;
        for (int i = 0; i < count; i++)
        {
            float value = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
            if (i < fade) value *= i / (float)fade;
            if (i > count - fade) value *= (count - i) / (float)fade;
            data[i] = value;
        }

        var clip = AudioClip.Create("Caveman" + name, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
