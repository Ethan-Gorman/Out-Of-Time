using UnityEngine;

// Hurts the player on touch. Put it on a trigger collider: an axe blade, a bed of spikes.
// The player's invulnerability after a hit stops it from draining health every frame.
[RequireComponent(typeof(Collider))]
public class Hazard : MonoBehaviour
{
    public float damage = 25f;

    [Tooltip("Off = harmless. SpikeTrap turns this on and off as the spikes rise and sink.")]
    public bool armed = true;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other) => TryHit(other);
    void OnTriggerStay(Collider other) => TryHit(other);

    void TryHit(Collider other)
    {
        if (!armed || !isActiveAndEnabled || other.attachedRigidbody == null)
            return;

        var health = other.attachedRigidbody.GetComponent<MedievalHealth>();
        if (health != null)
            health.Damage(damage, transform.position);
    }
}
