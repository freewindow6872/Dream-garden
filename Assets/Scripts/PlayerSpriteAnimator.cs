using UnityEngine;

/// <summary>
/// 플레이어 스프라이트 애니메이션 통합 담당.
/// 기존 PlayerController2D / PlayerCombat / Health 상태를 읽어 스프라이트만 교체한다.
/// 이동/공격 판정과 게임플레이 로직은 변경하지 않는다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerController2D))]
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("Animation Speed (frames per second)")]
    [SerializeField] private float idleFps = 7f;
    [SerializeField] private float runFps = 12f;
    [SerializeField] private float dashFps = 12f;
    [SerializeField] private float jumpFps = 9f;
    [SerializeField] private float airFps = 5f;
    [SerializeField] private float fallFps = 9f;
    [SerializeField] private float hurtFps = 10f;
    [SerializeField] private float deathFps = 8f;
    [SerializeField] private float moveThreshold = 0.15f;
    [SerializeField] private float verticalStateThreshold = 0.5f;

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
        spriteRenderer = GetComponent<SpriteRenderer>();
        controller = GetComponent<PlayerController2D>();
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<Health>();

        idleFrames = LoadFrames("idel", 5);
        runFrames = LoadFrames("run", 8);
        dashFrames = LoadFrames("dash", 2);

        jumpFrames = LoadFrames("jump", 2);
        airFrames = LoadFrames("air", 2);
        fallFrames = LoadFrames("fall", 2);

        attack1Frames = LoadFrames("attack_1", 4);
        attack2Frames = LoadFrames("attack_2", 4);
        hurtFrames = LoadFrames("hurt", 1);
        deathFrames = LoadFrames("death", 6);

        spriteRenderer.color = Color.white;
        SetState(AnimationState.Idle, true);
    }

    private void OnEnable()
    {
        if (combat == null) combat = GetComponent<PlayerCombat>();
        if (health == null) health = GetComponent<Health>();

        if (combat != null) combat.AttackStarted += OnAttackStarted;
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (combat != null) combat.AttackStarted -= OnAttackStarted;
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
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

            Sprite[] attackFrames = GetAttackFrames();
            UpdateTimedAttack(attackFrames);
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
            : 0.26f;

        oneShotActive = true;
        SetState(AnimationState.Attack, true);
    }

    private Sprite[] GetAttackFrames()
    {
        // 0번/1번 콤보는 attack_1, 마지막 2번 콤보는 attack_2
        return combat != null && combat.ComboIndex == 2 ? attack2Frames : attack1Frames;
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
        hurtTimer = 0.25f;
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
