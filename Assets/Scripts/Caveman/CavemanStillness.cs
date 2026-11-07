using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Shows and teaches the core mechanic in the Caveman Era:
// - a soft dark vignette closes in as the world slows, so "time stopped" reads at a glance (colours stay as they are)
// - the first time the player stands still for a moment, the caveman says what's happening
// Added at runtime by CavemanFx. Builds its own URP Volume, so no scene setup is needed.
public class CavemanStillness : MonoBehaviour
{
    public float stillSaturation = 0f;
    public float stillVignette = 0.3f;
    public float stillContrast = 0f;
    public float fadeSpeed = 6f;

    public float hintDelay = 7f;
    public float hintStillTime = 1f;
    [TextArea] public string[] hintLines =
    {
        "Whoa... when I stand still, the whole world stops.",
        "But my clock still ticks. Look, plan, then move!",
    };

    ColorAdjustments color;
    Vignette vignette;
    float amount;
    float stillFor;
    float alive;
    bool hinted;

    void Start()
    {
        var cam = Camera.main;
        if (cam != null)
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        color = profile.Add<ColorAdjustments>(true);
        vignette = profile.Add<Vignette>(true);
        vignette.smoothness.Override(0.6f);
        vignette.color.Override(new Color(0.08f, 0.05f, 0.03f));

        var volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.sharedProfile = profile;
        Apply();
    }

    void Update()
    {
        // 0 while running, 1 when the world is (almost) frozen
        float target = 1f - Mathf.InverseLerp(0.05f, 0.9f, WorldTime.Scale);
        amount = Mathf.MoveTowards(amount, target, fadeSpeed * Time.deltaTime);
        Apply();

        alive += Time.deltaTime;
        stillFor = WorldTime.Scale < 0.2f ? stillFor + Time.deltaTime : 0f;
        if (!hinted && alive > hintDelay && stillFor > hintStillTime && Clock.Instance != null && !Clock.Instance.IsOut)
        {
            hinted = true;
            if (MedievalUI.Instance != null)
                MedievalUI.Instance.Say(hintLines);
        }
    }

    void Apply()
    {
        if (color == null)
            return;
        float eased = amount * amount * (3f - 2f * amount);
        color.saturation.Override(stillSaturation * eased);
        color.contrast.Override(stillContrast * eased);
        vignette.intensity.Override(stillVignette * eased);
    }
}
