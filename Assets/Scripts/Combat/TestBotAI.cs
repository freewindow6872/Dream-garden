using UnityEngine;

[RequireComponent(typeof(Health), typeof(Collider2D))]
public class TestBotAI : MonoBehaviour
{
    public enum BotType { Scout, Sniper, Drone }
    [Header("Bot")]
    public BotType type = BotType.Scout;
    [SerializeField] private Transform player;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int gearReward = 1;
    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 1.2f;
    [SerializeField] private float chaseSpeedMultiplier = 1.5f;
    [SerializeField] private float patrolMinX = -4f;
    [SerializeField] private float patrolMaxX = 4f;
    [SerializeField] private float wallCheckDistance = .55f;
    [SerializeField] private float groundCheckDistance = 1.3f;
    [Header("Detection (world units)")]
    [SerializeField] private float scoutFrontRange = 6f;
    [SerializeField] private float scoutBackRange = 3f;
    [SerializeField] private float sniperRange = 7f;
    [SerializeField] private float droneDetectionRadius = 8f;
    [SerializeField] private float verticalDetectionTolerance = 2.5f;
    [Header("Ranged spacing")]
    [Tooltip("원거리 봇이 유지하려는 플레이어와의 가로 거리")]
    [SerializeField] private float rangedPreferredDistance = 4.5f;
    [Tooltip("유지 거리에서 이만큼 벗어나도 제자리에서 공격")]
    [SerializeField] private float rangedDistanceTolerance = 0.75f;
    [Tooltip("거리를 조절할 때 이동 속도 배율")]
    [SerializeField] private float rangedRepositionSpeedMultiplier = 1f;
    [SerializeField] private float recognitionDelay = .35f;
    [Header("Attacks")]
    [SerializeField] private int meleeDamage = 1;
    [SerializeField] private float meleeRange = 1.0f;
    [Tooltip("근접 공격 판정의 세로 허용 거리")]
    [SerializeField] private float meleeVerticalRange = 1.2f;
    [SerializeField] private float meleeCooldown = 1.0f;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float projectileSpeed = 6f;
    [SerializeField] private float projectileCooldown = 1.8f;
    [SerializeField] private float projectileLifetime = 3f;
    [SerializeField] private Vector2 projectileKnockback = new Vector2(3f, 2f);
    [SerializeField] private float sniperLaserLength = 12f;
    [SerializeField] private float laserCooldown = 1.8f;
    [SerializeField] private float laserTelegraphTime = .55f;
    [SerializeField] private float laserVerticalTolerance = .65f;
    [SerializeField] private float laserWidth = .055f;
    [Header("Visual")]
    [SerializeField] private Color alertColor = Color.yellow;

    private SpriteRenderer spriteRenderer;
    private Health health;
    private Rigidbody2D body;
    private int direction = 1;
    private bool alerted, dead, recognizing;
    private float recognitionTimer, attackTimer, laserTelegraphTimer;
    private LineRenderer laser;
    private Color baseColor;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = type == BotType.Drone ? 0f : 3f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (spriteRenderer) baseColor = spriteRenderer.color;
        health.Configure(Team.Enemy, Mathf.Max(1, Mathf.RoundToInt(maxHealth)), 0f);
        health.Died += OnDied;
        if (GetComponent<EnemyHealthBar>() == null) gameObject.AddComponent<EnemyHealthBar>();
        if (player == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p) player = p.transform; }
        EnsureLaser();
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

    private void Update()
    {
        EnsureLaser(); // Bootstrap이 런타임에 봇 종류를 지정하는 경우에도 저격 레이저 생성
        if (dead) return;
        if (player == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p) player = p.transform; else { Patrol(); return; } }
        Vector2 delta = player.position - transform.position;
        float dx = Mathf.Abs(delta.x), dy = Mathf.Abs(delta.y);
        bool sees = type == BotType.Scout
            ? dx <= (delta.x * direction >= 0 ? scoutFrontRange : scoutBackRange) && dy <= verticalDetectionTolerance
            : type == BotType.Sniper ? dx <= sniperRange && dy <= verticalDetectionTolerance
            : delta.magnitude <= droneDetectionRadius;

        if (!alerted && sees) { alerted = true; recognizing = true; recognitionTimer = recognitionDelay; if (spriteRenderer) spriteRenderer.color = alertColor; }
        if (recognizing)
        {
            recognitionTimer -= Time.deltaTime;
            if (recognitionTimer <= 0f) { recognizing = false; if (spriteRenderer) spriteRenderer.color = baseColor; }
            else { if (type == BotType.Sniper) UpdateLaser(false); return; }
        }
        if (!alerted) { Patrol(); return; }

        direction = delta.x >= 0 ? 1 : -1;
        if (spriteRenderer) spriteRenderer.flipX = direction < 0;
        switch (type)
        {
            case BotType.Scout: UpdateScout(delta); break;
            case BotType.Sniper: UpdateSniper(delta); break;
            case BotType.Drone: UpdateDrone(delta); break;
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

    private void UpdateDrone(Vector2 delta)
    {
        // 원거리 공격을 위해 플레이어에게 달라붙지 않고 지정한 가로 거리를 유지한다.
        float distanceX = Mathf.Abs(delta.x);
        float preferred = Mathf.Max(0.1f, rangedPreferredDistance);
        float tolerance = Mathf.Max(0f, rangedDistanceTolerance);
        float moveDir = 0f;
        if (distanceX > preferred + tolerance) moveDir = direction;       // 너무 멀면 접근
        else if (distanceX < preferred - tolerance) moveDir = -direction; // 너무 가까우면 후퇴
        if (Mathf.Abs(delta.y) > verticalDetectionTolerance) moveDir = 0f;
        SetHorizontalSpeed(moveDir * patrolSpeed * rangedRepositionSpeedMultiplier);

        attackTimer -= Time.deltaTime;
        if (distanceX <= droneDetectionRadius && distanceX >= preferred - tolerance && distanceX <= preferred + tolerance &&
            Mathf.Abs(delta.y) <= verticalDetectionTolerance && attackTimer <= 0f)
        {
            attackTimer = projectileCooldown;
            Vector2 from = transform.position + Vector3.up * 0.1f;
            Vector2 target = (Vector2)player.position - from;
            EnemyBullet.Spawn(from, target.normalized, projectileSpeed, projectileDamage, projectileKnockback, projectileLifetime, gameObject);
        }
    }

    private void UpdateSniper(Vector2 delta)
    {
        // 저격봇도 너무 가까워지면 후퇴하고, 사격 거리 밖이면 천천히 접근한다.
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
                    // 첫 번째 유효한 장애물/대상에서 레이저가 막힌다.
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
        if (type != BotType.Drone)
        {
            Vector2 origin = transform.position;
            bool wall = Physics2D.Raycast(origin, Vector2.right * direction, wallCheckDistance, GameLayers.GroundMaskValue);
            bool groundAhead = Physics2D.Raycast(origin + Vector2.right * direction * .45f + Vector2.down * .25f, Vector2.down, groundCheckDistance, GameLayers.GroundMaskValue);
            if (transform.position.x <= patrolMinX || transform.position.x >= patrolMaxX || wall || !groundAhead) direction *= -1;
        }
        else if (Physics2D.Raycast(transform.position, Vector2.right * direction, wallCheckDistance, GameLayers.GroundMaskValue)) direction *= -1;
        SetHorizontalSpeed(direction * patrolSpeed);
        if (spriteRenderer) spriteRenderer.flipX = direction < 0;
    }

    private void SetHorizontalSpeed(float speed)
    {
        if (body == null) return;
        Vector2 velocity = body.velocity;
        velocity.x = speed;
        if (type == BotType.Drone) velocity.y = 0f;
        body.velocity = velocity;
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
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, rangedPreferredDistance);
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

    private void OnDestroy() { if (health != null) health.Died -= OnDied; }
}
