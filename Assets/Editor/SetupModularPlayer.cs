using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

// One-shot setup for the modular player:
//  1. The base Player prefab gets PlayerDamage (hits cost clock time, in every era).
//  2. MedievalPlayer is rebuilt as a Prefab Variant of Player: same sprite, animations and movement,
//     plus only its Medieval extras (jump, interact, shadow). Edits to Player now flow into it.
//  3. MedievalEssentials gets the shared Clock (GameManager) and timer (HUD), and its camera follows the new player.
//  4. The health bar is removed from MedievalUI, and the old MedievalHealth / HealthBarUI scripts are deleted.
// Only prefabs change, no scene files. Run via Tools > Out Of Time > Make Player Modular, then delete this file.
public static class SetupModularPlayer
{
    const string PlayerPath = "Assets/prefabs/Player.prefab";
    const string MedievalPlayerPath = "Assets/prefabs/Medieval/MedievalPlayer.prefab";
    const string EssentialsPath = "Assets/prefabs/Medieval/MedievalEssentials.prefab";
    const string MedievalUIPath = "Assets/prefabs/Medieval/MedievalUI.prefab";
    const string GameManagerPath = "Assets/prefabs/GameManager.prefab";
    const string HudPath = "Assets/prefabs/HUD.prefab";

    [MenuItem("Tools/Out Of Time/Make Player Modular")]
    static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        AddDamageToBasePlayer();
        RebuildMedievalPlayerAsVariant();
        AddTimerToMedievalEssentials();
        RemoveHealthBar();

        AssetDatabase.SaveAssets();
        AssetDatabase.DeleteAsset("Assets/Scripts/Medieval/MedievalHealth.cs");
        AssetDatabase.DeleteAsset("Assets/Scripts/Medieval/HealthBarUI.cs");
        Debug.Log("[SetupModularPlayer] Done. MedievalPlayer is now a variant of Player.");
    }

    static void AddDamageToBasePlayer()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (root.GetComponent<PlayerDamage>() == null)
                root.AddComponent<PlayerDamage>();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void RebuildMedievalPlayerAsVariant()
    {
        var old = PrefabUtility.LoadPrefabContents(MedievalPlayerPath);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var variant = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, preview);
            variant.name = "MedievalPlayer";

            // Physics tuned for jumping: only values that differ become variant overrides
            CopyValues(old.GetComponent<Rigidbody>(), variant.GetComponent<Rigidbody>());
            CopyValues(old.GetComponent<CapsuleCollider>(), variant.GetComponent<CapsuleCollider>());

            // Medieval-only abilities
            var jump = CopyAsNew(old.GetComponent<MedievalJump>(), variant);
            var interactor = CopyAsNew(old.GetComponent<MedievalInteractor>(), variant);

            var shadow = old.transform.Find("Shadow");
            if (shadow != null)
            {
                var copy = Object.Instantiate(shadow.gameObject, variant.transform);
                copy.name = "Shadow";
            }

            // Carry the old health tuning (knockback, invulnerability) over to the shared PlayerDamage
            var damage = variant.GetComponent<PlayerDamage>();
            var oldHealth = old.GetComponents<MonoBehaviour>().FirstOrDefault(c => c != null && c.GetType().Name == "MedievalHealth");
            if (oldHealth != null)
            {
                var from = new SerializedObject(oldHealth);
                var to = new SerializedObject(damage);
                foreach (var name in new[] { "invulnerableTime", "knockbackSpeed", "knockbackUpSpeed", "knockbackTime" })
                {
                    var p = from.FindProperty(name);
                    if (p != null)
                        to.FindProperty(name).floatValue = p.floatValue;
                }
                to.ApplyModifiedPropertiesWithoutUndo();
            }
            damage.disableOnOutOfTime = new Behaviour[] { jump, interactor }.Where(b => b != null).ToArray();

            // Overwrites the asset in place (same GUID), now as a variant of Player
            PrefabUtility.SaveAsPrefabAsset(variant, MedievalPlayerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(old);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static void AddTimerToMedievalEssentials()
    {
        var root = PrefabUtility.LoadPrefabContents(EssentialsPath);
        try
        {
            if (root.GetComponentInChildren<Clock>(true) == null)
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPath), root.transform);
            if (root.GetComponentInChildren<ClockUI>(true) == null)
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath), root.transform);

            // The player was rebuilt, so point the camera at the new one
            var player = root.GetComponentInChildren<PlayerMovement>(true);
            var follow = root.GetComponentInChildren<CameraFollow>(true);
            if (player != null && follow != null)
                follow.target = player.transform;

            PrefabUtility.SaveAsPrefabAsset(root, EssentialsPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void RemoveHealthBar()
    {
        var root = PrefabUtility.LoadPrefabContents(MedievalUIPath);
        try
        {
            var bars = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(c => c != null && c.GetType().Name == "HealthBarUI")
                .Select(c => c.gameObject)
                .ToList();
            foreach (var bar in bars)
                Object.DestroyImmediate(bar);
            PrefabUtility.SaveAsPrefabAsset(root, MedievalUIPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void CopyValues(Component from, Component to)
    {
        if (from == null || to == null)
            return;
        ComponentUtility.CopyComponent(from);
        ComponentUtility.PasteComponentValues(to);
    }

    static T CopyAsNew<T>(T from, GameObject target) where T : Component
    {
        if (from == null)
            return null;
        ComponentUtility.CopyComponent(from);
        ComponentUtility.PasteComponentAsNew(target);
        return target.GetComponent<T>();
    }
}
