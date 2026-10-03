using UnityEngine;

/// <summary>
/// 플레이어 이동. (기획서 2.2)
///  - 기본 점프 / 대시 / 이단 점프 (대시·이단 점프는 "강화 시스템으로 획득")
///  - 기존 공개 API(IsGrounded/HorizontalSpeed/VerticalSpeed)는 그대로 유지
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float acceleration = 45f;
    [SerializeField] private float deceleration = 55f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 13f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;
    [SerializeField] private float fallGravityMultiplier = 2.2f;
    [SerializeField] private float lowJumpGravityMultiplier = 1.8f;

    [Header("Ground Check")]
    [Tooltip("비워두면 콜라이더 아래쪽에서 자동 계산")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.55f, 0.12f);
    [SerializeField] private LayerMask groundLayer;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Double Jump (기획서 2.2: 강화로 획득)")]
    [SerializeField] private int maxAirJumps = 1;
    [SerializeField] private float airJumpForceMultiplier = 0.9f;

    [Header("Dash (기획서 2.2: 강화로 획득)")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.16f;
    [SerializeField] private float dashCooldown = 0.45f;
    [SerializeField] private int maxAirDashes = 1;
    [SerializeField] private bool dashGrantsInvulnerability = false;

    [Header("Unlock")]
    [Tooltip("true: 대시/이단점프를 처음부터 사용 (테스트용). false: GameManager.Progress 값을 따름")]
    [SerializeField] private bool unlockAllForTesting = true;

    [Header("Hurt")]
    [SerializeField] private float hurtStunTime = 0.25f;

    private Rigidbody2D rb;
    private Collider2D col;
    private Health health;

    private float moveInput;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private bool isGrounded;

    private int airJumpsLeft;
    private int airDashesLeft;
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private int dashDir = 1;
    private float baseGravityScale = 1f;
    private float hurtTimer;

    public int FacingDir { get; private set; }
    /// <summary>공격 중 등 방향 전환을 막고 싶을 때 PlayerCombat이 사용</summary>
    public bool FacingLocked { get; set; }
    /// <summary>공격 중 이동 속도 배율 (1 = 정상)</summary>
    public float MoveSpeedMultiplier { get; set; }
    public bool InputLocked { get; private set; }
    public bool IsDashing { get { return isDashing; } }
    public bool CanAct { get { return !InputLocked && !isDashing && hurtTimer <= 0f; } }

    public bool DashUnlocked { get { return unlockAllForTesting || GameManager.Instance.Progress.dashUnlocked; } }
    public bool DoubleJumpUnlocked { get { return unlockAllForTesting || GameManager.Instance.Progress.doubleJumpUnlocked; } }

    public event System.Action Jumped;
    public event System.Action DashStarted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        health = GetComponent<Health>();
        baseGravityScale = rb.gravityScale;

        FacingDir = 1;
        MoveSpeedMultiplier = 1f;

        // 기존 씬의 값(3번 레이어)과 "Ground" 이름 레이어를 모두 지면으로 인정
        groundLayer = groundLayer.value | GameLayers.GroundMaskValue;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
        EndDash();
    }

    private void Update()
    {
        moveInput = InputLocked ? 0f : GameInput.Horizontal;

        TickTimers();
        CheckGround();
        HandleJumpTimers();

        if (!InputLocked)
        {
            HandleDashInput();
            HandleJumpInput();
        }

        HandleFacing();
        UpdateHitBlink();
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
            return;
        }

        if (hurtTimer <= 0f)
            HandleMovement();

        ApplyBetterGravity();
    }

    // ---------------------------------------------------------------- timers

    private void TickTimers()
    {
        float dt = Time.deltaTime;
        if (dashCooldownTimer > 0f) dashCooldownTimer -= dt;
        if (hurtTimer > 0f) hurtTimer -= dt;

        if (isDashing)
        {
            dashTimer -= dt;
            if (dashTimer <= 0f) EndDash();
        }
    }

    // ---------------------------------------------------------------- ground

    private Vector2 GroundCheckCenter()
    {
        if (groundCheck != null) return groundCheck.position;
        if (col == null) return transform.position;

        Bounds b = col.bounds;
        return new Vector2(b.center.x, b.min.y - groundCheckSize.y * 0.5f + 0.02f);
    }

    private void CheckGround()
    {
        bool overlap = Physics2D.OverlapBox(GroundCheckCenter(), groundCheckSize, 0f, groundLayer) != null;

        // 올라가는 중에는 지면으로 치지 않는다 (점프 직후 재점프/원웨이 발판 통과 중 오작동 방지)
        isGrounded = overlap && rb.linearVelocity.y <= 0.05f;

        if (isGrounded)
        {
            airJumpsLeft = maxAirJumps;
            airDashesLeft = maxAirDashes;
        }
    }

    private void HandleJumpTimers()
    {
        if (isGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter -= Time.deltaTime;

        if (jumpBufferCounter > 0f)
            jumpBufferCounter -= Time.deltaTime;
    }

    // ---------------------------------------------------------------- jump

    private void HandleJumpInput()
    {
        bool pressed = GameInput.JumpDown;
        if (pressed) jumpBufferCounter = jumpBufferTime;

        if (jumpBufferCounter > 0f && CanAct)
        {
            if (coyoteCounter > 0f)
            {
                DoJump(jumpForce);
                jumpBufferCounter = 0f;
                coyoteCounter = 0f;
            }
            else if (pressed && !isGrounded && DoubleJumpUnlocked && airJumpsLeft > 0)
            {
                airJumpsLeft--;
                DoJump(jumpForce * airJumpForceMultiplier);
                jumpBufferCounter = 0f;
            }
        }

        if (GameInput.JumpUp && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }
    }

    private void DoJump(float force)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
        if (Jumped != null) Jumped();
    }

    // ---------------------------------------------------------------- dash

    private void HandleDashInput()
    {
        if (GameInput.DashDown) TryDash();
    }

    private void TryDash()
    {
        if (!DashUnlocked || !CanAct) return;
        if (dashCooldownTimer > 0f) return;
        if (!isGrounded && airDashesLeft <= 0) return;

        if (!isGrounded) airDashesLeft--;

        dashDir = Mathf.Abs(moveInput) > 0.01f ? (moveInput > 0f ? 1 : -1) : FacingDir;
        FacingDir = dashDir;

        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);

        if (dashGrantsInvulnerability && health != null) health.SetForcedInvulnerable(true);
        if (DashStarted != null) DashStarted();
    }

    private void EndDash()
    {
        if (!isDashing) return;

        isDashing = false;
        rb.gravityScale = baseGravityScale;
        rb.linearVelocity = new Vector2(dashDir * moveSpeed, 0f);

        if (dashGrantsInvulnerability && health != null) health.SetForcedInvulnerable(false);
    }

    // ---------------------------------------------------------------- movement

    private void HandleMovement()
    {
        float targetSpeed = moveInput * moveSpeed * MoveSpeedMultiplier;
        float speedDifference = targetSpeed - rb.linearVelocity.x;

        float accelRate = Mathf.Abs(targetSpeed) > 0.01f
            ? acceleration
            : deceleration;

        float movement = speedDifference * accelRate;

        rb.AddForce(Vector2.right * movement);
    }

    private void ApplyBetterGravity()
    {
        if (rb.linearVelocity.y < 0f)
        {
            rb.AddForce(
                Vector2.up * Physics2D.gravity.y * (fallGravityMultiplier - 1f),
                ForceMode2D.Force
            );
        }
        else if (rb.linearVelocity.y > 0f && !GameInput.JumpHeld)
        {
            rb.AddForce(
                Vector2.up * Physics2D.gravity.y * (lowJumpGravityMultiplier - 1f),
                ForceMode2D.Force
            );
        }
    }

    private void HandleFacing()
    {
        if (!FacingLocked && !InputLocked && !isDashing && hurtTimer <= 0f && Mathf.Abs(moveInput) > 0.01f)
            FacingDir = moveInput > 0f ? 1 : -1;

        if (spriteRenderer != null)
            spriteRenderer.flipX = FacingDir < 0;
    }

    // ---------------------------------------------------------------- hurt / lock

    private void OnDamaged(DamageInfo info)
    {
        EndDash();
        hurtTimer = hurtStunTime;
        if (info.knockback.sqrMagnitude > 0.01f)
            rb.linearVelocity = info.knockback;
    }

    private void OnDied(Health h)
    {
        SetInputLocked(true);
    }

    private void UpdateHitBlink()
    {
        if (spriteRenderer == null || health == null) return;

        Color c = spriteRenderer.color;
        c.a = health.IsHitInvulnerable && !health.IsDead
            ? (Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.5f ? 0.35f : 1f)
            : 1f;
        spriteRenderer.color = c;
    }

    public void SetInputLocked(bool locked)
    {
        InputLocked = locked;
        if (locked) EndDash();
    }

    /// <summary>공격 돌진(lunge) 등, 수평 속도를 순간적으로 더한다.</summary>
    public void AddHorizontalImpulse(float vx)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x + vx, rb.linearVelocity.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 center = Application.isPlaying || groundCheck != null ? GroundCheckCenter() : (Vector2)transform.position + Vector2.down * 0.5f;
        Gizmos.DrawWireCube(center, groundCheckSize);
    }

    // ---------------------------------------------------------------- public API (기존 유지)

    public bool IsGrounded()
    {
        return isGrounded;
    }

    public float HorizontalSpeed()
    {
        return Mathf.Abs(rb.linearVelocity.x);
    }

    public float VerticalSpeed()
    {
        return rb.linearVelocity.y;
    }
}
