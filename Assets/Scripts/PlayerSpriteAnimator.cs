using UnityEngine;

/// <summary>Dream Garden character sprite animation without requiring an Animator Controller asset.</summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerController2D))]
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("Animation Speed (frames per second)")]
    [SerializeField] private float idleFps = 7f;
    [SerializeField] private float runFps = 12f;
    [SerializeField] private float dashFps = 12f;
    [SerializeField] private float moveThreshold = 0.15f;

    private SpriteRenderer spriteRenderer;
    private PlayerController2D controller;
    private Sprite[] idleFrames, runFrames, dashFrames;
    private int frameIndex;
    private float frameTimer;
    private AnimationState currentState;
    private enum AnimationState { Idle, Run, Dash }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        controller = GetComponent<PlayerController2D>();
        idleFrames = LoadFrames("idel", 5);
        runFrames = LoadFrames("run", 8);
        dashFrames = LoadFrames("dash", 2);
        SetState(AnimationState.Idle, true);
        spriteRenderer.color = Color.white;
    }

    private Sprite[] LoadFrames(string prefix, int count)
    {
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++)
            frames[i] = Resources.Load<Sprite>("PlayerSprites/" + prefix + "_" + (i + 1).ToString("00"));
        return frames;
    }

    private void Update()
    {
        AnimationState next = controller.IsDashing ? AnimationState.Dash :
            Mathf.Abs(controller.HorizontalSpeed()) > moveThreshold ? AnimationState.Run : AnimationState.Idle;
        if (next != currentState) SetState(next, true);

        Sprite[] frames = GetFrames(currentState);
        if (frames == null || frames.Length == 0) return;
        frameTimer += Time.deltaTime;
        float fps = currentState == AnimationState.Idle ? idleFps : currentState == AnimationState.Run ? runFps : dashFps;
        if (frameTimer >= 1f / Mathf.Max(1f, fps))
        {
            frameTimer = 0f;
            frameIndex = (frameIndex + 1) % frames.Length;
            if (frames[frameIndex] != null) spriteRenderer.sprite = frames[frameIndex];
        }
        if (Mathf.Abs(controller.HorizontalSpeed()) > moveThreshold && !controller.FacingLocked)
            spriteRenderer.flipX = controller.FacingDir < 0;
    }

    private void SetState(AnimationState state, bool reset)
    {
        currentState = state;
        if (reset) { frameIndex = 0; frameTimer = 0f; }
        Sprite[] frames = GetFrames(state);
        if (frames != null && frames.Length > 0 && frames[0] != null) spriteRenderer.sprite = frames[0];
    }

    private Sprite[] GetFrames(AnimationState state)
    {
        switch (state) { case AnimationState.Run: return runFrames; case AnimationState.Dash: return dashFrames; default: return idleFrames; }
    }
}
