using UnityEngine;

// Hurts the player on touch: costs clock time via PlayerDamage. Put it on a trigger collider: an axe blade, a bed of spikes.
// The player's invulnerability after a hit stops it from draining time every frame.
[RequireComponent(typeof(Collider))]
public class Hazard : MonoBehaviour
{
    [Tooltip("Seconds taken off the clock. Below 0 = the player's default (PlayerDamage.secondsPerHit)")]
    public float timeCost = -1f;

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

        var player = other.attachedRigidbody.GetComponent<PlayerDamage>();
        if (player != null)
            player.Hit(transform.position, timeCost);
    }
}
