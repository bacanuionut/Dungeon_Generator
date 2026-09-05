using UnityEngine;

/// <summary>
/// Lightweight frame-by-frame sprite animation used by procedural
/// environmental effects such as wall flames.
///
/// It avoids creating an Animator Controller for every generated torch and
/// allows each instance to start on a different deterministic frame and run
/// at a slightly different speed.
/// </summary>
public class EnvironmentSpriteAnimator : MonoBehaviour
{
    private SpriteRenderer targetRenderer;
    private Sprite[] frames;
    private float framesPerSecond;
    private float speedScale;
    private int startingFrame;
    private float elapsedTime;
    private int displayedFrame = -1;


    public void Initialise(
        SpriteRenderer renderer,
        Sprite[] animationFrames,
        float animationFramesPerSecond,
        int startFrame,
        float animationSpeedScale)
    {
        targetRenderer = renderer;
        frames = animationFrames;
        framesPerSecond = Mathf.Max(0.01f, animationFramesPerSecond);
        speedScale = Mathf.Max(0.01f, animationSpeedScale);
        startingFrame = Mathf.Max(0, startFrame);
        elapsedTime = 0f;

        if (targetRenderer == null ||
            frames == null ||
            frames.Length == 0)
        {
            enabled = false;
            return;
        }

        startingFrame %= frames.Length;
        displayedFrame = startingFrame;
        targetRenderer.sprite = frames[displayedFrame];
    }


    private void Update()
    {
        if (targetRenderer == null ||
            frames == null ||
            frames.Length == 0)
        {
            return;
        }

        elapsedTime += Time.deltaTime * speedScale;

        int frameOffset = Mathf.FloorToInt(
            elapsedTime * framesPerSecond
        );

        int frameIndex =
            (startingFrame + frameOffset) % frames.Length;

        if (frameIndex == displayedFrame)
            return;

        displayedFrame = frameIndex;
        targetRenderer.sprite = frames[displayedFrame];
    }
}
