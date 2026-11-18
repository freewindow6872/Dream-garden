using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DroneBotAnimation : MonoBehaviour
{
    public enum State { Fly, Scan, Attack, Hit }

    [Header("Animation Speed (frames per second)")]
    [SerializeField] private float flyFrameRate = 8f;
    [Tooltip("인식 연출(3프레임). TestBotAI 의 recognitionDelay(0.35초)에 맞춰 약 9fps")]
    [SerializeField] private float scanFrameRate = 9f;
    [SerializeField] private float attackFrameRate = 10f;

    [Header("Hit")]
    [Tooltip("기본 피격 프레임 유지 시간(초)")]
    [SerializeField] private float hitDuration = 0.28f;

    private const string ResourceFolder = "DroneSprites/";

    private SpriteRenderer spriteRenderer;
    private Sprite[] flyFrames, scanFrames, attackFrames;
    private Sprite hitFrame;

    private State state = State.Fly;
    private State stateBeforeHit = State.Fly;
    private int frameIndex;
    private float frameTimer;
    private float currentHitDuration;
    private bool loaded;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHitDuration = hitDuration;
        LoadFrames();
        spriteRenderer.color = Color.white;
        EnterState(State.Fly);
    }

    private void LoadFrames()
    {
        if (loaded) return;
        loaded = true;

        flyFrames = LoadFrames("fly", 4);
        scanFrames = LoadFrames("scan", 3);
        attackFrames = LoadFrames("attack", 4);
        hitFrame = Resources.Load<Sprite>(ResourceFolder + "hit_01");

        if (flyFrames[0] == null)
            Debug.LogWarning("DroneBotAnimation: Resources/DroneSprites 에서 프레임을 찾지 못했습니다.", this);
    }

    private static Sprite[] LoadFrames(string prefix, int count)
    {
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++)
            frames[i] = Resources.Load<Sprite>(ResourceFolder + prefix + "_" + (i + 1).ToString("00"));
        return frames;
    }

    public void Play(State next)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        LoadFrames();

        if (state == State.Hit && next != State.Hit)
        {
            stateBeforeHit = next;
            return;
        }

        if (state == next && next != State.Hit) return;

        EnterState(next);
    }

    private void EnterState(State next)
    {
        state = next;
        frameIndex = 0;
        frameTimer = 0f;
        ApplyFrame();
    }

    public void PlayHit()
    {
        PlayHit(hitDuration);
    }

    public void PlayHit(float duration)
    {
        if (state != State.Hit) stateBeforeHit = state;
        currentHitDuration = Mathf.Max(0.05f, duration);
        Play(State.Hit);
    }

    private void Update()
    {
        if (state == State.Hit)
        {
            frameTimer += Time.deltaTime;
            if (frameTimer >= currentHitDuration) EnterState(stateBeforeHit);
            return;
        }

        Sprite[] frames = CurrentFrames();
        if (frames == null || frames.Length <= 1) return;

        float step = 1f / Mathf.Max(1f, CurrentFps());
        frameTimer += Time.deltaTime;
        while (frameTimer >= step)
        {
            frameTimer -= step;
            if (state == State.Scan)
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

    private Sprite[] CurrentFrames()
    {
        switch (state)
        {
            case State.Scan: return scanFrames;
            case State.Attack: return attackFrames;
            default: return flyFrames;
        }
    }

    private float CurrentFps()
    {
        switch (state)
        {
            case State.Scan: return scanFrameRate;
            case State.Attack: return attackFrameRate;
            default: return flyFrameRate;
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

        Sprite[] frames = CurrentFrames();
        if (frames == null || frames.Length == 0) return;

        int index = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
        if (frames[index] != null) spriteRenderer.sprite = frames[index];
    }
}