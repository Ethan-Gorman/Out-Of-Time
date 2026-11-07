using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Medieval Era's on-screen text: current objective, interaction prompt, dialogue and a big banner.
// Other scripts reach it with MedievalUI.Instance. Sits on the root of the MedievalUI prefab.
// Only shows and hides objects: restyle or replace the art freely.
public class MedievalUI : MonoBehaviour
{
    public static MedievalUI Instance { get; private set; }

    [Header("Objective (top right)")]
    public GameObject objectiveRoot;
    public Text objectiveText;

    [Header("Interaction prompt (bottom)")]
    public GameObject promptRoot;
    public Text promptText;

    [Header("Dialogue (bottom)")]
    public GameObject dialogueRoot;
    public Text dialogueText;
    [Tooltip("Base seconds each dialogue line stays up, plus secondsPerCharacter for its length.")]
    public float secondsPerLine = 1.8f;
    public float secondsPerCharacter = 0.035f;

    [Header("Banner (centre)")]
    public GameObject bannerRoot;
    public Text bannerText;

    readonly Queue<string> lines = new Queue<string>();
    float lineTimer;

    void Awake()
    {
        Instance = this;
        SetActive(promptRoot, false);
        SetActive(dialogueRoot, false);
        SetActive(bannerRoot, false);
        SetObjective("");
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetObjective(string text)
    {
        SetActive(objectiveRoot, !string.IsNullOrEmpty(text));
        if (objectiveText != null)
            objectiveText.text = text;
    }

    // Shows lines of dialogue one after another, a few seconds each.
    // New dialogue replaces whatever was still waiting, so lines never lag behind what just happened
    public void Say(params string[] newLines)
    {
        if (newLines == null || newLines.Length == 0)
            return;

        lines.Clear();
        foreach (var line in newLines)
            if (!string.IsNullOrEmpty(line))
                lines.Enqueue(line);

        NextLine();
    }

    public void ShowPrompt(string text)
    {
        if (promptText != null)
            promptText.text = text;
        SetActive(promptRoot, true);
    }

    public void HidePrompt() => SetActive(promptRoot, false);

    public void ShowBanner(string text)
    {
        if (bannerText != null)
            bannerText.text = text;
        SetActive(bannerRoot, true);
    }

    public void HideBanner() => SetActive(bannerRoot, false);

    void Update()
    {
        if (dialogueRoot == null || !dialogueRoot.activeSelf)
            return;

        // Real time: dialogue keeps its pace while the world is slowed
        lineTimer -= Time.unscaledDeltaTime;
        if (lineTimer <= 0f)
            NextLine();
    }

    void NextLine()
    {
        if (lines.Count == 0)
        {
            SetActive(dialogueRoot, false);
            return;
        }

        string line = lines.Dequeue();
        if (dialogueText != null)
            dialogueText.text = line;
        SetActive(dialogueRoot, true);
        lineTimer = secondsPerLine + line.Length * secondsPerCharacter;
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}
