using System.IO;
using UnityEngine.SceneManagement;

// The eras in play order and the scene each one lives in. "The next level" is the next era in this list whose
// scene is in Build Settings, so other scenes there (SampleScene, test scenes) are never part of the run, and
// the last era built so far is where the run ends.
// Adding an era: add it to the Era enum and to this list, make its scene (LevelInfo set to the new era),
// and add the scene to Build Settings. Nothing else needs to know the order
public static class EraOrder
{
    public static readonly (Era era, string scene)[] Eras =
    {
        (Era.Caveman, "Caveman"),
        (Era.Medieval, "Medieval"),
        (Era.Steampunk, "Steampunk"),
        (Era.Future, "Future"),
    };

    // Build index of the scene with this name, or -1 if it isn't in Build Settings
    public static int BuildIndexOf(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            if (Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)) == sceneName)
                return i;
        return -1;
    }

    // Build index of the era after this scene's, or -1 when this is the last era built so far
    // (or the scene isn't an era at all)
    public static int NextBuildIndex(string currentScene)
    {
        int at = System.Array.FindIndex(Eras, e => e.scene == currentScene);
        if (at < 0)
            return -1;
        for (int i = at + 1; i < Eras.Length; i++)
        {
            int index = BuildIndexOf(Eras[i].scene);
            if (index >= 0)
                return index;
        }
        return -1;
    }

    public static bool HasNext(string currentScene) => NextBuildIndex(currentScene) >= 0;

    // For the active scene
    public static int NextBuildIndex() => NextBuildIndex(SceneManager.GetActiveScene().name);
    public static bool HasNext() => HasNext(SceneManager.GetActiveScene().name);
}
