using UnityEngine;

/// <summary>
/// 6종의 적 봇 AI 통합 컨트롤러.
///  - Scout         : 정찰 및 근접 추적/근접 공격
///  - Sniper        : 거리 유지 및 조준 레이저 사격
///  - Drone         : 공중 자유 순찰, 스캔 인식, 고속 돌진/접촉 공격
///  - SprintBot     : 탐색(walk) → 인식(scan) → 돌진(run) → 벽 근처/플레이어를 지나치면 정지(stop)
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
        SprintBot = 3,
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
    [SerializeField, Min(1)] private int sniperDamage = 1;
    [SerializeField, Min(1f)] private float sniperLaserLength = 12f;
    [SerializeField, Min(0.1f)] private float laserCooldown = 1.8f;
    [SerializeField, Min(0.05f)] private float laserTelegraphTime = 0.55f;
    [SerializeField, Min(0.05f)] private float sniperAttackAnimationDuration = 0.25f;
    [SerializeField, Min(0.05f)] private float sniperStandDuration = 0.35f;
    [SerializeField, Min(0.05f)] private float laserVerticalTolerance = 0.65f;
    [SerializeField, Min(0.005f)] private float laserWidth = 0.055f;
    [SerializeField] private Vector2 sniperKnockback = new Vector2(3f, 1f);

    [Header("Body Contact Damage (All Bots)\n")]
    [SerializeField, Min(1)] private int bodyContactDamage = 1;

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

    [Header("Sprint Bot")]
    [Tooltip("돌진 중 벽까지 이 거리 안으로 들어오면 달리기를 멈춘다")]
    [SerializeField, Min(0.1f)] private float sprintWallStopDistance = 1.6f;
    [Tooltip("돌진 방향 기준으로 플레이어가 이 거리 이상 뒤로 지나가면 달리기를 멈춘다")]
    [SerializeField, Min(0f)] private float sprintPassDistance = 0.6f;
    [Tooltip("정지(stop) 모션 시간(초). 이 동안 감속하며 미끄러진다")]
    [SerializeField, Min(0.05f)] private float sprintStopDuration = 0.5f;
    [SerializeField, Min(1)] private int sprintContactDamage = 1;
    [SerializeField, Min(0.1f)] private float sprintContactRange = 0.9f;
    [SerializeField, Min(0.1f)] private float sprintContactVerticalRange = 1.2f;
    [Tooltip("스프린트 봇 스프라이트 원본이 왼쪽을 보고 있으면 체크 (현재 에셋은 왼쪽을 봄)")]
    [SerializeField] private bool sprintSpriteFacesLeft = true;

    [Header("Visual")]
    [SerializeField] private Color alertColor = Color.yellow;
    [Tooltip("스나이퍼 스프라이트 원본이 왼쪽을 보고 있으면 체크 (현재 에셋은 왼쪽을 봄). 방향이 반대로 보이면 이 값을 토글하세요.")]
    [SerializeField] private bool sniperSpriteFacesLeft = true;

    private SpriteRenderer spriteRenderer;
    private DroneBotAnimation droneAnimation;
    private SprintBotAnimation sprintAnimation;
    private int runDirection = 1;
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
    private float sniperStandTimer;
    private float sniperAttackTimer;
    private bool sniperStandAfterShot;
    private float droneHitStunTimer;
    private float spinHitTimer;
    private int spinHitsLeft;

    public DroneState CurrentDroneState => droneState;
    public bool IsDroneStunned => droneHitStunTimer > 0f || (hitReaction != null && hitReaction.IsStunned);
    public bool IsChargeOrMixerType =>
        type == BotType.MixerBot || type == BotType.MixerChargeBot;
    public bool IsSprintType => type == BotType.SprintBot;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        droneAnimation = GetComponent<DroneBotAnimation>();
        sprintAnimation = GetComponent<SprintBotAnimation>();
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
        if (hitReaction == null) hitReaction = gameObject.AddComponent<HitReaction>();

        spawnPosition = transform.position;

        if (spriteRenderer != null)
        {
            if (type == BotType.SprintBot && sprintAnimation == null)
                sprintAnimation = gameObject.AddComponent<SprintBotAnimation>();
            if (spriteRenderer.sprite == null && droneAnimation == null && sprintAnimation == null)
                spriteRenderer.sprite = RuntimeSprites.White;
            baseColor = spriteRenderer.color;
        }

        if (type == BotType.Drone)
            ApplyDroneFlip();

        if (type == BotType.Sniper)
        {
            if (droneAnimation == null) droneAnimation = gameObject.AddComponent<DroneBotAnimation>();
            droneAnimation.SetSniperMode();
        }

        if (GetComponent<EnemyHealthBar>() == null)
            gameObject.AddComponent<EnemyHealthBar>();

        ResolvePlayer();
        EnsureLaser();
        EnsureMixerAttackVisual();
    }

    private void Start()
    {
        if (body != null) body.gravityScale = type == BotType.Drone ? 0f : 3f;
        if (type == BotType.Sniper)
        {
            if (droneAnimation == null) droneAnimation = gameObject.AddComponent<DroneBotAnimation>();
            droneAnimation.SetSniperMode();
        }
        int resolvedMaxHp = health != null && health.Max > 0 ? health.Max : Mathf.Max(1, maxHealth);
        health.Configure(Team.Enemy, resolvedMaxHp, 0f);
        health.Died += OnDied;
        health.Damaged += OnDamaged;
        GoldCounter.EnsureInstance();
    }

    /// <summary>direction(1=오른쪽, -1=왼쪽)에 맞춰 스프라이트를 뒤집는다. 스나이퍼는 원본이 왼쪽을 보므로 반대로 처리.</summary>
    private void ApplyFacing()
    {
        if (spriteRenderer == null) return;
        bool faceRight = direction >= 0;
        bool spriteFacesLeft = (type == BotType.Sniper && sniperSpriteFacesLeft)
                            || (type == BotType.SprintBot && sprintSpriteFacesLeft);
        spriteRenderer.flipX = spriteFacesLeft ? faceRight : !faceRight;
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

        if (type == BotType.SprintBot)
        {
            UpdateSprint(delta);
            return;
        }

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
                if (type == BotType.Sniper && droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Scan);
                return;
            }
            PatrolGround();
            if (type == BotType.Sniper && droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Walk);
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
                if (type == BotType.Sniper)
                {
                    UpdateLaser(false);
                    if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Scan);
                }
                return;
            }
        }

        direction = delta.x >= 0f ? 1 : -1;
        ApplyFacing();

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
        bool targetInRange = distanceX <= sniperRange && Mathf.Abs(delta.y) <= verticalDetectionTolerance;

        if (!targetInRange)
        {
            actionState = ActionState.Patrol;
            laserTelegraphTimer = 0f;
            sniperStandAfterShot = false;
            sniperStandTimer = 0f;
            sniperAttackTimer = 0f;
            UpdateLaser(false);
            if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Walk);
            PatrolGround();
            return;
        }

        SetHorizontalSpeed(0f);
        direction = delta.x >= 0f ? 1 : -1;
        ApplyFacing();

        // 레이저 발사 순간 공격 모션을 재생한 뒤 일어서기 모션으로 전환
        if (sniperAttackTimer > 0f)
        {
            sniperAttackTimer -= Time.deltaTime;
            if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Attack);
            UpdateLaser(false);
            if (sniperAttackTimer <= 0f)
            {
                sniperStandAfterShot = true;
                sniperStandTimer = sniperStandDuration;
                if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Stand);
            }
            return;
        }

        // 사격 직후에는 잠시 일어서는 모션을 재생한 뒤 다시 조준 상태로 복귀
        if (sniperStandAfterShot)
        {
            sniperStandTimer -= Time.deltaTime;
            if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Stand);
            if (sniperStandTimer <= 0f)
            {
                sniperStandAfterShot = false;
                if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Idle);
            }
            UpdateLaser(false);
            return;
        }

        // 감지 범위 안에서는 후퇴하지 않고 제자리에서 조준/대기
        if (laserTelegraphTimer <= 0f && attackTimer > 0f)
        {
            if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Idle);
            UpdateLaser(false);
            return;
        }

        if (laserTelegraphTimer <= 0f && attackTimer <= 0f)
        {
            laserTelegraphTimer = laserTelegraphTime;
            if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Sit);
        }

        if (laserTelegraphTimer > 0f)
        {
            laserTelegraphTimer -= Time.deltaTime;
            if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Sit);
            UpdateLaser(true);
            if (laserTelegraphTimer <= 0f)
            {
                attackTimer = laserCooldown;
                Vector2 origin = (Vector2)transform.position + Vector2.up * 0.25f;
                RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.right * direction, sniperLaserLength);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit2D hit in hits)
                {
                    if (hit.collider == null || hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
                    Health target = hit.collider.GetComponentInParent<Health>();
                    if (target != null && target.Team == Team.Player && Mathf.Abs(origin.y - player.position.y) <= laserVerticalTolerance)
                        DamagePlayer(sniperDamage, new Vector2(direction * Mathf.Abs(sniperKnockback.x), sniperKnockback.y));
                    break;
                }
                sniperAttackTimer = sniperAttackAnimationDuration;
                sniperStandAfterShot = false;
                UpdateLaser(false);
                if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Attack);
            }
        }
        else
        {
            if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Idle);
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

    // ------------------------------------------------------------------ Sprint Bot
    // 탐색(walk) → 인식(scan) → 돌진(run) → 정지(stop) → [범위 안이면 scan부터 다시 / 아니면 walk]
    // ActionState 매핑: Patrol=walk, Recognizing=scan, Active=run, Recovery=stop

    private bool SprintCanSee(Vector2 delta)
    {
        float range = delta.x * direction >= 0f ? scoutFrontRange : scoutBackRange;
        return Mathf.Abs(delta.x) <= range && Mathf.Abs(delta.y) <= verticalDetectionTolerance;
    }

    private void SprintFacePlayer(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > 0.01f) direction = delta.x >= 0f ? 1 : -1;
        ApplyFacing();
    }

    private void BeginSprintScan(Vector2 delta)
    {
        SprintFacePlayer(delta);
        actionState = ActionState.Recognizing;
        stateTimer = recognitionDelay;
        SetHorizontalSpeed(0f);
        if (sprintAnimation != null) sprintAnimation.Play(SprintBotAnimation.State.Scan, recognitionDelay);
    }

    private void UpdateSprint(Vector2 delta)
    {
        switch (actionState)
        {
            case ActionState.Patrol:
                if (SprintCanSee(delta))
                {
                    BeginSprintScan(delta);
                    return;
                }
                PatrolGround();
                if (sprintAnimation != null) sprintAnimation.Play(SprintBotAnimation.State.Walk);
                break;

            case ActionState.Recognizing:
                SetHorizontalSpeed(0f);
                SprintFacePlayer(delta);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    // 돌진 방향은 이 시점에 고정한다 (플레이어를 지나쳤는지 판정하기 위해)
                    runDirection = direction;
                    actionState = ActionState.Active;
                    if (sprintAnimation != null) sprintAnimation.Play(SprintBotAnimation.State.Run);
                }
                else if (sprintAnimation != null)
                {
                    sprintAnimation.Play(SprintBotAnimation.State.Scan, recognitionDelay);
                }
                break;

            case ActionState.Active:
                UpdateSprintRun(delta);
                break;

            case ActionState.Recovery:
                UpdateSprintStop(delta);
                break;
        }
    }

    private void UpdateSprintRun(Vector2 delta)
    {
        direction = runDirection;
        ApplyFacing();
        SetHorizontalSpeed(runDirection * patrolSpeed * chargeSpeedMultiplier);
        if (sprintAnimation != null) sprintAnimation.Play(SprintBotAnimation.State.Run);

        // 달리는 도중 닿으면 피해
        if (Mathf.Abs(delta.x) <= sprintContactRange && Mathf.Abs(delta.y) <= sprintContactVerticalRange)
        {
            DamagePlayer(sprintContactDamage, Vector2.zero);
        }

        Vector2 pos = body != null ? body.position : (Vector2)transform.position;
        bool nearWall = Physics2D.Raycast(pos + Vector2.up * 0.1f, Vector2.right * runDirection, sprintWallStopDistance, GameLayers.GroundMaskValue);
        bool passedPlayer = delta.x * runDirection < -sprintPassDistance;

        if (nearWall || passedPlayer) BeginSprintStop();
    }

    private void BeginSprintStop()
    {
        actionState = ActionState.Recovery;
        stateTimer = sprintStopDuration;
        if (sprintAnimation != null) sprintAnimation.Play(SprintBotAnimation.State.Stop, sprintStopDuration);
    }

    private void UpdateSprintStop(Vector2 delta)
    {
        stateTimer -= Time.deltaTime;

        // 감속하며 미끄러진다. 벽이 바로 앞이면 즉시 멈춘다.
        Vector2 pos = body != null ? body.position : (Vector2)transform.position;
        bool wallAhead = Physics2D.Raycast(pos + Vector2.up * 0.1f, Vector2.right * runDirection, wallCheckDistance, GameLayers.GroundMaskValue);
        float t = Mathf.Clamp01(stateTimer / Mathf.Max(0.05f, sprintStopDuration));
        SetHorizontalSpeed(wallAhead ? 0f : runDirection * patrolSpeed * chargeSpeedMultiplier * t);

        if (stateTimer > 0f) return;

        SetHorizontalSpeed(0f);
        if (SprintCanSee(delta))
            BeginSprintScan(delta);          // 아직 인식 범위 안 → 인식부터 다시
        else
        {
            actionState = ActionState.Patrol; // 범위 밖 → 탐색
            if (sprintAnimation != null) sprintAnimation.Play(SprintBotAnimation.State.Walk);
        }
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
        if (spriteRenderer != null && droneAnimation == null && sprintAnimation == null)
            spriteRenderer.color = alertColor;
    }

    private void RestoreBaseColor()
    {
        if (spriteRenderer != null && droneAnimation == null && sprintAnimation == null)
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
        ApplyFacing();
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

    // Reliable body-contact check independent of Collision/Trigger callbacks.
    // Health.TakeDamage already enforces the player's existing invulnerability window.
    private void FixedUpdate()
    {
        if (dead || player == null || selfCollider == null) return;

        Health target = player.GetComponentInParent<Health>();
        if (target == null || target.Team != Team.Player || target.IsDead) return;

        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider == null || !playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy) continue;

            ColliderDistance2D contact = selfCollider.Distance(playerCollider);
            if (contact.isValid && contact.distance <= 0f)
            {
                ApplyBodyContactDamage(target, contact.pointA);
                return;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!dead && actionState == ActionState.Active && collision.collider.gameObject.layer == GameLayers.Ground)
        {
            if (type == BotType.SprintBot)
            {
                // 진행 방향의 벽(거의 수직인 면)에 부딪혔을 때만 정지. 경사/바닥 접촉은 무시.
                for (int i = 0; i < collision.contactCount; i++)
                {
                    Vector2 n = collision.GetContact(i).normal;
                    if (Mathf.Abs(n.x) > 0.7f && n.x * runDirection < 0f) { BeginSprintStop(); break; }
                }
            }
            else if (type == BotType.MixerChargeBot && attackTimer <= 0f) BeginSpin();
        }
        TryBodyContactAttack(collision.otherCollider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryBodyContactAttack(collision.otherCollider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryBodyContactAttack(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryBodyContactAttack(other);
    }

    private void TryBodyContactAttack(Collider2D other)
    {
        if (dead || other == null) return;

        Health target = other.GetComponentInParent<Health>();
        if (target == null || target.Team != Team.Player || target.IsDead) return;

        // Drone contact keeps its original aggro/stun restrictions.
        if (type == BotType.Drone)
        {
            if (IsDroneStunned) return;
            if (droneContactOnlyInAggro && droneState != DroneState.Aggro) return;
        }

        ApplyBodyContactDamage(target, other.ClosestPoint(transform.position));
    }

    private void ApplyBodyContactDamage(Health target, Vector2 hitPoint)
    {
        if (target == null || target.Team != Team.Player || target.IsDead) return;

        // Existing Health invulnerability handles repeat contact; no extra cooldown or knockback.
        int sourceDamage = type == BotType.Drone ? droneContactDamage : bodyContactDamage;
        DamageInfo info = new DamageInfo
        {
            amount = Mathf.Max(1, sourceDamage),
            knockback = Vector2.zero,
            sourceTeam = Team.Enemy,
            source = gameObject,
            hitPoint = hitPoint
        };
        target.TakeDamage(info);
    }

    private void OnDamaged(DamageInfo info)
    {
        if (type == BotType.Sniper)
        {
            actionState = ActionState.Active;
            stateTimer = 0f;
            attackTimer = 0f;
            laserTelegraphTimer = laserTelegraphTime;
            sniperAttackTimer = 0f;
            sniperStandAfterShot = false;
            if (droneAnimation != null)
            {
                droneAnimation.Play(DroneBotAnimation.State.Sit);
                droneAnimation.PlayHit();
            }
        }

        if (type == BotType.Drone)
        {
            if (droneState == DroneState.Free) TriggerDroneRecognition();
            if (hitReaction == null)
            {
                droneHitStunTimer = Mathf.Max(0f, droneHitStunDuration);
                droneKnockbackVelocity = info.knockback * droneHitKnockbackScale;
                SetVelocity(droneKnockbackVelocity);
                if (droneAnimation != null && droneHitStunTimer > 0f) droneAnimation.PlayHit(droneHitStunTimer);
            }
            else if (droneAnimation != null) droneAnimation.PlayHit();
            return;
        }

        if (type == BotType.SprintBot)
        {
            // 피격 시 어그로: 정찰/정지(쿨다운) 상태여도 바로 인식(scan) 단계로 진입. 돌진 중이면 그대로 달린다.
            if (actionState == ActionState.Patrol || actionState == ActionState.Recovery)
            {
                Vector2 d = player != null ? (Vector2)(player.position - transform.position) : Vector2.zero;
                BeginSprintScan(d);
            }
            if (sprintAnimation != null) sprintAnimation.PlayHit();
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
                Gizmos.DrawWireSphere(origin, sniperRange);
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