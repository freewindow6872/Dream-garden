using UnityEngine;

[RequireComponent(typeof(Health), typeof(Collider2D))]
public class TestBotAI : MonoBehaviour
{
    public enum BotType { Scout, Sniper, Drone }
    [Header("Bot")]
    public BotType type = BotType.Scout;
    [SerializeField] private Transform player;
    [SerializeField] private float maxHealth = 3f;
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
    [SerializeField] private float recognitionDelay = .35f;
    [Header("Attacks")]
    [SerializeField] private int meleeDamage = 1;
    [SerializeField] private float meleeRange = 1.0f;
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
    private int direction = 1;
    private bool alerted, dead, recognizing;
    private float recognitionTimer, attackTimer, laserTelegraphTimer;
    private LineRenderer laser;
    private Color baseColor;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        health = GetComponent<Health>();
        if (spriteRenderer) baseColor = spriteRenderer.color;
        health.Configure(Team.Enemy, Mathf.Max(1, Mathf.RoundToInt(maxHealth)), 0f);
        health.Died += OnDied;
        if (player == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p) player = p.transform; }
        if (type == BotType.Sniper)
        {
            laser = gameObject.AddComponent<LineRenderer>();
            laser.positionCount = 2; laser.startWidth = laserWidth; laser.endWidth = laserWidth;
            laser.material = new Material(Shader.Find("Sprites/Default"));
            laser.startColor = Color.red; laser.endColor = Color.red; laser.enabled = false; laser.sortingOrder = 6;
        }
    }

    private void Update()
    {
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
        if (Mathf.Abs(delta.x) > meleeRange || Mathf.Abs(delta.y) > 1.2f)
            transform.position += Vector3.right * direction * (patrolSpeed * chaseSpeedMultiplier) * Time.deltaTime;
        attackTimer -= Time.deltaTime;
        if (Mathf.Abs(delta.x) <= meleeRange && Mathf.Abs(delta.y) <= 1.2f && attackTimer <= 0f)
        {
            attackTimer = meleeCooldown;
            DamagePlayer(meleeDamage, new Vector2(direction * 3f, 1.5f));
        }
    }

    private void UpdateDrone(Vector2 delta)
    {
        // Drone chases in a straight line, including vertically; its projectile attack is ranged.
        transform.position += (Vector3)(delta.sqrMagnitude > .001f ? delta.normalized : Vector2.right * direction) * (patrolSpeed * chaseSpeedMultiplier) * Time.deltaTime;
        attackTimer -= Time.deltaTime;
        if (delta.magnitude <= droneDetectionRadius && attackTimer <= 0f)
        {
            attackTimer = projectileCooldown;
            Vector2 from = transform.position;
            Vector2 target = (Vector2)player.position - from;
            EnemyBullet.Spawn(from, target.normalized, projectileSpeed, projectileDamage, projectileKnockback, projectileLifetime, gameObject);
        }
    }

    private void UpdateSniper(Vector2 delta)
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f && laserTelegraphTimer <= 0f) laserTelegraphTimer = laserTelegraphTime;
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
                    if (target != null && target.Team == Team.Player && Mathf.Abs(hit.point.y - player.position.y) <= laserVerticalTolerance)
                    {
                        DamagePlayer(projectileDamage, new Vector2(direction * 3f, 1f));
                    }
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
        transform.position += Vector3.right * direction * patrolSpeed * Time.deltaTime;
        if (spriteRenderer) spriteRenderer.flipX = direction < 0;
    }

    private void OnDied(Health h)
    {
        if (dead) return;
        dead = true; if (laser) laser.enabled = false;
        GearReward.Spawn(transform.position, gearReward);
        Destroy(gameObject);
    }
    private void OnDestroy() { if (health != null) health.Died -= OnDied; }
}

public class GearReward : MonoBehaviour
{
    private int amount = 1;
    private bool collected;
    public static void Spawn(Vector2 position, int value)
    {
        GameObject go = new GameObject("Gear Gold Reward"); go.transform.position = position;
        go.transform.localScale = new Vector3(.38f, .38f, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>(); sr.sprite = RuntimeSprites.White; sr.color = new Color(1f, .72f, .12f); sr.sortingOrder = 8;
        CircleCollider2D col = go.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = .7f;
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 1.2f; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.AddForce(new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 5f)), ForceMode2D.Impulse);
        GearReward reward = go.AddComponent<GearReward>(); reward.amount = Mathf.Max(0, value);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !other.CompareTag("Player")) return;
        collected = true; GoldCounter.Add(amount); Destroy(gameObject);
    }
}

public class GoldCounter : MonoBehaviour
{
    public static int Gold { get; private set; }
    private static GoldCounter instance;
    private GUIStyle style;
    [SerializeField] private string label = "Gold";
    [SerializeField] private Vector2 screenOffset = new Vector2(20f, 20f);
    [SerializeField] private int fontSize = 24;
    private void Awake() { instance = this; Gold = 0; }
    public static void Add(int amount) { Gold += Mathf.Max(0, amount); }
    private void OnGUI()
    {
        if (style == null) { style = new GUIStyle(GUI.skin.label); style.fontSize = fontSize; style.fontStyle = FontStyle.Bold; style.normal.textColor = Color.white; }
        GUI.Label(new Rect(Screen.width - 170f - screenOffset.x, screenOffset.y, 150f, 40f), label + ": " + Gold, style);
    }
}
