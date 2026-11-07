using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Caveman game-feel helpers: sound effects, screen shake, floating text and debris chips.
// Created on demand by CavemanLevel, so nothing needs placing in the scene.
// Debris runs on world time, so chips hang in the air when the player stands still.
public class CavemanFx : MonoBehaviour
{
    public static CavemanFx Instance { get; private set; }

    public float volume = 0.6f;
    public float maxShakeOffset = 0.18f;
    public float textRiseSpeed = 60f;
    public float textLifetime = 1.1f;

    const int VoiceCount = 8;

    struct Chip
    {
        public Transform transform;
        public Vector3 velocity;
        public Vector3 spin;
        public Vector3 scale;
        public float age;
        public float life;
    }

    struct FloatingText
    {
        public RectTransform rect;
        public Text text;
        public Vector3 worldPosition;
        public Color color;
        public float age;
    }

    readonly List<Chip> chips = new List<Chip>();
    readonly List<FloatingText> texts = new List<FloatingText>();
    AudioSource[] voices;
    int nextVoice;
    Canvas canvas;
    Mesh chipMesh;
    CavemanCameraShake shake;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public static CavemanFx Ensure()
    {
        if (Instance == null)
            new GameObject("CavemanFx").AddComponent<CavemanFx>();
        return Instance;
    }

    void Awake()
    {
        Instance = this;

        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
            voices[i].spatialBlend = 0f;
        }

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chipMesh = cube.GetComponent<MeshFilter>().sharedMesh;
        Destroy(cube);

        BuildCanvas();
        gameObject.AddComponent<CavemanStillness>();
    }

    void Start()
    {
        Clock.TimeChanged += OnClockChanged;
    }

    void OnDestroy()
    {
        Clock.TimeChanged -= OnClockChanged;
        if (Instance == this)
            Instance = null;
    }

    // Damage and rewards get their own sound and a kick of shake
    void OnClockChanged(float seconds)
    {
        if (seconds < 0f)
        {
            Play(CavemanSfx.Sound.Hurt);
            Shake(0.7f);
        }
        else
        {
            Play(CavemanSfx.Sound.Gain);
        }
    }

    // ---- Sound ----

    // Pitch dips a little when the world is slowed, and varies slightly so repeats don't sound robotic
    public static void Play(CavemanSfx.Sound sound, float volumeScale = 1f)
    {
        var fx = Ensure();
        var voice = fx.voices[fx.nextVoice];
        fx.nextVoice = (fx.nextVoice + 1) % VoiceCount;
        voice.clip = CavemanSfx.Get(sound);
        voice.volume = fx.volume * volumeScale;
        voice.pitch = Mathf.Lerp(0.8f, 1f, WorldTime.Scale) * Random.Range(0.94f, 1.06f);
        voice.Play();
    }

    // ---- Screen shake ----

    // amount 0..1: 0.15 for a light tap, 0.7 for taking damage
    public static void Shake(float amount)
    {
        var fx = Ensure();
        if (fx.shake == null)
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            fx.shake = cam.GetComponent<CavemanCameraShake>();
            if (fx.shake == null)
                fx.shake = cam.gameObject.AddComponent<CavemanCameraShake>();
        }
        fx.shake.maxOffset = fx.maxShakeOffset;
        fx.shake.AddTrauma(amount);
    }

    // ---- Debris ----

    // Little cubes that burst out of a hit, wearing the materials of what was hit
    public static void Debris(Vector3 position, Material[] materials, int count, float size = 0.12f, float force = 3f)
    {
        if (materials == null || materials.Length == 0 || count <= 0)
            return;

        var fx = Ensure();
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Chip");
            go.transform.position = position + Random.insideUnitSphere * 0.2f;
            go.transform.rotation = Random.rotation;
            var scale = Vector3.one * size * Random.Range(0.6f, 1.3f);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = fx.chipMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[Random.Range(0, materials.Length)];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector2 flat = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1f) * force;
            fx.chips.Add(new Chip
            {
                transform = go.transform,
                velocity = new Vector3(flat.x, Random.Range(1.2f, 2f) * force, flat.y),
                spin = Random.insideUnitSphere * 720f,
                scale = scale,
                life = Random.Range(0.6f, 0.9f),
            });
        }
    }

    // All the materials on an object's renderers, for Debris
    public static Material[] MaterialsOf(Component root)
    {
        if (root == null)
            return null;
        var list = new List<Material>();
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            foreach (var material in renderer.sharedMaterials)
                if (material != null && !list.Contains(material))
                    list.Add(material);
        return list.ToArray();
    }

    // ---- Floating text ----

    public static void Text(Vector3 worldPosition, string message, Color color)
    {
        var fx = Ensure();
        var go = new GameObject("FloatText", typeof(RectTransform));
        go.transform.SetParent(fx.canvas.transform, false);
        var text = go.AddComponent<Text>();
        text.text = message;
        text.font = fx.Font();
        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        fx.texts.Add(new FloatingText
        {
            rect = (RectTransform)go.transform,
            text = text,
            worldPosition = worldPosition,
            color = color,
        });
        fx.PlaceText(fx.texts[fx.texts.Count - 1]);
    }

    Font Font()
    {
        if (MedievalUI.Instance != null && MedievalUI.Instance.dialogueText != null && MedievalUI.Instance.dialogueText.font != null)
            return MedievalUI.Instance.dialogueText.font;
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void BuildCanvas()
    {
        var go = new GameObject("CavemanFxCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.matchWidthOrHeight = 1f;
    }

    void PlaceText(FloatingText item)
    {
        var cam = Camera.main;
        if (cam == null)
            return;
        Vector3 screen = cam.WorldToScreenPoint(item.worldPosition);
        float scale = canvas.transform.localScale.x > 0f ? canvas.transform.localScale.x : 1f;
        item.rect.position = screen + Vector3.up * (item.age * textRiseSpeed * scale);
    }

    // ---- Update ----

    void Update()
    {
        float dt = WorldTime.Delta;
        for (int i = chips.Count - 1; i >= 0; i--)
        {
            var chip = chips[i];
            if (chip.transform == null)
            {
                chips.RemoveAt(i);
                continue;
            }

            chip.age += dt;
            chip.velocity += Physics.gravity * dt;
            chip.transform.position += chip.velocity * dt;
            chip.transform.Rotate(chip.spin * dt, Space.Self);
            float shrink = Mathf.Clamp01((chip.life - chip.age) / (chip.life * 0.4f));
            chip.transform.localScale = chip.scale * shrink;

            if (chip.age >= chip.life)
            {
                Destroy(chip.transform.gameObject);
                chips.RemoveAt(i);
                continue;
            }
            chips[i] = chip;
        }

        // Text runs on real time so it always reads, even while the world is frozen
        for (int i = texts.Count - 1; i >= 0; i--)
        {
            var item = texts[i];
            if (item.rect == null)
            {
                texts.RemoveAt(i);
                continue;
            }

            item.age += Time.deltaTime;
            float pop = 1f + 0.4f * Mathf.Clamp01(1f - item.age / 0.12f);
            item.rect.localScale = Vector3.one * pop;
            var color = item.color;
            color.a = Mathf.Clamp01((textLifetime - item.age) / 0.35f);
            item.text.color = color;
            PlaceText(item);

            if (item.age >= textLifetime)
            {
                Destroy(item.rect.gameObject);
                texts.RemoveAt(i);
                continue;
            }
            texts[i] = item;
        }
    }
}
