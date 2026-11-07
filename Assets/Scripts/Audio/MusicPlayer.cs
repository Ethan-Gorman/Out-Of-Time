using System;
using System.Collections;
using UnityEngine;

// One era's soundtrack: two stems of exactly the same length
[Serializable]
public class EraTrack
{
    public Era era;
    public AudioClip music;
    public AudioClip drums;
}

// Plays the current era's music and drums stems in sync, and reacts to world time:
// when the player stands still the drums fade out and everything gets muffled and washed in reverb.
// Part of the Persistent prefab, so the music keeps playing through level restarts.
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    public EraTrack[] tracks;

    // Each source has its own filters on the same object (Unity needs them together)
    public AudioSource musicSource;
    public AudioSource drumsSource;

    [Range(0f, 1f)] public float volume = 0.8f;

    [Tooltip("Seconds to fade out the old era and fade in the new one")]
    public float fadeTime = 0.5f;

    [Tooltip("Roughly how many seconds the drums/muffle/reverb take to follow the player stopping or moving")]
    public float freezeFadeTime = 1.5f;

    [Tooltip("Low-pass cutoff when the world is frozen. Lower = more muffled")]
    public float frozenCutoff = 800f;
    public float openCutoff = 22000f;

    [Tooltip("Reverb level when frozen, in millibels. -10000 = none, 0 = full")]
    [Range(-10000f, 0f)] public float frozenReverb = -1200f;

    [Tooltip("Reverb tail length in seconds")]
    public float reverbDecay = 3f;

    AudioLowPassFilter musicFilter;
    AudioLowPassFilter drumsFilter;
    AudioReverbFilter musicReverb;

    // 0..1 multiplier used for fading between eras
    float eraFade;

    // World time scale, smoothed more slowly than WorldTime itself so the music eases in and out
    float smoothedScale = 1f;
    float smoothVelocity;
    Coroutine switching;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    void Awake()
    {
        // LevelManager on this object destroys duplicates, but Awake order isn't guaranteed,
        // so don't let a duplicate subscribe in the meantime
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        FindFilters();
        LevelManager.EraChanged += Play;
    }

    // Also called from Update: a script recompile during Play mode wipes these private references
    void FindFilters()
    {
        musicFilter = musicSource.GetComponent<AudioLowPassFilter>();
        drumsFilter = drumsSource.GetComponent<AudioLowPassFilter>();

        // Reverb goes on the music stem only: the drums fade out when frozen anyway
        musicReverb = musicSource.GetComponent<AudioReverbFilter>();
        if (musicReverb == null)
            musicReverb = musicSource.gameObject.AddComponent<AudioReverbFilter>();
        musicReverb.reverbPreset = AudioReverbPreset.User;
        musicReverb.decayTime = reverbDecay;
    }

    void OnDestroy()
    {
        if (Instance != this)
            return;

        LevelManager.EraChanged -= Play;
        Instance = null;
    }

    void Play(Era era)
    {
        var track = Array.Find(tracks, t => t.era == era);
        if (track == null || track.music == null || track.drums == null)
        {
            Debug.LogWarning($"[MusicPlayer] No music set up for {era}.");
            return;
        }

        if (musicSource.clip == track.music && musicSource.isPlaying)
            return;

        if (switching != null)
            StopCoroutine(switching);
        switching = StartCoroutine(Switch(track));
    }

    IEnumerator Switch(EraTrack track)
    {
        // Unscaled time throughout: world time can be frozen while we fade
        while (eraFade > 0f)
        {
            eraFade = Mathf.MoveTowards(eraFade, 0f, Time.unscaledDeltaTime / fadeTime);
            yield return null;
        }

        musicSource.Stop();
        drumsSource.Stop();
        musicSource.clip = track.music;
        drumsSource.clip = track.drums;

        // Both clips must be loaded before scheduling, or one could start late and drift
        track.music.LoadAudioData();
        track.drums.LoadAudioData();
        while (track.music.loadState == AudioDataLoadState.Loading || track.drums.loadState == AudioDataLoadState.Loading)
            yield return null;

        // Start both on the same audio tick, slightly in the future, so they are sample-aligned
        double start = AudioSettings.dspTime + 0.1;
        musicSource.PlayScheduled(start);
        drumsSource.PlayScheduled(start);

        while (eraFade < 1f)
        {
            eraFade = Mathf.MoveTowards(eraFade, 1f, Time.unscaledDeltaTime / fadeTime);
            yield return null;
        }

        switching = null;
    }

    void Update()
    {
        if (musicFilter == null || musicReverb == null)
            FindFilters();

        // Unscaled time: the music must keep easing even when the world is frozen
        smoothedScale = Mathf.SmoothDamp(smoothedScale, Mathf.Clamp01(WorldTime.Scale), ref smoothVelocity,
            freezeFadeTime, Mathf.Infinity, Time.unscaledDeltaTime);
        float s = Mathf.Clamp01(smoothedScale);

        musicSource.volume = volume * eraFade;

        // Drums disappear as the world stops
        drumsSource.volume = volume * eraFade * Mathf.SmoothStep(0f, 1f, s);

        // Exponential sweep sounds even to the ear: 800 Hz when frozen, fully open when moving
        float cutoff = frozenCutoff * Mathf.Pow(openCutoff / frozenCutoff, s);
        musicFilter.cutoffFrequency = cutoff;
        drumsFilter.cutoffFrequency = cutoff;

        // Reverb swells in as the world stops, so the music hangs in the air
        musicReverb.room = Mathf.Lerp(frozenReverb, -10000f, s);
    }
}
