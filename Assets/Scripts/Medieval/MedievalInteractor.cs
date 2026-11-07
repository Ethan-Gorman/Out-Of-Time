using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Lets the player use Interactables. Shows "[E] ..." for the nearest one in range and
// uses it when Interact is pressed (project-wide "Player/Interact": E / gamepad north).
// Sits on MedievalPlayer.
public class MedievalInteractor : MonoBehaviour
{
    readonly List<Interactable> nearby = new List<Interactable>();
    InputAction interact;
    string shownPrompt;

    void Awake()
    {
        interact = InputSystem.actions.FindAction("Player/Interact");
    }

    public void Enter(Interactable target)
    {
        if (!nearby.Contains(target))
            nearby.Add(target);
    }

    public void Exit(Interactable target)
    {
        nearby.Remove(target);
    }

    void OnDisable()
    {
        ShowPrompt(null);
    }

    void Update()
    {
        nearby.RemoveAll(i => i == null || !i.isActiveAndEnabled);

        // Closest usable one wins
        Interactable best = null;
        float bestDistance = float.MaxValue;
        foreach (var candidate in nearby)
        {
            if (!candidate.CanInteract)
                continue;
            float d = (candidate.transform.position - transform.position).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }

        ShowPrompt(best != null ? $"[E]  {best.Prompt}" : null);

        // WasPressedThisFrame reacts to the press itself, so the action's Hold interaction doesn't delay it
        bool pressed = interact != null ? interact.WasPressedThisFrame()
                                        : Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (pressed && best != null)
            best.Interact(this);
    }

    void ShowPrompt(string text)
    {
        if (text == shownPrompt)
            return;
        shownPrompt = text;

        var ui = MedievalUI.Instance;
        if (ui == null)
            return;
        if (string.IsNullOrEmpty(text))
            ui.HidePrompt();
        else
            ui.ShowPrompt(text);
    }
}
