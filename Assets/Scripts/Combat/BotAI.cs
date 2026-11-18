using UnityEngine;

/// <summary>
/// 6종의 적 봇 AI 통합 컨트롤러.
///  - Scout         : 정찰 및 근접 추적/근접 공격
///  - Sniper        : 거리 유지 및 조준 레이저 사격
///  - Drone         : 공중 자유 순찰, 스캔 인식, 고속 돌진/접촉 공격
///  - ChargeBot     : 인식 후 고속 돌진 (벽/근접 충돌 시 회복 대기)
///  - MixerBot      : 인식 후 접근하여 회전 다단 히트 공격
///  - MixerChargeBot: 인식 후 고속 돌진하여 근접/벽 도달 시 회전 다단 히트 공격
/// </summary>
[RequireComponent(typeof(Health), typeof(Collider2D))]
public class BotAI : MonoBehaviour
{
    public enum BotType
    {
        Scout = 0,
        Sniper = 1,
        Drone = 2,
        ChargeBot = 3,
        MixerBot = 4,
        MixerChargeBot = 5
    }

    public enum DroneState { Free, Recognizing, Aggro }
    private enum ActionState { Patrol, Recognizing, Active, Spin, Recovery }

    [Header("Bot Identity")]
    public BotType type = BotType.Scout;
    [SerializeField] private Transform player;
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0)] private int gearReward = 1;

    [Header("Movement & Patrol (Ground Bots)")]
    [SerializeField, Min(0f)] private float patrolSpeed = 1.2f;
    [SerializeField, Min(0.1f)] private float chaseSpeedMultiplier = 1.5f;
    [SerializeField] private bool usePatrolBounds = false;
    [SerializeField] private float patrolMinX = -4f;
    [SerializeField] private float patrolMaxX = 4f;
    [SerializeField, Min(0.05f)] private float wallCheckDistance = 0.55f;
    [SerializeField, Min(0.05f)] private float groundCheckDistance = 1.3f;

    [Header("Detection")]
    [SerializeField, Min(0f)] private float scoutFrontRange = 6f;
    [SerializeField, Min(0f)] private float scoutBackRange = 3f;
    [SerializeField, Min(0f)] private float sniperRange = 7f;
    [SerializeField, Min(0f)] private float verticalDetectionTolerance = 2.5f;
    [Tooltip("플레이어 발견 후 공격 상태로 전환되기까지의 인식 시간(초)")]
    [SerializeField, Min(0f)] private float recognitionDelay = 0.35f;

    [Header("Scout (Melee)")]
    [SerializeField, Min(1)] private int meleeDamage = 1;
    [SerializeField, Min(0.1f)] private float meleeRange = 1.0f;
    [SerializeField, Min(0.1f)] private float meleeVerticalRange = 1.2f;
    [SerializeField, Min(0.05f)] private float meleeCooldown = 1.0f;
    [SerializeField] private Vector2 meleeKnockback = new Vector2(3f, 1.5f);

    [Header("Sniper (Laser)")]
    [SerializeField, Min(0.1f)] private float rangedPreferredDistance = 5.5f;
    [SerializeField, Min(0f)] private float rangedDistanceTolerance = 0.75f;
    [SerializeField, Min(0f)] private float rangedRepositionSpeedMultiplier = 1f;
    [SerializeField, Min(1)] private int sniperDamage = 1;
    [SerializeField, Min(1f)] private float sniperLaserLength = 12f;
    [SerializeField, Min(0.1f)] private float laserCooldown = 1.8f;
    [SerializeField, Min(0.05f)] private float laserTelegraphTime = 0.55f;
    [SerializeField, Min(0.05f)] private float laserVerticalTolerance = 0.65f;
    [SerializeField, Min(0.005f)] private float laserWidth = 0.055f;
    [SerializeField] private Vector2 sniperKnockback = new Vector2(3f, 1f);

    [Header("Drone (Aerial)")]
    [SerializeField] private DroneState droneState = DroneState.Free;
    [SerializeField] private bool droneSpriteFacesLeft = true;
    [SerializeField, Min(0.5f)] private float droneDetectionRadius = 8f;
    [SerializeField, Min(0f)] private float droneLoseAggroRadius = 0f;
    [SerializeField, Min(0f)] private float droneFreePatrolSpeed = 0.8f;
    [SerializeField, Min(0f)] private float droneFreePatrolRadius = 3.5f;
    [SerializeField, Min(0f)] private float droneAggroSpeedMultiplier = 1.7f;
    [SerializeField] private float droneTargetOffsetY = 0f;
    [SerializeField, Min(1)] private int droneContactDamage = 1;
    [SerializeField] private Vector2 droneContactKnockback = new Vector2(4.5f, 2.5f);
    [SerializeField, Min(0.05f)] private float droneContactCooldown = 0.4f;
    [SerializeField] private bool droneContactOnlyInAggro = true;
    [SerializeField, Min(0f)] private float droneHitStunDuration = 0.35f;
    [SerializeField, Min(0f)] private float droneHitKnockbackScale = 0f;

    [Header("Charge & Mixer Bots")]
    [SerializeField, Min(1f)] private float chargeSpeedMultiplier = 4f;
    [SerializeField, Min(0f)] private float recoverySeconds = 2.5f;
    [SerializeField, Min(0.1f)] private float mixerAttackRange = 1.5f;
    [SerializeField, Min(0.1f)] private float mixerAttackVerticalRange = 1.8f;
    [SerializeField, Min(1)] private int mixerAttackDamage = 1;
    [SerializeField, Min(1)] private int spinHitCount = 2;
    [SerializeField, Min(0.01f)] private float spinHitInterval = 0.12f;
    [SerializeField, Min(0.05f)] private float spinDuration = 0.55f;
    [SerializeField, Min(0f)] private float mixerAttackCooldown = 2.5f;
    [SerializeField] private Vector2 mixerKnockback = new Vector2(5f, 2.5f);
    [SerializeField] private bool showMixerAttackVisual = true;

    [Header("Visual")]
    [SerializeField] private Color alertColor = Color.yellow;

    private SpriteRenderer spriteRenderer;
    private DroneBotAnimation droneAnimation;
    private HitReaction hitReaction;
    private Health health;
    private Rigidbody2D body;
    private Collider2D selfCollider;
    private Collider2D playerCollider;
    private LineRenderer laser;
    private Transform attackVisualTransform;
    private SpriteRenderer attackVisualRenderer;

    private Vector2 spawnPosition;
    private Vector2 droneKnockbackVelocity;
    private Color baseColor = Color.white;
    private ActionState actionState = ActionState.Patrol;
    private int direction = 1;
    private bool dead;
    private float stateTimer;
    private float attackTimer;
    private float laserTelegraphTimer;
    private float droneHitStunTimer;
    private float droneContactTimer;
    private float spinHitTimer;
    private int spinHitsLeft;

    public DroneState CurrentDroneState => droneState;
    public bool IsDroneStunned => droneHitStunTimer > 0f || (hitReaction != null && hitReaction.IsStunned);
    public bool IsChargeOrMixerType =>
        type == BotType.ChargeBot || type == BotType.MixerBot || type == BotType.MixerChargeBot;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        droneAnimation = GetComponent<DroneBotAnimation>();
        hitReaction = GetComponent<HitReaction>();
        health = GetComponent<Health>();
        selfCollider = GetComponent<Collider2D>();

        body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = type == BotType.Drone ? 0f : 3f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        spawnPosition = transform.position;

        if (spriteRenderer != null)
        {
            if (spriteRenderer.sprite == null && droneAnimation == null)
                spriteRenderer.sprite = RuntimeSprites.White;
            baseColor = spriteRenderer.color;
        }

        if (type == BotType.Drone)
            ApplyDroneFlip();

        if (GetComponent<EnemyHealthBar>() == null)
            gameObject.AddComponent<EnemyHealthBar>();

        ResolvePlayer();
        EnsureLaser();
        EnsureMixerAttackVisual();
    }

    private void Start()
    {
        int resolvedMaxHp = health != null && health.Max > 0 ? health.Max : Mathf.Max(1, maxHealth);
        health.Configure(Team.Enemy, resolvedMaxHp, 0f);
        health.Died += OnDied;
        health.Damaged += OnDamaged;
        GoldCounter.EnsureInstance();
    }

    private void ResolvePlayer()
    {
        if (player == null)
        {
            PlayerController2D pc = FindObjectOfType<PlayerController2D>();
            if (pc != null)
            {
                player = pc.transform;
            }
            else
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
        }

        if (player != null && playerCollider == null)
            playerCollider = player.GetComponent<Collider2D>();
    }

    private void EnsureLaser()
    {
        if (type != BotType.Sniper || laser != null) return;
        laser = GetComponent<LineRenderer>();
        if (laser == null) laser = gameObject.AddComponent<LineRenderer>();
        laser.positionCount = 2;
        laser.startWidth = laserWidth;
        laser.endWidth = laserWidth;
        if (laser.material == null) laser.material = new Material(Shader.Find("Sprites/Default"));
        laser.startColor = Color.red;
        laser.endColor = Color.red;
        laser.enabled = false;
        laser.sortingOrder = 6;
    }

    private void EnsureMixerAttackVisual()
    {
        if (!showMixerAttackVisual) return;
        if (type != BotType.MixerBot && type != BotType.MixerChargeBot) return;
        if (attackVisualTransform != null) return;

        GameObject visual = new GameObject("MixerAttackRangeVisual");
        attackVisualTransform = visual.transform;
        attackVisualTransform.SetParent(transform, false);
        attackVisualRenderer = visual.AddComponent<SpriteRenderer>();
        attackVisualRenderer.sprite = RuntimeSprites.White;
        attackVisualRenderer.color = new Color(1f, 1f, 1f, 0.35f);
        attackVisualRenderer.sortingOrder = 5;
        attackVisualRenderer.enabled = false;
    }

    private void Update()
    {
        EnsureLaser();
        EnsureMixerAttackVisual();

        if (dead) return;
        if (player == null) ResolvePlayer();

        if (type == BotType.Drone)
        {
            UpdateDrone();
            return;
        }

        if (hitReaction != null && hitReaction.IsStunned)
        {
            UpdateLaser(false);
            return;
        }

        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        if (player == null)
        {
            PatrolGround();
            return;
        }

        Vector2 delta = player.position - transform.position;

        if (IsChargeOrMixerType)
        {
            UpdateChargeOrMixer(delta);
            return;
        }

        UpdateScoutOrSniper(delta);
    }

    // ------------------------------------------------------------------ Scout & Sniper

    private void UpdateScoutOrSniper(Vector2 delta)
    {
        float dx = Mathf.Abs(delta.x);
        float dy = Mathf.Abs(delta.y);

        bool sees = type == BotType.Scout
            ? dx <= (delta.x * direction >= 0f ? scoutFrontRange : scoutBackRange) && dy <= verticalDetectionTolerance
            : dx <= sniperRange && dy <= verticalDetectionTolerance;

        if (actionState == ActionState.Patrol)
        {
            if (sees)
            {
                BeginRecognition();
                return;
            }
            PatrolGround();
            return;
        }

        if (actionState == ActionState.Recognizing)
        {
            SetHorizontalSpeed(0f);
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                RestoreBaseColor();
                actionState = ActionState.Active;
            }
            else
            {
                if (type == BotType.Sniper) UpdateLaser(false);
                return;
            }
        }

        direction = delta.x >= 0f ? 1 : -1;
        if (spriteRenderer != null) spriteRenderer.flipX = direction < 0;

        if (type == BotType.Scout)
            UpdateScout(delta);
        else
            UpdateSniper(delta);
    }

    private void UpdateScout(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > meleeRange || Mathf.Abs(delta.y) > meleeVerticalRange)
            SetHorizontalSpeed(direction * patrolSpeed * chaseSpeedMultiplier);
        else
            SetHorizontalSpeed(0f);

        if (Mathf.Abs(delta.x) <= meleeRange && Mathf.Abs(delta.y) <= meleeVerticalRange && attackTimer <= 0f)
        {
            attackTimer = meleeCooldown;
            DamagePlayer(meleeDamage, new Vector2(direction * Mathf.Abs(meleeKnockback.x), meleeKnockback.y));
        }
    }

    private void UpdateSniper(Vector2 delta)
    {
        float distanceX = Mathf.Abs(delta.x);
        float preferred = Mathf.Max(0.1f, rangedPreferredDistance);
        float tolerance = Mathf.Max(0f, rangedDistanceTolerance);

        float moveDir = 0f;
        if (distanceX > preferred + tolerance) moveDir = direction;
        else if (distanceX < preferred - tolerance) moveDir = -direction;

        SetHorizontalSpeed(moveDir * patrolSpeed * rangedRepositionSpeedMultiplier);

        delta = player.position - transform.position;
        distanceX = Mathf.Abs(delta.x);
        direction = delta.x >= 0f ? 1 : -1;
        if (spriteRenderer != null) spriteRenderer.flipX = direction < 0;

        bool inFiringPosition = distanceX <= sniperRange
            && distanceX >= preferred - tolerance
            && distanceX <= preferred + tolerance
            && Mathf.Abs(delta.y) <= laserVerticalTolerance;

        if (inFiringPosition && attackTimer <= 0f && laserTelegraphTimer <= 0f)
            laserTelegraphTimer = laserTelegraphTime;

        if (!inFiringPosition && laserTelegraphTimer <= 0f)
        {
            UpdateLaser(false);
            return;
        }

        if (laserTelegraphTimer > 0f)
        {
            laserTelegraphTimer -= Time.deltaTime;
            UpdateLaser(true);

            if (laserTelegraphTimer <= 0f)
            {
                attackTimer = laserCooldown;
                Vector2 origin = (Vector2)transform.position + Vector2.up * 0.25f;
                RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.right * direction, sniperLaserLength);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (RaycastHit2D hit in hits)
                {
                    if (hit.collider == null || hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                        continue;

                    Health target = hit.collider.GetComponentInParent<Health>();
                    if (target != null && target.Team == Team.Player && Mathf.Abs(origin.y - player.position.y) <= laserVerticalTolerance)
                        DamagePlayer(sniperDamage, new Vector2(direction * Mathf.Abs(sniperKnockback.x), sniperKnockback.y));
                    break;
                }
            }
        }
        else
        {
            UpdateLaser(false);
        }
    }

    private void UpdateLaser(bool visible)
    {
        if (laser == null) return;
        laser.startWidth = laserWidth;
        laser.endWidth = laserWidth;
        Vector3 from = transform.position + Vector3.up * 0.25f;
        laser.SetPosition(0, from);
        laser.SetPosition(1, from + Vector3.right * direction * sniperLaserLength);
        laser.enabled = visible;
    }

    // ------------------------------------------------------------------ Charge & Mixer Bots

    private void UpdateChargeOrMixer(Vector2 delta)
    {
        if (actionState != ActionState.Patrol)
        {
            direction = delta.x >= 0f ? 1 : -1;
            if (spriteRenderer != null) spriteRenderer.flipX = direction < 0;
        }

        if (actionState == ActionState.Patrol && CanSeePlayerForChargeMixer(delta))
        {
            BeginRecognition();
            SetHorizontalSpeed(0f);
            return;
        }

        switch (actionState)
        {
            case ActionState.Patrol:
                PatrolGround();
                break;

            case ActionState.Recognizing:
                SetHorizontalSpeed(0f);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    RestoreBaseColor();
                    actionState = ActionState.Active;
                }
                break;

            case ActionState.Active:
                ExecuteChargeOrApproach(delta);
                break;

            case ActionState.Spin:
                ExecuteSpin(delta);
                break;

            case ActionState.Recovery:
                SetHorizontalSpeed(0f);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    actionState = ActionState.Patrol;
                    direction *= -1;
                }
                break;
        }
    }

    private bool CanSeePlayerForChargeMixer(Vector2 delta)
    {
        SpriteRenderer playerSprite = player.GetComponentInChildren<SpriteRenderer>();
        bool facingBot = true;
        if (playerSprite != null)
            facingBot = (delta.x >= 0f) == !playerSprite.flipX;

        float range = facingBot ? scoutFrontRange : scoutBackRange;
        return Mathf.Abs(delta.x) <= range && Mathf.Abs(delta.y) <= verticalDetectionTolerance;
    }

    private void ExecuteChargeOrApproach(Vector2 delta)
    {
        SetHorizontalSpeed(direction * patrolSpeed * chargeSpeedMultiplier);

        Vector2 pos = body != null ? body.position : (Vector2)transform.position;
        bool hitWall = Physics2D.Raycast(pos + Vector2.up * 0.1f, Vector2.right * direction, wallCheckDistance, GameLayers.GroundMaskValue);
        bool close = Mathf.Abs(delta.x) <= mixerAttackRange && Mathf.Abs(delta.y) <= mixerAttackVerticalRange;

        if (type == BotType.MixerBot && close)
        {
            if (attackTimer <= 0f) BeginSpin();
            else SetHorizontalSpeed(0f);
            return;
        }

        if (type == BotType.MixerChargeBot && (close || hitWall))
        {
            if (attackTimer <= 0f) BeginSpin();
            else SetHorizontalSpeed(0f);
            return;
        }

        if (type == BotType.ChargeBot && (close || hitWall))
        {
            BeginRecovery();
        }
    }

    private void BeginSpin()
    {
        actionState = ActionState.Spin;
        stateTimer = spinDuration;
        spinHitTimer = 0f;
        spinHitsLeft = spinHitCount;
        SetHorizontalSpeed(0f);
        if (attackVisualRenderer != null) attackVisualRenderer.enabled = true;
    }

    private void ExecuteSpin(Vector2 delta)
    {
        SetHorizontalSpeed(0f);
        stateTimer -= Time.deltaTime;
        spinHitTimer -= Time.deltaTime;

        if (attackVisualTransform != null && attackVisualRenderer != null)
        {
            float progress = 1f - Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, spinDuration));
            float phase = progress * 2f;
            float swingT = phase < 1f ? phase : phase - 1f;
            float angle = phase < 1f ? Mathf.Lerp(-55f, 55f, swingT) : Mathf.Lerp(55f, -55f, swingT);

            attackVisualTransform.localPosition = new Vector3(direction * mixerAttackRange * 0.5f, 0f, -0.1f);
            attackVisualTransform.localRotation = Quaternion.Euler(0f, 0f, angle * direction);
            attackVisualTransform.localScale = new Vector3(mixerAttackRange, mixerAttackVerticalRange * 2f, 1f);
        }

        if (spinHitsLeft > 0 && spinHitTimer <= 0f)
        {
            if (Mathf.Abs(delta.x) <= mixerAttackRange && Mathf.Abs(delta.y) <= mixerAttackVerticalRange)
                DamagePlayer(mixerAttackDamage, new Vector2(direction * Mathf.Abs(mixerKnockback.x), mixerKnockback.y));

            spinHitsLeft--;
            spinHitTimer = spinHitInterval;
        }

        if (stateTimer <= 0f)
        {
            if (attackVisualRenderer != null) attackVisualRenderer.enabled = false;
            if (attackVisualTransform != null) attackVisualTransform.localRotation = Quaternion.identity;
            attackTimer = mixerAttackCooldown;
            BeginRecovery();
        }
    }

    private void BeginRecovery()
    {
        actionState = ActionState.Recovery;
        stateTimer = recoverySeconds;
        SetHorizontalSpeed(0f);
    }

    // ------------------------------------------------------------------ Drone

    private void UpdateDrone()
    {
        if (droneContactTimer > 0f)
            droneContactTimer -= Time.deltaTime;

        if (droneHitStunTimer > 0f)
        {
            droneHitStunTimer -= Time.deltaTime;
            if (droneHitKnockbackScale <= 0.001f)
            {
                SetVelocity(Vector2.zero);
            }
            else if (hitReaction == null || !hitReaction.IsStunned)
            {
                droneKnockbackVelocity = Vector2.Lerp(droneKnockbackVelocity, Vector2.zero, Time.deltaTime * 14f);
                SetVelocity(droneKnockbackVelocity);
            }

            if (droneHitStunTimer <= 0f)
            {
                droneKnockbackVelocity = Vector2.zero;
                if (hitReaction == null || !hitReaction.IsStunned)
                    SetVelocity(Vector2.zero);
            }
            return;
        }

        if (hitReaction != null && hitReaction.IsStunned)
            return;

        if (player == null)
        {
            EnterDroneFree();
            PatrolDroneFree();
            return;
        }

        Vector2 playerCenter = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.position;
        Vector2 delta = playerCenter - (Vector2)transform.position;
        float distanceToPlayer = delta.magnitude;

        switch (droneState)
        {
            case DroneState.Free:
                if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Fly);
                if (distanceToPlayer <= droneDetectionRadius)
                {
                    TriggerDroneRecognition();
                    return;
                }
                PatrolDroneFree();
                break;

            case DroneState.Recognizing:
                SetVelocity(Vector2.zero);
                FaceTowards(delta.x);
                if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Scan);

                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    RestoreBaseColor();
                    droneState = DroneState.Aggro;
                    if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Attack);
                }
                break;

            case DroneState.Aggro:
                if (droneLoseAggroRadius > 0f && distanceToPlayer > droneLoseAggroRadius)
                {
                    EnterDroneFree();
                    return;
                }

                FaceTowards(delta.x);
                if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Attack);

                Vector2 targetPos = playerCenter + Vector2.up * droneTargetOffsetY;
                Vector2 toTarget = targetPos - (Vector2)transform.position;
                Vector2 moveDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;

                if (Mathf.Abs(moveDir.x) > 0.01f &&
                    Physics2D.Raycast(transform.position, Vector2.right * Mathf.Sign(moveDir.x), wallCheckDistance, GameLayers.GroundMaskValue))
                {
                    moveDir.x = 0f;
                }

                float aggroChaseSpeed = droneFreePatrolSpeed * Mathf.Max(0f, droneAggroSpeedMultiplier);
                SetVelocity(moveDir * aggroChaseSpeed);
                break;
        }
    }

    private void EnterDroneFree()
    {
        actionState = ActionState.Patrol;
        droneState = DroneState.Free;
        RestoreBaseColor();
        if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Fly);
    }

    private void TriggerDroneRecognition()
    {
        actionState = ActionState.Recognizing;
        stateTimer = recognitionDelay;
        droneState = DroneState.Recognizing;
        SetAlertTint();
        SetVelocity(Vector2.zero);
        if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Scan);
    }

    private void PatrolDroneFree()
    {
        bool hitWall = Physics2D.Raycast(transform.position, Vector2.right * direction, wallCheckDistance, GameLayers.GroundMaskValue);
        bool outOfBounds = false;
        if (droneFreePatrolRadius > 0f)
        {
            float offsetX = transform.position.x - spawnPosition.x;
            if ((direction > 0 && offsetX >= droneFreePatrolRadius) || (direction < 0 && offsetX <= -droneFreePatrolRadius))
                outOfBounds = true;
        }

        if (hitWall || outOfBounds) direction *= -1;
        SetVelocity(new Vector2(direction * droneFreePatrolSpeed, 0f));
        ApplyDroneFlip();
    }

    private void FaceTowards(float deltaX)
    {
        if (Mathf.Abs(deltaX) > 0.01f)
            direction = deltaX >= 0f ? 1 : -1;
        ApplyDroneFlip();
    }

    private void ApplyDroneFlip()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.flipX = droneSpriteFacesLeft ? direction > 0 : direction < 0;
    }

    // ------------------------------------------------------------------ Shared Helpers & Events

    private void BeginRecognition()
    {
        actionState = ActionState.Recognizing;
        stateTimer = recognitionDelay;
        SetAlertTint();
    }

    private void SetAlertTint()
    {
        if (spriteRenderer != null && droneAnimation == null)
            spriteRenderer.color = alertColor;
    }

    private void RestoreBaseColor()
    {
        if (spriteRenderer != null && droneAnimation == null)
            spriteRenderer.color = baseColor;
    }

    private void PatrolGround()
    {
        Vector2 origin = body != null ? body.position : (Vector2)transform.position;
        int groundMask = GameLayers.GroundMaskValue;
        bool wall = Physics2D.Raycast(origin + Vector2.up * 0.1f, Vector2.right * direction, wallCheckDistance, groundMask);
        Vector2 footAhead = origin + Vector2.right * direction * 0.45f + Vector2.down * 0.25f;
        bool groundAhead = Physics2D.Raycast(footAhead, Vector2.down, groundCheckDistance, groundMask);

        bool outOfBounds = usePatrolBounds && patrolMaxX > patrolMinX &&
            ((direction < 0 && origin.x <= patrolMinX) || (direction > 0 && origin.x >= patrolMaxX));

        if (wall || !groundAhead || outOfBounds)
            direction *= -1;

        SetHorizontalSpeed(direction * patrolSpeed);
        if (spriteRenderer != null) spriteRenderer.flipX = direction < 0;
    }

    private void SetHorizontalSpeed(float speed)
    {
        if (body != null)
        {
            Vector2 v = body.velocity;
            v.x = speed;
            body.velocity = v;
        }
        else
        {
            transform.position += Vector3.right * speed * Time.deltaTime;
        }
    }

    private void SetVelocity(Vector2 velocity)
    {
        if (body != null)
            body.velocity = velocity;
        else
            transform.position += (Vector3)(velocity * Time.deltaTime);
    }

    private void DamagePlayer(int amount, Vector2 knockback)
    {
        if (player == null) return;
        Health ph = player.GetComponentInParent<Health>();
        if (ph == null || ph.Team != Team.Player) return;

        DamageInfo info = new DamageInfo
        {
            amount = Mathf.Max(1, amount),
            knockback = knockback,
            sourceTeam = Team.Enemy,
            source = gameObject,
            hitPoint = player.position
        };
        ph.TakeDamage(info);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!dead && actionState == ActionState.Active && collision.collider.gameObject.layer == GameLayers.Ground)
        {
            if (type == BotType.ChargeBot) BeginRecovery();
            else if (type == BotType.MixerChargeBot && attackTimer <= 0f) BeginSpin();
        }
        TryDroneContactAttack(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDroneContactAttack(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDroneContactAttack(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDroneContactAttack(other);
    }

    private void TryDroneContactAttack(Collider2D other)
    {
        if (dead || type != BotType.Drone || other == null) return;
        if (IsDroneStunned) return;
        if (droneContactOnlyInAggro && droneState != DroneState.Aggro) return;
        if (droneContactTimer > 0f) return;

        Health target = other.GetComponentInParent<Health>();
        if (target == null || target.Team != Team.Player || target.IsDead) return;

        float pushDir = target.transform.position.x >= transform.position.x ? 1f : -1f;
        Vector2 knockback = new Vector2(pushDir * Mathf.Abs(droneContactKnockback.x), droneContactKnockback.y);

        DamageInfo info = new DamageInfo
        {
            amount = Mathf.Max(1, droneContactDamage),
            knockback = knockback,
            sourceTeam = Team.Enemy,
            source = gameObject,
            hitPoint = other.ClosestPoint(transform.position)
        };

        if (target.TakeDamage(info))
            droneContactTimer = droneContactCooldown;
    }

    private void OnDamaged(DamageInfo info)
    {
        if (type == BotType.Drone)
        {
            if (droneState == DroneState.Free)
                TriggerDroneRecognition();

            droneHitStunTimer = Mathf.Max(0f, droneHitStunDuration);
            droneKnockbackVelocity = info.knockback * droneHitKnockbackScale;
            if (hitReaction == null || !hitReaction.IsStunned)
                SetVelocity(droneKnockbackVelocity);

            if (droneAnimation != null && droneHitStunTimer > 0f)
                droneAnimation.PlayHit(droneHitStunTimer);
            return;
        }

        if (actionState == ActionState.Patrol)
            BeginRecognition();
    }

    private void OnDied(Health h)
    {
        if (dead) return;
        dead = true;
        if (laser != null) laser.enabled = false;
        if (attackVisualRenderer != null) attackVisualRenderer.enabled = false;
        GearReward.Spawn(transform.position, gearReward);
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        int dir = Application.isPlaying ? direction : 1;

        switch (type)
        {
            case BotType.Scout:
                Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.85f);
                Gizmos.DrawWireCube(origin + Vector3.right * dir * (meleeRange * 0.5f), new Vector3(meleeRange, meleeVerticalRange * 2f, 0.05f));
                break;

            case BotType.Sniper:
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
                Gizmos.DrawLine(origin + Vector3.up * 0.25f, origin + Vector3.up * 0.25f + Vector3.right * dir * sniperLaserLength);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(origin, rangedPreferredDistance);
                break;

            case BotType.Drone:
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
                Gizmos.DrawWireSphere(origin, droneDetectionRadius);
                if (droneFreePatrolRadius > 0f)
                {
                    Vector3 center = Application.isPlaying ? (Vector3)spawnPosition : origin;
                    Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
                    Gizmos.DrawLine(center + Vector3.left * droneFreePatrolRadius, center + Vector3.right * droneFreePatrolRadius);
                }
                break;

            default:
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(origin, scoutFrontRange);
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(origin + Vector3.right * dir * (mixerAttackRange * 0.5f), new Vector3(mixerAttackRange, mixerAttackVerticalRange * 2f, 0.05f));
                break;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= OnDied;
            health.Damaged -= OnDamaged;
        }
        if (attackVisualTransform != null)
            Destroy(attackVisualTransform.gameObject);
    }
}