using UnityEngine;

public class InteractionZone : MonoBehaviour
{
    public GameObject interactionPanel;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            interactionPanel.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            interactionPanel.SetActive(false);
        }
    }
}