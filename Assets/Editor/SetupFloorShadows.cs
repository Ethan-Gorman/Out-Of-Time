using UnityEditor;
using UnityEngine;

// One-shot setup: creates the floor shadow material and assigns it to the LevelBorder prefab,
// which puts it on each level's painted floor at runtime. Only a material and a prefab change.
// Run via Tools > Out Of Time > Setup Floor Shadows, then delete this file.
public static class SetupFloorShadows
{
    const string MaterialPath = "Assets/Materials/FloorReceiveShadows.mat";
    const string BorderPath = "Assets/prefabs/LevelBorder.prefab";

    [MenuItem("Tools/Out Of Time/Setup Floor Shadows")]
    static void Run()
    {
        var shader = Shader.Find("Out Of Time/Sprite Receive Shadows");
        if (shader == null)
        {
            Debug.LogError("[SetupFloorShadows] Sprite Receive Shadows shader not found. Check Assets/Shaders compiled.");
            return;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        var root = PrefabUtility.LoadPrefabContents(BorderPath);
        try
        {
            root.GetComponent<LevelBorder>().floorMaterial = mat;
            PrefabUtility.SaveAsPrefabAsset(root, BorderPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SetupFloorShadows] Done. Tweak Shadow Strength / Tint on " + MaterialPath);
    }
}
