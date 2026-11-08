using UnityEngine;

public class CastleGuide : Interactable
{
    [SerializeField] CastleAdventure adventure;
    public override string Prompt => "Talk to the castle warden";
    public override bool CanInteract => adventure != null && adventure.Stage != CastleAdventure.QuestStage.Arrival;
    public override void Interact(MedievalInteractor interactor)
    {
        if (CanInteract) adventure.TellStory();
    }
}
