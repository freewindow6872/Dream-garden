using UnityEngine;

[RequireComponent(typeof(Health), typeof(Collider2D))]
public class TestBotAI : MonoBehaviour
{
    public enum BotType { Scout, Sniper, Drone }
    public enum DroneState { Free, Recognizing, Aggro }

    [Header("Bot")]
    public BotType type = BotType.Scout;
    [SerializeField] private Transform player;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int gearReward = 1;

    [Header("Movement (Ground Bots)")]
    [SerializeField] private float patrolSpeed = 1.2f;
    [SerializeField] private float chaseSpeedMultiplier = 1.5f;
    [SerializeField] private float patrolMinX = -4f;
    [SerializeField] private float patrolMaxX = 4f;
    [SerializeField] private float wallCheckDistance = .55f;
    [SerializeField] private float groundCheckDistance = 1.3f;

    [Header("Detection (Ground Bots)")]
    [SerializeField] private float scoutFrontRange = 6f;
    [SerializeField] private float scoutBackRange = 3f;
    [SerializeField] private float sniperRange = 7f;
    [SerializeField] private float verticalDetectionTolerance = 2.5f;
    [Tooltip("플레이어 발견 후 어그로 전환까지 걸리는 인식 시간(초)")]
    [SerializeField] private float recognitionDelay = .35f;

    [Header("Drone - Detection & Movement")]
    [Tooltip("현재 드론 상태 (Free: 자유 순찰, Recognizing: 플레이어 인식, Aggro: 어그로 추적/접촉 공격)")]
    [SerializeField] private DroneState droneState = DroneState.Free;
    [Tooltip("드론 원본 스프라이트가 왼쪽을 보고 그려져 있으면 체크")]
    [SerializeField] private bool droneSpriteFacesLeft = true;
    [Tooltip("플레이어를 감지해 인식 상태로 전환되는 범위")]
    [SerializeField] private float droneDetectionRadius = 8f;
    [Tooltip("어그로가 풀리고 자유 상태로 돌아가는 거리 (0이면 한번 인식 후 해제되지 않음)")]
    [SerializeField] private float droneLoseAggroRadius = 0f;
    [Tooltip("혼자 자유롭게 순찰(날기)할 때의 기본 이동 속도")]
    [SerializeField] private float droneFreePatrolSpeed = 0.8f;
    [Tooltip("자유 순찰 시 스폰 지점 기준 좌우 왕복 반경 (0이면 벽에 닿을 때까지 비행)")]
    [SerializeField] private float droneFreePatrolRadius = 3.5f;
    [Tooltip("플레이어에게 다가갈 때 자유 순찰 속도 대비 이동 속도 배율 (기본 1.7배)")]
    [SerializeField] private float droneAggroSpeedMultiplier = 1.7f;
    [Tooltip("플레이어 중심 기준 목표 Y 오프셋 (0이면 플레이어 몸통 정중앙으로 돌진)")]
    [SerializeField] private float droneTargetOffsetY = 0f;

    [Header("Drone - Contact Attack & Hit Stun")]
    [Tooltip("봇에 닿았을 때 플레이어에게 입히는 데미지")]
    [SerializeField] private int droneContactDamage = 1;
    [Tooltip("봇에 닿았을 때 플레이어를 밀어내는 넉백")]
    [SerializeField] private Vector2 droneContactKnockback = new Vector2(4.5f, 2.5f);
    [Tooltip("연속 접촉 공격 판정 최소 간격(초)")]
    [SerializeField] private float droneContactCooldown = 0.4f;
    [Tooltip("체크 시 어그로(공격) 상태일 때만 접촉 데미지 발생")]
    [SerializeField] private bool droneContactOnlyInAggro = true;
    [Tooltip("플레이어에게 공격을 맞았을 때 그 자리에서 멈춰 있는 경직 시간(초)")]
    [SerializeField] private float droneHitStunDuration = 0.35f;
    [Tooltip("피격 경직 중 밀려나는 넉백 배율 (0이면 제자리에 완전히 멈춤)")]
    [SerializeField] private float droneHitKnockbackScale = 0f;

    [Header("Ranged Spacing (Sniper)")]
    [Tooltip("원거리 봇이 유지하려는 플레이어와의 가로 거리")]
    [SerializeField] private float rangedPreferredDistance = 4.5f;
    [Tooltip("유지 거리에서 이만큼 벗어나도 제자리에서 공격")]
    [SerializeField] private float rangedDistanceTolerance = 0.75f;
    [Tooltip("거리를 조절할 때 이동 속도 배율")]
    [SerializeField] private float rangedRepositionSpeedMultiplier = 1f;

    [Header("Attacks (Ground Bots)")]
    [SerializeField] private int meleeDamage = 1;
    [SerializeField] private float meleeRange = 1.0f;
    [Tooltip("근접 공격 판정의 세로 허용 거리")]
    [SerializeField] private float meleeVerticalRange = 1.2f;
    [SerializeField] private float meleeCooldown = 1.0f;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float sniperLaserLength = 12f;
    [SerializeField] private float laserCooldown = 1.8f;
    [SerializeField] private float laserTelegraphTime = .55f;
    [SerializeField] private float laserVerticalTolerance = .65f;
    [SerializeField] private float laserWidth = .055f;

    [Header("Visual")]
    [SerializeField] private Color alertColor = Color.yellow;

    private SpriteRenderer spriteRenderer;
    private DroneBotAnimation droneAnimation;
    private HitReaction hitReaction;
    private Health health;
    private Rigidbody2D body;
    private Collider2D selfCollider;
    private Collider2D playerCollider;
    private Vector2 spawnPosition;
    private Vector2 droneKnockbackVelocity;
    private int direction = 1;
    private bool alerted, dead, recognizing;
    private float recognitionTimer, attackTimer, laserTelegraphTimer;
    private float droneHitStunTimer, droneContactTimer;
    private LineRenderer laser;
    private Color baseColor;

    public DroneState CurrentDroneState => droneState;
    public bool IsDroneStunned => droneHitStunTimer > 0f || (hitReaction != null && hitReaction.IsStunned);

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
        if (type == BotType.Drone) ApplyDroneFlip();

        if (spriteRenderer) baseColor = spriteRenderer.color;
        health.Configure(Team.Enemy, Mathf.Max(1, Mathf.RoundToInt(maxHealth)), 0f);
        health.Died += OnDied;
        health.Damaged += OnDamaged;
        if (GetComponent<EnemyHealthBar>() == null) gameObject.AddComponent<EnemyHealthBar>();
        ResolvePlayer();
        EnsureLaser();
    }

    private void ResolvePlayer()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (player != null && playerCollider == null)
        {
            playerCollider = player.GetComponent<Collider2D>();
        }
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

    private void SetAlertTint()
    {
        if (spriteRenderer && droneAnimation == null) spriteRenderer.color = alertColor;
    }

    private void Update()
    {
        EnsureLaser();
        if (dead) return;

        if (player == null) ResolvePlayer();

        if (type == BotType.Drone)
        {
            UpdateDroneStateMachine();
            return;
        }

        if (player == null) { Patrol(); return; }

        Vector2 delta = player.position - transform.position;
        float dx = Mathf.Abs(delta.x), dy = Mathf.Abs(delta.y);
        bool sees = type == BotType.Scout
            ? dx <= (delta.x * direction >= 0 ? scoutFrontRange : scoutBackRange) && dy <= verticalDetectionTolerance
            : dx <= sniperRange && dy <= verticalDetectionTolerance;

        if (!alerted && sees)
        {
            alerted = true;
            recognizing = true;
            recognitionTimer = recognitionDelay;
            SetAlertTint();
        }
        if (recognizing)
        {
            recognitionTimer -= Time.deltaTime;
            if (recognitionTimer <= 0f)
            {
                recognizing = false;
                if (spriteRenderer) spriteRenderer.color = baseColor;
            }
            else
            {
                if (type == BotType.Sniper) UpdateLaser(false);
                return;
            }
        }
        if (!alerted) { Patrol(); return; }

        direction = delta.x >= 0 ? 1 : -1;
        if (spriteRenderer) spriteRenderer.flipX = direction < 0;
        switch (type)
        {
            case BotType.Scout: UpdateScout(delta); break;
            case BotType.Sniper: UpdateSniper(delta); break;
        }
    }

    private void UpdateDroneStateMachine()
    {
        if (droneContactTimer > 0f)
            droneContactTimer -= Time.deltaTime;

        // 피격 경직 처리: 그 자리에서 잠깐 멈춘 뒤 경직이 풀리면 다시 플레이어에게 다가감
        if (droneHitStunTimer > 0f)
        {
            droneHitStunTimer -= Time.deltaTime;
            if (droneHitKnockbackScale <= 0.001f)
            {
                SetDroneVelocity(Vector2.zero);
            }
            else if (hitReaction == null || !hitReaction.IsStunned)
            {
                droneKnockbackVelocity = Vector2.Lerp(droneKnockbackVelocity, Vector2.zero, Time.deltaTime * 14f);
                SetDroneVelocity(droneKnockbackVelocity);
            }

            if (droneHitStunTimer <= 0f)
            {
                droneKnockbackVelocity = Vector2.zero;
                if (hitReaction == null || !hitReaction.IsStunned)
                    SetDroneVelocity(Vector2.zero);
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
                SetDroneVelocity(Vector2.zero);
                FaceTowards(delta.x);
                if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Scan);

                recognitionTimer -= Time.deltaTime;
                if (recognitionTimer <= 0f)
                {
                    recognizing = false;
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

                // 혼자 움직이는 속도(droneFreePatrolSpeed) * 배율(droneAggroSpeedMultiplier, 기본 1.7배) 속도로 추적
                Vector2 targetPos = playerCenter + Vector2.up * droneTargetOffsetY;
                Vector2 toTarget = targetPos - (Vector2)transform.position;
                Vector2 moveDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;

                if (Mathf.Abs(moveDir.x) > 0.01f &&
                    Physics2D.Raycast(transform.position, Vector2.right * Mathf.Sign(moveDir.x), wallCheckDistance, GameLayers.GroundMaskValue))
                {
                    moveDir.x = 0f;
                }

                float aggroChaseSpeed = droneFreePatrolSpeed * Mathf.Max(0f, droneAggroSpeedMultiplier);
                SetDroneVelocity(moveDir * aggroChaseSpeed);
                break;
        }
    }

    private void EnterDroneFree()
    {
        alerted = false;
        recognizing = false;
        droneState = DroneState.Free;
        if (droneAnimation != null) droneAnimation.Play(DroneBotAnimation.State.Fly);
    }

    private void TriggerDroneRecognition()
    {
        alerted = true;
        recognizing = true;
        recognitionTimer = recognitionDelay;
        droneState = DroneState.Recognizing;
        SetAlertTint();
        SetDroneVelocity(Vector2.zero);
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
        SetDroneVelocity(new Vector2(direction * droneFreePatrolSpeed, 0f));
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

    private void SetDroneVelocity(Vector2 velocity)
    {
        if (body == null) return;
        body.velocity = velocity;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
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
        {
            droneContactTimer = droneContactCooldown;
        }
    }

    private void UpdateScout(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > meleeRange || Mathf.Abs(delta.y) > meleeVerticalRange)
            SetHorizontalSpeed(direction * patrolSpeed * chaseSpeedMultiplier);
        else
            SetHorizontalSpeed(0f);
        attackTimer -= Time.deltaTime;
        if (Mathf.Abs(delta.x) <= meleeRange && Mathf.Abs(delta.y) <= meleeVerticalRange && attackTimer <= 0f)
        {
            attackTimer = meleeCooldown;
            DamagePlayer(meleeDamage, new Vector2(direction * 3f, 1.5f));
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
        direction = delta.x >= 0 ? 1 : -1;

        attackTimer -= Time.deltaTime;
        bool inFiringPosition = distanceX <= sniperRange && distanceX >= preferred - tolerance &&
                                distanceX <= preferred + tolerance && Mathf.Abs(delta.y) <= laserVerticalTolerance;
        if (inFiringPosition && attackTimer <= 0f && laserTelegraphTimer <= 0f) laserTelegraphTimer = laserTelegraphTime;
        if (!inFiringPosition && laserTelegraphTimer <= 0f) { UpdateLaser(false); return; }
        if (laserTelegraphTimer > 0f)
        {
            laserTelegraphTimer -= Time.deltaTime;
            UpdateLaser(true);
            if (laserTelegraphTimer <= 0f)
            {
                attackTimer = laserCooldown;
                Vector2 origin = (Vector2)transform.position + Vector2.up * .25f;
                RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.right * direction, sniperLaserLength);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit2D hit in hits)
                {
                    if (hit.collider == null || hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
                    Health target = hit.collider.GetComponentInParent<Health>();
                    if (target != null && target.Team == Team.Player && Mathf.Abs(origin.y - player.position.y) <= laserVerticalTolerance)
                        DamagePlayer(projectileDamage, new Vector2(direction * 3f, 1f));
                    break;
                }
            }
        }
        else UpdateLaser(false);
    }

    private void UpdateLaser(bool visible)
    {
        if (laser == null) return;
        laser.startWidth = laserWidth; laser.endWidth = laserWidth;
        Vector3 from = transform.position + Vector3.up * .25f;
        laser.SetPosition(0, from); laser.SetPosition(1, from + Vector3.right * direction * sniperLaserLength);
        laser.enabled = visible;
    }

    private void DamagePlayer(int amount, Vector2 knockback)
    {
        if (player == null) return;
        Health ph = player.GetComponentInParent<Health>();
        if (ph == null || ph.Team != Team.Player) return;
        DamageInfo info = new DamageInfo { amount = Mathf.Max(1, amount), knockback = knockback, sourceTeam = Team.Enemy, source = gameObject, hitPoint = player.position };
        ph.TakeDamage(info);
    }

    private void Patrol()
    {
        Vector2 origin = transform.position;
        bool wall = Physics2D.Raycast(origin, Vector2.right * direction, wallCheckDistance, GameLayers.GroundMaskValue);
        bool groundAhead = Physics2D.Raycast(origin + Vector2.right * direction * .45f + Vector2.down * .25f, Vector2.down, groundCheckDistance, GameLayers.GroundMaskValue);
        if (transform.position.x <= patrolMinX || transform.position.x >= patrolMaxX || wall || !groundAhead) direction *= -1;
        SetHorizontalSpeed(direction * patrolSpeed);
        if (spriteRenderer) spriteRenderer.flipX = direction < 0;
    }

    private void SetHorizontalSpeed(float speed)
    {
        if (body == null) return;
        Vector2 velocity = body.velocity;
        velocity.x = speed;
        body.velocity = velocity;
    }

    private void OnDamaged(DamageInfo info)
    {
        if (type == BotType.Drone)
        {
            if (droneState == DroneState.Free)
            {
                TriggerDroneRecognition();
            }

            // 그 자리에서 잠깐 멈추는 경직 진입 + 피격 프레임 재생
            droneHitStunTimer = Mathf.Max(0f, droneHitStunDuration);
            droneKnockbackVelocity = info.knockback * droneHitKnockbackScale;
            if (hitReaction == null || !hitReaction.IsStunned)
                SetDroneVelocity(droneKnockbackVelocity);

            if (droneAnimation != null && droneHitStunTimer > 0f)
                droneAnimation.PlayHit(droneHitStunTimer);
            return;
        }

        if (!alerted)
        {
            alerted = true;
            recognizing = true;
            recognitionTimer = recognitionDelay;
            SetAlertTint();
        }
    }

    private void OnDied(Health h)
    {
        if (dead) return;
        dead = true; if (laser) laser.enabled = false;
        GearReward.Spawn(transform.position, gearReward);
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        if (type == BotType.Scout)
        {
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.85f);
            int dir = Application.isPlaying ? direction : 1;
            Vector3 center = origin + Vector3.right * dir * (meleeRange * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(meleeRange, meleeVerticalRange * 2f, 0.05f));
        }
        else if (type == BotType.Drone)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(origin, droneDetectionRadius);

            Collider2D col = selfCollider != null ? selfCollider : GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }

            if (droneLoseAggroRadius > 0f)
            {
                Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.45f);
                Gizmos.DrawWireSphere(origin, droneLoseAggroRadius);
            }

            if (droneFreePatrolRadius > 0f)
            {
                Vector3 center = Application.isPlaying ? (Vector3)spawnPosition : origin;
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
                Gizmos.DrawLine(center + Vector3.left * droneFreePatrolRadius, center + Vector3.right * droneFreePatrolRadius);
            }
        }
        else
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            int dir = Application.isPlaying ? direction : 1;
            Gizmos.DrawLine(origin + Vector3.up * .25f, origin + Vector3.up * .25f + Vector3.right * dir * sniperLaserLength);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, rangedPreferredDistance);
        }
    }

    private void OnDestroy() { if (health != null) { health.Died -= OnDied; health.Damaged -= OnDamaged; } }
}