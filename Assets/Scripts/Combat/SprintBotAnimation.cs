using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 스프린트 봇(돌격봇) 스프라이트 애니메이션.
/// 플레이어(PlayerSpriteAnimator)와 같은 방식: Resources/SprintSprites 의 개별 프레임(prefix_NN)을 불러와 상태별로 재생한다.
///   walk 8 / scan 4 / run 6 / stop 6 / hit 1
/// BotAI 가 Play(State) 로 상태를 지정한다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class SprintBotAnimation : MonoBehaviour
{
    public enum State { Walk, Scan, Run, Stop, Hit }

    [Header("Sprite Resource Settings")]
    [SerializeField] private string resourcesFolder = "SprintSprites";
    [SerializeField, Min(1)] private int walkFrameCount = 8;
    [SerializeField, Min(1)] private int scanFrameCount = 4;
    [SerializeField, Min(1)] private int runFrameCount = 6;
    [SerializeField, Min(1)] private int stopFrameCount = 6;

    [Header("Animation Speed (FPS)")]
    [SerializeField] private float walkFps = 10f;
    [SerializeField] private float scanFps = 12f;
    [SerializeField] private float runFps = 14f;
    [SerializeField] private float stopFps = 12f;
    [SerializeField] private float hitDuration = 0.22f;

    private SpriteRenderer spriteRenderer;
    private Sprite[] walkFrames, scanFrames, runFrames, stopFrames;
    private Sprite hitFrame;

    private State state = State.Walk;
    private State stateBeforeHit = State.Walk;
    private int frameIndex;
    private float frameTimer;
    private float hitTimer;
    private float currentHitDuration;
    private float fpsOverride;
    private bool loaded;

    public State Current => state;

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        Init();
        ApplyPreviewSpriteIfNeeded();
    }

    private void OnValidate()
    {
        loaded = false;
        Init();
        ApplyPreviewSpriteIfNeeded();
    }

    private void Init()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureLoaded();
        currentHitDuration = hitDuration;
        if (Application.isPlaying && spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            EnterState(state);
        }
    }

    private void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        walkFrames = LoadFrames("walk", walkFrameCount);
        scanFrames = LoadFrames("scan", scanFrameCount);
        runFrames = LoadFrames("run", runFrameCount);
        stopFrames = LoadFrames("stop", stopFrameCount);
        hitFrame = Resources.Load<Sprite>(resourcesFolder + "/hit_01");

        if (walkFrames[0] == null)
            Debug.LogWarning("SprintBotAnimation: Resources/" + resourcesFolder + " 에서 프레임을 찾지 못했습니다.", this);
    }

    private Sprite[] LoadFrames(string prefix, int count)
    {
        Sprite[] frames = new Sprite[Mathf.Max(1, count)];
        for (int i = 0; i < frames.Length; i++)
            frames[i] = Resources.Load<Sprite>(resourcesFolder + "/" + prefix + "_" + (i + 1).ToString("00"));
        return frames;
    }

    /// <summary>에디터(재생 전)에서도 기본 스프라이트(walk_01)를 보여준다.</summary>
    public void ApplyPreviewSpriteIfNeeded()
    {
        if (Application.isPlaying) return;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || walkFrames == null || walkFrames[0] == null) return;

        bool placeholder = spriteRenderer.sprite == null
            || spriteRenderer.sprite.name == "Square"
            || spriteRenderer.sprite.name == "White";
        if (!placeholder) return;

        spriteRenderer.sprite = walkFrames[0];
        spriteRenderer.color = Color.white;
#if UNITY_EDITOR
        EditorUtility.SetDirty(spriteRenderer);
#endif
    }

    /// <summary>
    /// 상태 재생. 같은 상태를 다시 호출하면 무시한다. 피격(Hit) 중에는 끝난 뒤 돌아갈 상태만 갱신한다.
    /// oneShotDuration &gt; 0 이면 Scan/Stop 을 그 시간 안에 끝나도록 fps 를 맞춘다.
    /// </summary>
    public void Play(State next, float oneShotDuration = 0f)
    {
        if (state == State.Hit && next != State.Hit)
        {
            stateBeforeHit = next;
            fpsOverride = FpsForDuration(next, oneShotDuration);
            return;
        }

        if (state == next && next != State.Hit) return;

        fpsOverride = FpsForDuration(next, oneShotDuration);
        EnterState(next);
    }

    public void PlayHit()
    {
        PlayHit(hitDuration);
    }

    public void PlayHit(float duration)
    {
        if (state != State.Hit) stateBeforeHit = state;
        currentHitDuration = Mathf.Max(0.05f, duration);
        EnterState(State.Hit);
    }

    private float FpsForDuration(State s, float duration)
    {
        if (duration <= 0f) return 0f;
        Sprite[] frames = GetFrames(s);
        return frames == null ? 0f : frames.Length / duration;
    }

    private void EnterState(State next)
    {
        state = next;
        frameIndex = 0;
        frameTimer = 0f;
        hitTimer = 0f;
        ApplyFrame();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (state == State.Hit)
        {
            hitTimer += Time.deltaTime;
            if (hitTimer >= currentHitDuration) EnterState(stateBeforeHit);
            return;
        }

        Sprite[] frames = GetFrames(state);
        if (frames == null || frames.Length <= 1) return;

        float step = 1f / Mathf.Max(1f, fpsOverride > 0f ? fpsOverride : GetFps(state));
        frameTimer += Time.deltaTime;
        while (frameTimer >= step)
        {
            frameTimer -= step;
            bool oneShot = state == State.Scan || state == State.Stop;
            if (oneShot)
            {
                if (frameIndex < frames.Length - 1) frameIndex++;
            }
            else
            {
                frameIndex = (frameIndex + 1) % frames.Length;
            }
            ApplyFrame();
        }
    }

    private void ApplyFrame()
    {
        if (spriteRenderer == null) return;

        if (state == State.Hit)
        {
            if (hitFrame != null) spriteRenderer.sprite = hitFrame;
            return;
        }

        Sprite[] frames = GetFrames(state);
        if (frames == null || frames.Length == 0) return;
        Sprite s = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        if (s != null) spriteRenderer.sprite = s;
    }

    private Sprite[] GetFrames(State s)
    {
        switch (s)
        {
            case State.Scan: return scanFrames;
            case State.Run: return runFrames;
            case State.Stop: return stopFrames;
            default: return walkFrames;
        }
    }

    private float GetFps(State s)
    {
        switch (s)
        {
            case State.Scan: return scanFps;
            case State.Run: return runFps;
            case State.Stop: return stopFps;
            default: return walkFps;
        }
    }
}
