using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// One-shot setup: creates the border materials and the LevelBorder prefab,
// and nests the prefab into LevelEssentials. Only assets change, no scene files.
// Run via Tools > Out Of Time > Setup Level Border, then delete this file.
public static class SetupBorder
{
    const string PrefabPath = "Assets/prefabs/LevelBorder.prefab";
    const string EssentialsPath = "Assets/prefabs/LevelEssentials.prefab";
    const string MaterialsFolder = "Assets/Materials";
    const string TilesFolder = "Assets/Tiles";

    [MenuItem("Tools/Out Of Time/Setup Level Border")]
    static void Run()
    {
        var shader = Shader.Find("Out Of Time/Greyscale Sprite");
        if (shader == null)
        {
            Debug.LogError("[SetupBorder] Greyscale Sprite shader not found. Check Assets/Shaders compiled.");
            return;
        }

        var wallTop = CreateMaterial("BorderWallTop", shader, m => m.SetFloat("_Brightness", 0.9f));
        var outside = CreateMaterial("BorderOutside", shader, m => m.SetFloat("_Brightness", 0.45f));
        var wallSide = CreateMaterial("BorderWallSide", Shader.Find("Universal Render Pipeline/Lit"),
            m => m.SetColor("_BaseColor", new Color(0.32f, 0.32f, 0.35f)));

        // Start with every tile in Assets/Tiles; pick your own selection on the prefab afterwards
        var tiles = AssetDatabase.FindAssets("t:TileBase", new[] { TilesFolder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(tile => tile != null)
            .ToArray();

        var go = new GameObject("LevelBorder");
        var border = go.AddComponent<LevelBorder>();
        border.wallTiles = tiles;
        border.outsideTiles = tiles;
        border.wallTopMaterial = wallTop;
        border.wallSideMaterial = wallSide;
        border.outsideMaterial = outside;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        var root = PrefabUtility.LoadPrefabContents(EssentialsPath);
        try
        {
            var old = root.transform.Find("LevelBorder");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            PrefabUtility.InstantiatePrefab(prefab, root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, EssentialsPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SetupBorder] Done. Pick tiles on {PrefabPath} (Wall Tiles / Outside Tiles).");
    }

    static Material CreateMaterial(string name, Shader shader, System.Action<Material> configure)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        configure(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
