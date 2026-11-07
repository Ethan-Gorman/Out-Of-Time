using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public float timeValue = 10f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Clock.Instance.AddTime(timeValue);
            Destroy(gameObject);
        }
    }
}