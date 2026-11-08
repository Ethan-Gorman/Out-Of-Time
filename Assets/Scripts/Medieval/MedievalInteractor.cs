using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Lets the player use Interactables. Shows "[E] ..." for the nearest one in range and
// uses it when Interact is pressed (project-wide "Player/Interact": E / gamepad north).
// Sits on MedievalPlayer.
public class MedievalInteractor : MonoBehaviour
{
    // The player's interactor, for outlines that react to range and selection
    public static MedievalInteractor Current { get; private set; }

    // The one Interact would use right now (closest usable in range), or null
    public Interactable Selected { get; private set; }

    readonly List<Interactable> nearby = new List<Interactable>();
    InputAction interact;
    string shownPrompt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Current = null;
    }

    public bool IsNearby(Interactable target) => nearby.Contains(target);

    void Awake()
    {
        interact = InputSystem.actions.FindAction("Player/Interact");
    }

    void OnEnable()
    {
        Current = this;
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
        Selected = null;
        if (Current == this)
            Current = null;
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

        Selected = best;
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
