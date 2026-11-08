using UnityEngine;

// Base class for things the player uses with the Interact button (E / gamepad north):
// levers, chests, the time machine. Needs a trigger collider on the same object for the "in range" zone.
// Keep solid colliders and art on child objects so the designer can swap the art freely.
public abstract class Interactable : MonoBehaviour
{
    [SerializeField] protected string prompt = "Interact";

    // Text shown after the key hint, e.g. "[E] Pull lever"
    public virtual string Prompt => prompt;

    // False hides the prompt and ignores presses (e.g. a lever that's already pulled)
    public virtual bool CanInteract => true;

    public abstract void Interact(MedievalInteractor interactor);

    // Outline (InteractOutline): glowing while usable, grey once used if it stays in the world.
    // Override to hide it instead once used (things that break or die), to only show it while it is the selected
    // one (villagers), or to outline it red (enemies)
    public virtual bool KeepUsedOutline => true;
    public virtual bool OutlineOnlyInRange => false;
    public virtual bool IsEnemy => false;

    protected virtual void OnTriggerEnter(Collider other)
    {
        var interactor = FindInteractor(other);
        if (interactor != null)
            interactor.Enter(this);
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        var interactor = FindInteractor(other);
        if (interactor != null)
            interactor.Exit(this);
    }

    static MedievalInteractor FindInteractor(Collider other)
    {
        return other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<MedievalInteractor>() : null;
    }
}
