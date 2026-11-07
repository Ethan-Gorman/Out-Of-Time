using UnityEngine;

public class SpriteTrigger : MonoBehaviour
{
    public Sprite newSprite;
    public Sprite oldSprite;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Player"))
        {

            spriteRenderer.sprite = newSprite;
        }
    }

    private void OnTriggerExit(Collider other) {

        if (other.CompareTag("Player")) {
            spriteRenderer.sprite = oldSprite;
        }
    }
}