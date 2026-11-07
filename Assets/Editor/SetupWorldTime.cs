using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-shot setup: adds the WorldTime controller, the Clock, the clock UI and a test enemy.
// Run via Tools > Out Of Time > Setup World Time, then delete this file.
public static class SetupWorldTime
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string EnemySpritePath = "Assets/Undead Survivor/Sprites/Enemy 0.png";
    const string EnemyControllerPath = "Assets/Undead Survivor/Animations/Enemy/AcEnemy 0.controller";

    [MenuItem("Tools/Out Of Time/Setup World Time")]
    static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[SetupWorldTime] No object tagged Player in the scene.");
            return;
        }

        if (player.GetComponent<TimeController>() == null)
            player.AddComponent<TimeController>();

        if (Object.FindAnyObjectByType<Clock>() == null)
            new GameObject("GameManager").AddComponent<Clock>();

        if (Object.FindAnyObjectByType<ClockUI>() == null)
            CreateClockUI();

        if (Object.FindAnyObjectByType<EnemyChase>() == null)
            CreateEnemy(player.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupWorldTime] Done. Press Play: stand still and the enemy slows down.");
    }

    static void CreateClockUI()
    {
        var canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var textObject = new GameObject("Clock", typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(canvasObject.transform, false);

        // Anchor and pivot to the top-left corner, inset by 40px
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(40f, -40f);
        rect.sizeDelta = new Vector2(600f, 100f);

        var text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 72;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.UpperLeft;
        text.text = "1:00.0";

        var outline = textObject.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        textObject.AddComponent<ClockUI>();
    }

    static void CreateEnemy(Transform player)
    {
        var enemy = new GameObject("Enemy");
        enemy.transform.position = player.position + new Vector3(6f, 0f, 8f);

        var body = enemy.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var capsule = enemy.AddComponent<CapsuleCollider>();
        capsule.radius = 0.3f;
        capsule.height = 1f;
        capsule.center = new Vector3(0f, 0.5f, 0f);

        var spriteObject = new GameObject("Sprite");
        spriteObject.transform.SetParent(enemy.transform, false);
        var sr = spriteObject.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(EnemySpritePath).OfType<Sprite>().FirstOrDefault(s => s.name == "Run 0");
        spriteObject.AddComponent<Billboard>();
        if (sr.sprite != null)
            spriteObject.transform.localPosition = new Vector3(0f, sr.sprite.bounds.extents.y, 0f);

        // The enemy animations target the SpriteRenderer on the same object, so the Animator goes on the sprite child
        var animator = spriteObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyControllerPath);

        var chase = enemy.AddComponent<EnemyChase>();
        chase.target = player;
        chase.sprite = sr;
        chase.animator = animator;
    }
}
