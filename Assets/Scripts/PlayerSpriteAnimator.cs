using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// 플레이어 스프라이트 애니메이션 통합 담당.
/// [ExecuteAlways]를 통해 플레이 버튼을 누르지 않은 에디트 모드에서도 기본 스프라이트(idel_01)를 즉시 적용한다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerController2D))]
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("Sprite Resource Settings")]
    [SerializeField] private string resourcesFolder = "PlayerSprites";
    [SerializeField] private string idlePrefix = "idel";
    [SerializeField, Min(1)] private int idleFrameCount = 5;
    [SerializeField] private string runPrefix = "run";
    [SerializeField, Min(1)] private int runFrameCount = 8;
    [SerializeField] private string dashPrefix = "dash";
    [SerializeField, Min(1)] private int dashFrameCount = 2;
    [SerializeField] private string jumpPrefix = "jump";
    [SerializeField, Min(1)] private int jumpFrameCount = 2;
    [SerializeField] private string airPrefix = "air";
    [SerializeField, Min(1)] private int airFrameCount = 2;
    [SerializeField] private string fallPrefix = "fall";
    [SerializeField, Min(1)] private int fallFrameCount = 2;
    [SerializeField] private string attack1Prefix = "attack_1";
    [SerializeField, Min(1)] private int attack1FrameCount = 4;
    [SerializeField] private string attack2Prefix = "attack_2";
    [SerializeField, Min(1)] private int attack2FrameCount = 4;
    [SerializeField] private string hurtPrefix = "hurt";
    [SerializeField, Min(1)] private int hurtFrameCount = 1;
    [SerializeField] private string deathPrefix = "death";
    [SerializeField, Min(1)] private int deathFrameCount = 6;

    [Header("Animation Speed (FPS)")]
    [SerializeField] private float idleFps = 7f;
    [SerializeField] private float runFps = 12f;
    [SerializeField] private float dashFps = 12f;
    [SerializeField] private float jumpFps = 9f;
    [SerializeField] private float airFps = 5f;
    [SerializeField] private float fallFps = 9f;
    [SerializeField] private float hurtFps = 10f;
    [SerializeField] private float deathFps = 8f;

    [Header("State Thresholds & Timings")]
    [SerializeField] private float moveThreshold = 0.15f;
    [SerializeField] private float verticalStateThreshold = 0.5f;
    [SerializeField] private float hurtDuration = 0.25f;
    [SerializeField] private float defaultAttackDuration = 0.26f;
    [SerializeField] private int secondAttackComboIndex = 2;

    private SpriteRenderer spriteRenderer;
    private PlayerController2D controller;
    private PlayerCombat combat;
    private Health health;

    private Sprite[] idleFrames, runFrames, dashFrames;
    private Sprite[] jumpFrames, airFrames, fallFrames;
    private Sprite[] attack1Frames, attack2Frames, hurtFrames, deathFrames;

    private int frameIndex;
    private float frameTimer;
    private float oneShotDuration;
    private bool oneShotActive;
    private bool isDead;
    private bool isHurt;
    private float hurtTimer;

    private AnimationState currentState;
    private enum AnimationState
    {
        Idle, Run, Dash, Jump, Air, Fall, Attack, Hurt, Death
    }

    private void Awake()
    {
        InitializeReferencesAndFrames();
        ApplyPreviewSpriteIfNeeded();
    }

    private void OnEnable()
    {
        InitializeReferencesAndFrames();
        ApplyPreviewSpriteIfNeeded();

        if (!Application.isPlaying) return;

        if (combat != null) combat.AttackStarted += OnAttackStarted;
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;

        if (combat != null) combat.AttackStarted -= OnAttackStarted;
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
    }

    private void OnValidate()
    {
        InitializeReferencesAndFrames();
        ApplyPreviewSpriteIfNeeded();
    }

    private void InitializeReferencesAndFrames()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (controller == null) controller = GetComponent<PlayerController2D>();
        if (combat == null) combat = GetComponent<PlayerCombat>();
        if (health == null) health = GetComponent<Health>();

        if (idleFrames == null || idleFrames.Length != idleFrameCount)
            idleFrames = LoadFrames(idlePrefix, idleFrameCount);

        if (!Application.isPlaying) return;

        runFrames = LoadFrames(runPrefix, runFrameCount);
        dashFrames = LoadFrames(dashPrefix, dashFrameCount);
        jumpFrames = LoadFrames(jumpPrefix, jumpFrameCount);
        airFrames = LoadFrames(airPrefix, airFrameCount);
        fallFrames = LoadFrames(fallPrefix, fallFrameCount);
        attack1Frames = LoadFrames(attack1Prefix, attack1FrameCount);
        attack2Frames = LoadFrames(attack2Prefix, attack2FrameCount);
        hurtFrames = LoadFrames(hurtPrefix, hurtFrameCount);
        deathFrames = LoadFrames(deathPrefix, deathFrameCount);

        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
        SetState(AnimationState.Idle, true);
    }

    public void ApplyPreviewSpriteIfNeeded()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        if (idleFrames == null || idleFrames.Length == 0 || idleFrames[0] == null)
            idleFrames = LoadFrames(idlePrefix, idleFrameCount);

        if (idleFrames != null && idleFrames.Length > 0 && idleFrames[0] != null)
        {
            bool isPlaceholder = spriteRenderer.sprite == null
                || spriteRenderer.sprite.name == "Square"
                || spriteRenderer.sprite.name == "White";

            if (!Application.isPlaying && isPlaceholder)
            {
                spriteRenderer.sprite = idleFrames[0];
                spriteRenderer.color = Color.white;
#if UNITY_EDITOR
                EditorUtility.SetDirty(spriteRenderer);
                if (!EditorApplication.isPlayingOrWillChangePlaymode && gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            }
        }
    }

    private Sprite[] LoadFrames(string prefix, int count)
    {
        Sprite[] frames = new Sprite[Mathf.Max(1, count)];
        for (int i = 0; i < frames.Length; i++)
            frames[i] = Resources.Load<Sprite>(resourcesFolder + "/" + prefix + "_" + (i + 1).ToString("00"));
        return frames;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (isDead)
        {
            UpdateOneShot(deathFrames, deathFps, true);
            return;
        }

        if (isHurt)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer <= 0f)
            {
                isHurt = false;
                SetState(ResolveMovementState(), true);
            }
            else
            {
                UpdateOneShot(hurtFrames, hurtFps, true);
                return;
            }
        }

        if (combat != null && combat.IsAttacking)
        {
            if (!oneShotActive || currentState != AnimationState.Attack)
                SetState(AnimationState.Attack, true);

            UpdateTimedAttack(GetAttackFrames());
            return;
        }

        if (oneShotActive && currentState == AnimationState.Attack)
            oneShotActive = false;

        AnimationState next = ResolveMovementState();
        if (next != currentState) SetState(next, true);
        UpdateLoop(GetFrames(currentState), GetFps(currentState));
    }

    private AnimationState ResolveMovementState()
    {
        if (controller.IsDashing) return AnimationState.Dash;

        if (!controller.IsGrounded())
        {
            float verticalSpeed = controller.VerticalSpeed();
            if (verticalSpeed > verticalStateThreshold) return AnimationState.Jump;
            if (verticalSpeed < -verticalStateThreshold) return AnimationState.Fall;
            return AnimationState.Air;
        }

        return controller.HorizontalSpeed() > moveThreshold
            ? AnimationState.Run
            : AnimationState.Idle;
    }

    private void OnAttackStarted(int comboIndex)
    {
        if (isDead || isHurt) return;

        oneShotDuration = combat != null
            ? Mathf.Max(0.05f, combat.CurrentStepDuration)
            : defaultAttackDuration;

        oneShotActive = true;
        SetState(AnimationState.Attack, true);
    }

    private Sprite[] GetAttackFrames()
    {
        return combat != null && combat.ComboIndex == secondAttackComboIndex ? attack2Frames : attack1Frames;
    }

    private void UpdateTimedAttack(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0) return;

        float frameDuration = oneShotDuration / frames.Length;
        frameTimer += Time.deltaTime;

        int targetFrame = Mathf.Min(
            frames.Length - 1,
            Mathf.FloorToInt(frameTimer / Mathf.Max(0.001f, frameDuration))
        );

        if (targetFrame != frameIndex)
        {
            frameIndex = targetFrame;
            ApplyFrame(frames, frameIndex);
        }
    }

    private void OnDamaged(DamageInfo info)
    {
        if (isDead) return;

        isHurt = true;
        hurtTimer = hurtDuration;
        oneShotActive = true;
        SetState(AnimationState.Hurt, true);
    }

    private void OnDied(Health deadHealth)
    {
        isDead = true;
        isHurt = false;
        oneShotActive = true;
        SetState(AnimationState.Death, true);
    }

    private void UpdateOneShot(Sprite[] frames, float fps, bool holdLastFrame)
    {
        if (frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
        if (frameTimer < 1f / Mathf.Max(1f, fps)) return;

        frameTimer = 0f;
        if (frameIndex < frames.Length - 1)
        {
            frameIndex++;
            ApplyFrame(frames, frameIndex);
        }
        else if (!holdLastFrame)
        {
            frameIndex = 0;
            ApplyFrame(frames, frameIndex);
        }
    }

    private void UpdateLoop(Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
        if (frameTimer >= 1f / Mathf.Max(1f, fps))
        {
            frameTimer = 0f;
            frameIndex = (frameIndex + 1) % frames.Length;
            ApplyFrame(frames, frameIndex);
        }
    }

    private void SetState(AnimationState state, bool reset)
    {
        currentState = state;
        if (reset)
        {
            frameIndex = 0;
            frameTimer = 0f;
        }

        Sprite[] frames = state == AnimationState.Attack
            ? GetAttackFrames()
            : GetFrames(state);

        ApplyFrame(frames, frameIndex);
    }

    private void ApplyFrame(Sprite[] frames, int index)
    {
        if (frames != null && index >= 0 && index < frames.Length && frames[index] != null)
            spriteRenderer.sprite = frames[index];
    }

    private Sprite[] GetFrames(AnimationState state)
    {
        switch (state)
        {
            case AnimationState.Run: return runFrames;
            case AnimationState.Dash: return dashFrames;
            case AnimationState.Jump: return jumpFrames;
            case AnimationState.Air: return airFrames;
            case AnimationState.Fall: return fallFrames;
            case AnimationState.Hurt: return hurtFrames;
            case AnimationState.Death: return deathFrames;
            default: return idleFrames;
        }
    }

    private float GetFps(AnimationState state)
    {
        switch (state)
        {
            case AnimationState.Run: return runFps;
            case AnimationState.Dash: return dashFps;
            case AnimationState.Jump: return jumpFps;
            case AnimationState.Air: return airFps;
            case AnimationState.Fall: return fallFps;
            default: return idleFps;
        }
    }
}

#if UNITY_EDITOR
[InitializeOnLoad]
public static class PlayerEditorSpriteSync
{
    static PlayerEditorSpriteSync()
    {
        EditorApplication.delayCall += SyncAllPlayersInScene;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += SyncAllPlayersInScene;
    }

    [MenuItem("DreamGarden/Apply Player Preview Sprites")]
    public static void SyncAllPlayersInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        PlayerSpriteAnimator[] animators = Object.FindObjectsOfType<PlayerSpriteAnimator>(true);
        for (int i = 0; i < animators.Length; i++)
            animators[i].ApplyPreviewSpriteIfNeeded();
    }
}
#endif