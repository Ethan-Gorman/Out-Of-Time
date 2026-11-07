using UnityEditor;
using UnityEngine;

// One-shot setup: adds OccluderFader to the camera in every essentials prefab, points it at the
// Lit Cutout shader, and creates a material that keeps that shader in builds.
// Only prefabs and a material change, no scene files. Safe to run again.
// Run via Tools > Out Of Time > Setup Occluder Fader, then delete this file.
public static class SetupOccluderFader
{
    const string KeeperPath = "Assets/Materials/LitCutoutKeeper.mat";
    const string OldKeeperPath = "Assets/Materials/FadeVariantKeeper.mat";

    static readonly string[] Essentials =
    {
        "Assets/prefabs/LevelEssentials.prefab",
        "Assets/prefabs/Medieval/MedievalEssentials.prefab",
    };

    [MenuItem("Tools/Out Of Time/Setup Occluder Fader")]
    static void Run()
    {
        var shader = Shader.Find("Out Of Time/Lit Cutout");
        if (shader == null)
        {
            Debug.LogError("[SetupOccluderFader] Lit Cutout shader not found. Check Assets/Shaders/LitCutout.shader compiled.");
            return;
        }

        var keeper = AssetDatabase.LoadAssetAtPath<Material>(KeeperPath);
        if (keeper == null)
        {
            keeper = new Material(shader);
            AssetDatabase.CreateAsset(keeper, KeeperPath);
        }

        // From the earlier whole-object fade version, no longer used
        AssetDatabase.DeleteAsset(OldKeeperPath);

        foreach (var path in Essentials)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var cam = root.GetComponentInChildren<Camera>(true);
                if (cam == null)
                {
                    Debug.LogWarning($"[SetupOccluderFader] No camera in {path}");
                    continue;
                }

                var fader = cam.GetComponent<OccluderFader>();
                if (fader == null)
                    fader = cam.gameObject.AddComponent<OccluderFader>();
                fader.cutoutShader = shader;
                fader.variantKeeper = keeper;

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SetupOccluderFader] Done.");
    }
}
