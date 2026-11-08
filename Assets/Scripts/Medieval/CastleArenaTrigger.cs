using UnityEngine;

public class CastleArenaTrigger : MonoBehaviour
{
    [SerializeField] CastleAdventure adventure;
    void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<MedievalInteractor>() != null)
            adventure.EnterArena();
    }
}
