using UnityEngine;
using UnityEngine.Events;

// An invisible zone that shows dialogue and/or changes the objective the first time the player walks in.
// Needs a trigger collider on the same object.
[RequireComponent(typeof(Collider))]
public class StoryTrigger : MonoBehaviour
{
    [TextArea] public string[] lines;

    public string objective;

    public bool fireAfterCrystal = false;

    // On: these lines wait until the dialogue already on screen has finished, instead of cutting it off.
    // Use it for zones the player can reach while other lines are still showing (e.g. just after the intro)
    public bool waitForCurrentDialogue = false;

    public UnityEvent onFired;

    public bool HasFired => fired;

    bool fired;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (fired || other.attachedRigidbody == null || !other.attachedRigidbody.CompareTag("Player"))
            return;
        if (!fireAfterCrystal && MedievalLevel.Instance != null && MedievalLevel.Instance.HasCrystal)
            return;

        fired = true;
        onFired?.Invoke();
        var ui = MedievalUI.Instance;
        if (ui == null)
            return;
        if (waitForCurrentDialogue)
            ui.Queue(MedievalUI.SpeakerKind.Story, lines);
        else
            ui.Say(MedievalUI.SpeakerKind.Story, lines);
        if (!string.IsNullOrEmpty(objective))
            ui.SetObjective(objective);
    }
}
