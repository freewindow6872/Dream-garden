using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class DroneBotAnimation : MonoBehaviour
{
    public enum State { Fly, Scan, Attack, Hit, Walk, Sit, Stand, Idle }

    [Header("Animation Speed (frames per second)")]
    [SerializeField] private float flyFrameRate = 8f;
    [Tooltip("인식 연출(3프레임). TestBotAI 의 recognitionDelay(0.35초)에 맞춰 약 9fps")]
    [SerializeField] private float scanFrameRate = 9f;
    [SerializeField] private float attackFrameRate = 10f;

    [Header("Sniper")]
    [Tooltip("체크하면 SniperSprites(스나이퍼 봇) 애니메이션을 사용합니다. 프리팹에 미리 체크해 두면 에디터에서도 바로 보입니다.")]
    [SerializeField] private bool sniperMode;

#if UNITY_EDITOR
    [Header("Editor Preview (에디터에서 재생 없이 미리보기)")]
    [SerializeField] private bool previewInEditor = true;
    [SerializeField] private State previewState = State.Idle;
#endif

    [Header("Hit")]
    [Tooltip("기본 피격 프레임 유지 시간(초)")]
    [SerializeField] private float hitDuration = 0.28f;

    private const string ResourceFolder = "DroneSprites/";

    private SpriteRenderer spriteRenderer;
    private Sprite[] flyFrames, scanFrames, attackFrames;
    private Sprite hitFrame;
    private Sprite[] sniperWalkFrames, sniperScanFrames, sniperSitFrames, sniperStandFrames, sniperAttackFrames, sniperIdleFrames;
    private Sprite sniperHitFrame;

    private State state = State.Fly;
    private State stateBeforeHit = State.Fly;
    private int frameIndex;
    private float frameTimer;
    private float currentHitDuration;
    private bool loaded;

    private void Awake()
    {
        if (!Application.isPlaying) return; // 에디터 미리보기는 OnEnable 쪽에서 처리

        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHitDuration = hitDuration;
        spriteRenderer.color = Color.white;
        if (sniperMode)
        {
            LoadSniperFrames();
            EnterState(State.Walk);
        }
        else
        {
            LoadFrames();
            EnterState(State.Fly);
        }
    }

    private void LoadFrames()
    {
        if (loaded) return;
        loaded = true;

        flyFrames = LoadFrames("fly", 4);
        scanFrames = LoadFrames("scan", 3);
        attackFrames = LoadFrames("attack", 4);
        hitFrame = Resources.Load<Sprite>(ResourceFolder + "hit_01");

        if (!sniperMode && flyFrames[0] == null)
            Debug.LogWarning("DroneBotAnimation: Resources/DroneSprites 에서 프레임을 찾지 못했습니다.", this);
    }

    private static Sprite[] LoadFrames(string prefix, int count)
    {
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++)
            frames[i] = Resources.Load<Sprite>(ResourceFolder + prefix + "_" + (i + 1).ToString("00"));
        return frames;
    }

    public void SetSniperMode()
    {
        LoadSniperFrames();
        sniperMode = true;
        Play(State.Walk);
    }

    private void LoadSniperFrames()
    {
        if (sniperWalkFrames != null) return;
        // 시트별 칸 수는 .meta 의 슬라이스(Sprite Editor)를 따른다.
        // idel 4 / scan 4 / sit 6 / stand 3 / walk 8 / attack 8 / hit 1
        sniperWalkFrames = LoadSheet("sinper_bot_walk");
        sniperScanFrames = LoadSheet("sinper_bot_scan");
        sniperSitFrames = LoadSheet("sinper_bot_sit");
        sniperStandFrames = LoadSheet("sinper_bot_stand");
        sniperAttackFrames = LoadSheet("sinper_bot_attack");
        sniperIdleFrames = LoadSheet("sinper_bot_idel");
        Sprite[] hit = LoadSheet("sinper_bot_hit");
        sniperHitFrame = hit.Length > 0 ? hit[0] : null;
    }

    /// <summary>슬라이스된 스프라이트(이름 끝 _숫자 순서)를 불러온다.</summary>
    private static Sprite[] LoadSheet(string resourceName)
    {
        Sprite[] loaded = Resources.LoadAll<Sprite>("SniperSprites/" + resourceName);
        if (loaded == null || loaded.Length == 0)
        {
            Debug.LogWarning("DroneBotAnimation: Resources/SniperSprites/" + resourceName + " 스프라이트를 찾지 못했습니다.");
            return new Sprite[0];
        }
        System.Array.Sort(loaded, (x, y) => SpriteIndex(x).CompareTo(SpriteIndex(y)));
        return loaded;
    }

    private static int SpriteIndex(Sprite sprite)
    {
        string n = sprite.name;
        int underscore = n.LastIndexOf('_');
        int index;
        return underscore >= 0 && int.TryParse(n.Substring(underscore + 1), out index) ? index : 0;
    }

    public void Play(State next)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (sniperMode) LoadSniperFrames(); else LoadFrames();

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
        if (!Application.isPlaying) return;
        Tick(Time.deltaTime);
    }

    private void Tick(float deltaTime)
    {
        if (state == State.Hit)
        {
            frameTimer += deltaTime;
            if (frameTimer >= currentHitDuration) EnterState(stateBeforeHit);
            return;
        }

        Sprite[] frames = CurrentFrames();
        if (frames == null || frames.Length <= 1) return;

        float step = 1f / Mathf.Max(1f, CurrentFps());
        frameTimer += deltaTime;
        while (frameTimer >= step)
        {
            frameTimer -= step;
            if (state == State.Scan || (sniperMode && (state == State.Sit || state == State.Stand)))
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
        if (sniperMode)
        {
            switch (state)
            {
                case State.Walk: return sniperWalkFrames;
                case State.Scan: return sniperScanFrames;
                case State.Sit: return sniperSitFrames;
                case State.Stand: return sniperStandFrames;
                case State.Attack: return sniperAttackFrames;
                case State.Idle: return sniperIdleFrames;
                default: return sniperWalkFrames;
            }
        }
        switch (state)
        {
            case State.Scan: return scanFrames;
            case State.Attack: return attackFrames;
            default: return flyFrames;
        }
    }

    private float CurrentFps()
    {
        if (sniperMode)
        {
            if (state == State.Attack) return 10f;
            if (state == State.Walk) return 8f;
            return 6f;
        }
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
            Sprite hit = sniperMode && sniperHitFrame != null ? sniperHitFrame : hitFrame;
            if (hit != null) spriteRenderer.sprite = hit;
            return;
        }

        Sprite[] frames = CurrentFrames();
        if (frames == null || frames.Length == 0) return;

        int index = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
        if (frames[index] != null) spriteRenderer.sprite = frames[index];
    }

#if UNITY_EDITOR
    // ---------------------------------------------------------------- Editor preview
    private Sprite editorOriginalSprite;
    private double editorLastTime;
    private bool editorPreviewing;
    private float editorLoopTimer;

    private void OnEnable()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.update += EditorTick;
        editorLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        BeginEditorPreview();
    }

    private void OnDisable()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.update -= EditorTick;
        EndEditorPreview();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        // OnValidate 안에서는 에셋/오브젝트 변경이 제한되므로 한 틱 뒤에 반영
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            EndEditorPreview();
            BeginEditorPreview();
        };
    }

    private void BeginEditorPreview()
    {
        if (!previewInEditor || !sniperMode) return;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;
        if (!editorPreviewing) editorOriginalSprite = spriteRenderer.sprite;
        editorPreviewing = true;
        LoadSniperFrames();
        currentHitDuration = hitDuration;
        editorLoopTimer = 0f;
        EnterState(previewState);
    }

    private void EndEditorPreview()
    {
        if (!editorPreviewing) return;
        editorPreviewing = false;
        if (spriteRenderer != null) spriteRenderer.sprite = editorOriginalSprite;
    }

    private void EditorTick()
    {
        if (this == null || Application.isPlaying || !editorPreviewing) return;
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - editorLastTime), 0.1f);
        editorLastTime = now;

        State before = state;
        Sprite beforeSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        Tick(dt);
        if (state != previewState) EnterState(previewState); // 피격 등으로 상태가 바뀌어도 미리보기 상태 유지

        // 한 번만 재생되는 상태(Scan/Sit/Stand)는 1.5초마다 처음부터 다시 보여준다
        editorLoopTimer += dt;
        if (IsOneShot(previewState) && editorLoopTimer > 1.5f)
        {
            editorLoopTimer = 0f;
            EnterState(previewState);
        }

        if (spriteRenderer != null && (before != state || beforeSprite != spriteRenderer.sprite))
            UnityEditor.SceneView.RepaintAll();
    }

    private static bool IsOneShot(State s)
    {
        return s == State.Scan || s == State.Sit || s == State.Stand;
    }
#endif
}
