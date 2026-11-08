using UnityEditor;
using UnityEditor.SceneManagement;

// Tools > Out Of Time > Play From Caveman: when ticked, pressing Play always starts the game at the Caveman Era
// (the first level, like a build does), whatever scene is open. Untick it to test the open scene directly
[InitializeOnLoad]
static class PlayFromCaveman
{
    const string Menu = "Tools/Out Of Time/Play From Caveman";
    const string Key = "OutOfTime.PlayFromCaveman";
    const string ScenePath = "Assets/Scenes/Caveman.unity";

    static PlayFromCaveman()
    {
        EditorApplication.delayCall += Apply;
    }

    [MenuItem(Menu)]
    static void Toggle()
    {
        EditorPrefs.SetBool(Key, !EditorPrefs.GetBool(Key, false));
        Apply();
    }

    [MenuItem(Menu, true)]
    static bool ToggleValidate()
    {
        UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(Key, false));
        return true;
    }

    static void Apply()
    {
        EditorSceneManager.playModeStartScene = EditorPrefs.GetBool(Key, false)
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)
            : null;
    }
}
