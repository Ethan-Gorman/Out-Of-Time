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
    public float secondsPerLine = 1.8f;
    public float secondsPerCharacter = 0.035f;

    [Header("Banner (centre)")]
    public GameObject bannerRoot;
    public Text bannerText;

    // Who a line is from. Npc and System lines show the speaker's name; Thought and Story lines are the
    // player's own voice / narration and show in italics, slightly dimmer
    public enum SpeakerKind { Npc, Thought, Story, System }

    struct Line
    {
        public SpeakerKind kind;
        public string speaker;
        public string text;
    }

    // Two channels: self-dialogue (thoughts, story, system lines from Say/Queue) and NPC conversation (Talk).
    // An NPC talking pauses the self-dialogue; it resumes where it left off once the NPC is done
    readonly Queue<Line> lines = new Queue<Line>();
    readonly Queue<Line> npcLines = new Queue<Line>();
    float lineTimer;
    bool showingNpc;
    Object npcSource;
    Line current;
    bool hasPaused;
    Line pausedLine;
    float pausedTimer;
    Text speakerText;

    // The objective panel's designed height, and how tall its text area is at that height.
    // Longer objectives (two lines, long words) grow the panel so the text never spills past the border
    float objectiveBaseHeight = -1f;
    float objectiveBaseTextHeight;

    void Awake()
    {
        Instance = this;
        if (objectiveRoot != null && objectiveText != null)
        {
            objectiveBaseHeight = ((RectTransform)objectiveRoot.transform).sizeDelta.y;
            // The text is stretched across the panel, so its height is the panel's minus its insets
            objectiveBaseTextHeight = objectiveBaseHeight + objectiveText.rectTransform.sizeDelta.y;
        }
        CreateSpeakerPlate();
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
        FitObjective();
    }

    // Grows (or shrinks back) the objective panel to fit its text
    void FitObjective()
    {
        if (objectiveBaseHeight < 0f || objectiveText == null || string.IsNullOrEmpty(objectiveText.text))
            return;
        var panel = (RectTransform)objectiveRoot.transform;
        float extra = Mathf.Max(0f, objectiveText.preferredHeight - objectiveBaseTextHeight);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, objectiveBaseHeight + Mathf.Ceil(extra) + (extra > 0f ? 6f : 0f));
    }

    // Shows lines of dialogue one after another, a few seconds each.
    // New dialogue replaces whatever was still waiting, so lines never lag behind what just happened
    // Lines starting "NAME: " (e.g. "SYSTEM: ...", "Castle Warden: ...") show NAME on the name plate;
    // other lines are the player's thoughts
    public void Say(params string[] newLines) => Say(SpeakerKind.Thought, newLines);

    // Say with a kind for lines that have no "NAME: " of their own (e.g. Story for narration).
    // While an NPC is talking, these wait behind the conversation instead of replacing it
    public void Say(SpeakerKind kind, params string[] newLines)
    {
        if (newLines == null || newLines.Length == 0)
            return;

        if (InNpcConversation)
        {
            Enqueue(lines, kind, null, newLines);
            return;
        }

        lines.Clear();
        Enqueue(lines, kind, null, newLines);
        NextLine();
    }

    // Like Say, but waits for the lines already showing or waiting, then shows these after them.
    // For story beats that may fire while other dialogue is still on screen (e.g. right after the intro)
    public void Queue(params string[] moreLines) => Queue(SpeakerKind.Thought, moreLines);

    public void Queue(SpeakerKind kind, params string[] moreLines)
    {
        if (moreLines == null || moreLines.Length == 0)
            return;

        Enqueue(lines, kind, null, moreLines);

        if (!IsTalking)
            NextLine();
    }

    // An NPC speaks (source is the NPC, so walking away from it can end the conversation).
    // Pauses any thought/story line on screen; it comes back after the NPC's lines
    public void Talk(Object source, string speaker, params string[] npcLinesToSay)
    {
        if (npcLinesToSay == null || npcLinesToSay.Length == 0)
            return;

        if (!showingNpc && IsTalking && !hasPaused)
        {
            pausedLine = current;
            pausedTimer = lineTimer;
            hasPaused = true;
        }

        npcSource = source;
        npcLines.Clear();
        Enqueue(npcLines, SpeakerKind.Npc, speaker, npcLinesToSay);
        NextLine();
    }

    // Ends the NPC conversation early (the player walked away); paused self-dialogue resumes.
    // Only if that NPC is the one talking
    public void EndTalk(Object source)
    {
        if (!InNpcConversation || source != npcSource)
            return;
        npcLines.Clear();
        NextLine();
    }

    // True while a line of dialogue is on screen
    public bool IsTalking => dialogueRoot != null && dialogueRoot.activeSelf;

    // True while an NPC's lines are showing or waiting
    public bool InNpcConversation => (showingNpc && IsTalking) || npcLines.Count > 0;

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
        if (npcLines.Count > 0)
        {
            showingNpc = true;
            Show(npcLines.Dequeue(), -1f);
            return;
        }

        showingNpc = false;
        npcSource = null;
        if (hasPaused)
        {
            hasPaused = false;
            // Give the resumed line a moment back on screen even if it had nearly finished
            Show(pausedLine, Mathf.Max(pausedTimer, secondsPerLine));
            return;
        }

        if (lines.Count == 0)
        {
            SetActive(dialogueRoot, false);
            return;
        }

        Show(lines.Dequeue(), -1f);
    }

    // Shows one line with its name plate. time < 0 uses the usual time for its length
    void Show(Line line, float time)
    {
        current = line;
        bool own = line.kind == SpeakerKind.Thought || line.kind == SpeakerKind.Story;
        if (dialogueText != null)
        {
            if (own)
            {
                Color dim = dialogueText.color;
                dim.a *= 0.75f;
                dialogueText.text = $"<color=#{ColorUtility.ToHtmlStringRGBA(dim)}><i>{line.text}</i></color>";
            }
            else
                dialogueText.text = line.text;
        }

        if (speakerText != null)
        {
            string plate = line.kind == SpeakerKind.Thought ? "Thought"
                : line.kind == SpeakerKind.Story ? "Story"
                : !string.IsNullOrEmpty(line.speaker) ? line.speaker
                : line.kind == SpeakerKind.System ? "SYSTEM" : "";
            // Same font and colour as the dialogue, so the UI theme's styling carries over
            if (dialogueText != null)
            {
                speakerText.font = dialogueText.font;
                speakerText.fontSize = Mathf.Max(10, Mathf.RoundToInt(dialogueText.fontSize * 0.75f));
                Color c = dialogueText.color;
                if (own)
                    c.a *= 0.75f;
                speakerText.color = c;
            }
            speakerText.text = own ? $"<i>{plate}</i>" : plate;
            speakerText.gameObject.SetActive(!string.IsNullOrEmpty(plate));
        }

        SetActive(dialogueRoot, true);
        lineTimer = time >= 0f ? time : secondsPerLine + line.text.Length * secondsPerCharacter;
    }

    // Adds lines to a channel, reading a "NAME: " start as the speaker
    static void Enqueue(Queue<Line> channel, SpeakerKind kind, string speaker, string[] newLines)
    {
        foreach (var text in newLines)
            if (!string.IsNullOrEmpty(text))
                channel.Enqueue(Parse(text, kind, speaker));
    }

    static Line Parse(string text, SpeakerKind kind, string speaker)
    {
        var line = new Line { kind = kind, speaker = speaker, text = text };
        int colon = text.IndexOf(": ");
        if (colon <= 0 || colon > 24 || !char.IsUpper(text[0]))
            return line;
        string name = text.Substring(0, colon);
        // Every word capitalised ("Castle Warden"), so narration like "Next stop: ..." stays plain text
        int spaces = 0;
        for (int i = 0; i < name.Length; i++)
        {
            char ch = name[i];
            if (ch == ' ')
            {
                spaces++;
                if (i + 1 >= name.Length || !char.IsUpper(name[i + 1]))
                    return line;
            }
            else if (!char.IsLetterOrDigit(ch))
                return line;
        }
        if (spaces > 2)
            return line;

        line.text = text.Substring(colon + 2);
        string upper = name.ToUpperInvariant();
        if (upper == "PLAYER")
        {
            line.kind = SpeakerKind.Thought;
            line.speaker = null;
        }
        else
        {
            line.kind = upper == "SYSTEM" ? SpeakerKind.System : SpeakerKind.Npc;
            line.speaker = name;
        }
        return line;
    }

    // A small name label on the top-left edge of the dialogue panel, made at runtime so the prefab stays as is
    void CreateSpeakerPlate()
    {
        if (dialogueRoot == null || dialogueText == null)
            return;
        var go = new GameObject("SpeakerName", typeof(RectTransform));
        go.layer = dialogueRoot.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(dialogueRoot.transform, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(12f, 2f);
        rect.sizeDelta = new Vector2(-24f, 30f);
        speakerText = go.AddComponent<Text>();
        speakerText.font = dialogueText.font;
        speakerText.alignment = TextAnchor.LowerLeft;
        speakerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        speakerText.verticalOverflow = VerticalWrapMode.Overflow;
        speakerText.supportRichText = true;
        speakerText.raycastTarget = false;
        go.SetActive(false);
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}
