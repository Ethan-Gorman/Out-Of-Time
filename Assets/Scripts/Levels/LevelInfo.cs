using UnityEngine;

// The four eras, in play order
public enum Era { Caveman, Medieval, Steampunk, Future }

// Says which era this scene is. Lives on the LevelEssentials prefab root:
// in each scene, set Era on the LevelEssentials instance (a prefab override), nothing else to add.
public class LevelInfo : MonoBehaviour
{
    public Era era = Era.Caveman;

    // Editor only: changing Era in the Inspector during Play mode switches era straight away, for testing
    void OnValidate()
    {
        if (Application.isPlaying && LevelManager.Instance != null)
            LevelManager.Instance.RefreshEra();
    }
}
