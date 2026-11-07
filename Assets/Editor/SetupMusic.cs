using UnityEditor;
using UnityEngine;

// One-shot setup: builds the Persistent prefab (LevelManager + MusicPlayer + 2 audio sources),
// nests it into LevelEssentials, adds LevelInfo to LevelEssentials, and sets the music import settings.
// Only prefabs and import settings are changed, no scene files.
// Run via Tools > Out Of Time > Setup Music And Levels, then delete this file.
public static class SetupMusic
{
    const string PersistentPath = "Assets/prefabs/Persistent.prefab";
    const string EssentialsPath = "Assets/prefabs/LevelEssentials.prefab";
    const string MusicFolder = "Assets/Audio/Music";

    static readonly (Era era, string file)[] Tracks =
    {
        (Era.Caveman, "Era1_Caveman"),
        (Era.Medieval, "Era2_Medieval"),
        (Era.Steampunk, "Era3_Steampunk"),
        (Era.Future, "Era4_Future"),
    };

    [MenuItem("Tools/Out Of Time/Setup Music And Levels")]
    static void Run()
    {
        SetImportSettings();
        var persistent = CreatePersistentPrefab();
        AddToLevelEssentials(persistent);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupMusic] Done. Set the Era on LevelEssentials in each scene, then press Play.");
    }

    static void SetImportSettings()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder }))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }

    static GameObject CreatePersistentPrefab()
    {
        var root = new GameObject("Persistent");
        root.AddComponent<LevelManager>();
        var player = root.AddComponent<MusicPlayer>();

        player.musicSource = CreateSource(root.transform, "Music");
        player.drumsSource = CreateSource(root.transform, "Drums");

        player.tracks = new EraTrack[Tracks.Length];
        for (int i = 0; i < Tracks.Length; i++)
        {
            var (era, file) = Tracks[i];
            player.tracks[i] = new EraTrack
            {
                era = era,
                music = LoadClip($"{file}_Music"),
                drums = LoadClip($"{file}_Drums"),
            };
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PersistentPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static AudioSource CreateSource(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f; // 2D: same volume wherever the camera is
        source.priority = 0;      // music never gets culled

        go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
        if (name == "Music")
            go.AddComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.User;
        return source;
    }

    static AudioClip LoadClip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicFolder}/{name}.wav");
        if (clip == null)
            Debug.LogWarning($"[SetupMusic] Missing {MusicFolder}/{name}.wav");
        return clip;
    }

    static void AddToLevelEssentials(GameObject persistent)
    {
        var root = PrefabUtility.LoadPrefabContents(EssentialsPath);
        try
        {
            if (root.GetComponent<LevelInfo>() == null)
                root.AddComponent<LevelInfo>();

            // Replace any older copy, then nest the prefab so edits to Persistent flow through
            var old = root.transform.Find("Persistent");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            PrefabUtility.InstantiatePrefab(persistent, root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, EssentialsPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
