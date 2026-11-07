using UnityEngine;

public class CavemanSpriteAnimation : MonoBehaviour
{
    public SpriteRenderer sprite;
    public Sprite[] frames;
    public float framesPerSecond = 8f;

    float frameTimer;
    int frameIndex;

    void Awake()
    {
        if (sprite == null)
            sprite = GetComponent<SpriteRenderer>();
        ApplyFrame();
    }

    void Update()
    {
        if (sprite == null || frames == null || frames.Length < 2 || framesPerSecond <= 0f)
            return;

        frameTimer += WorldTime.Delta;
        float frameDuration = 1f / framesPerSecond;
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % frames.Length;
            ApplyFrame();
        }
    }

    void ApplyFrame()
    {
        if (sprite != null && frames != null && frames.Length > 0)
            sprite.sprite = frames[frameIndex % frames.Length];
    }
}