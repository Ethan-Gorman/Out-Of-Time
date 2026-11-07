using UnityEngine;

// An invisible zone that shows dialogue and/or changes the objective the first time the player walks in.
// Needs a trigger collider on the same object.
[RequireComponent(typeof(Collider))]
public class StoryTrigger : MonoBehaviour
{
    [TextArea] public string[] lines;

    public string objective;

    public bool fireAfterCrystal = false;

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
        var ui = MedievalUI.Instance;
        if (ui == null)
            return;
        ui.Say(lines);
        if (!string.IsNullOrEmpty(objective))
            ui.SetObjective(objective);
    }
}
