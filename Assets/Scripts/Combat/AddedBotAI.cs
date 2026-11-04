using UnityEngine;

[RequireComponent(typeof(Health), typeof(Collider2D), typeof(Rigidbody2D))]
public class AddedBotAI : MonoBehaviour
{
    public enum Kind { ChargeBot, MixerBot, MixerChargeBot }
    private enum State { Patrol, Recognizing, Charge, Spin, Recovery }

    [Header("Bot")]
    [SerializeField] private Kind kind = Kind.ChargeBot;
    [SerializeField] private Transform player;
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0)] private int goldReward = 1;
    [Header("Movement")]
    [SerializeField, Min(0)] private float patrolSpeed = 1.2f;
    [SerializeField, Min(1)] private float chargeSpeedMultiplier = 1.5f;
    [SerializeField, Min(0.1f)] private float wallCheckDistance = 0.55f;
    [SerializeField, Min(0.1f)] private float groundCheckDistance = 1.3f;
    [SerializeField, Min(0)] private float recoverySeconds = 2f;
    [Header("Detection")]
    [SerializeField, Min(0)] private float frontRange = 6f;
    [SerializeField, Min(0)] private float backRange = 3f;
    [SerializeField, Min(0)] private float verticalRange = 2.5f;
    [SerializeField, Min(0)] private float recognitionSeconds = 0.35f;
    [Header("Mixer attack")]
    [SerializeField, Min(0.1f)] private float attackRange = 1.25f;
    [SerializeField, Min(0.1f)] private float attackVerticalRange = 1.2f;
    [SerializeField, Min(1)] private int attackDamage = 1;
    [SerializeField, Min(1)] private int spinHitCount = 2;
    [SerializeField, Min(0.01f)] private float spinHitInterval = 0.18f;
    [SerializeField, Min(0.05f)] private float spinDuration = 0.42f;
    [SerializeField, Min(0)] private float attackCooldown = 2f;
    [SerializeField] private Vector2 knockback = new Vector2(3f, 1.5f);
    [Header("Visual")]
    [SerializeField] private Color recognitionColor = Color.yellow;

    private Rigidbody2D rb;
    private Health health;
    private SpriteRenderer sprite;
    private Color originalColor;
    private State state = State.Patrol;
    private int direction = 1;
    private float timer, hitTimer, cooldown;
    private int hitsLeft;
    private bool dead;
    private Transform attackVisualTransform;
    private SpriteRenderer attackVisualRenderer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); health = GetComponent<Health>(); sprite = GetComponent<SpriteRenderer>();
        if (sprite) { if (sprite.sprite == null) sprite.sprite = RuntimeSprites.White; originalColor = sprite.color; }
        rb.bodyType = RigidbodyType2D.Dynamic; rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        health.Configure(Team.Enemy, Mathf.Max(1, maxHealth), 0f); health.Died += OnDied;
        if (GetComponent<EnemyHealthBar>() == null) gameObject.AddComponent<EnemyHealthBar>();
        CreateAttackVisual();
        FindPlayer();
    }

    private void FindPlayer()
    {
        if (player != null) return;
        var pc = FindObjectOfType<PlayerController2D>();
        if (pc != null) player = pc.transform;
        else { var go = GameObject.FindGameObjectWithTag("Player"); if (go) player = go.transform; }
    }

    private void Update()
    {
        if (dead) return;
        if (player == null) FindPlayer();
        if (player == null) { Patrol(); return; }
        Vector2 delta = player.position - transform.position;
        direction = delta.x >= 0 ? 1 : -1;
        if (sprite && state != State.Patrol) sprite.flipX = direction < 0;
        if (cooldown > 0) cooldown -= Time.deltaTime;

        if (state == State.Patrol && CanSeePlayer(delta))
        {
            state = State.Recognizing; timer = recognitionSeconds;
            if (sprite) sprite.color = recognitionColor;
            SetX(0);
        }

        switch (state)
        {
            case State.Patrol: Patrol(); break;
            case State.Recognizing:
                SetX(0); timer -= Time.deltaTime;
                if (timer <= 0) { if (sprite) sprite.color = originalColor; state = State.Charge; }
                break;
            case State.Charge: Charge(delta); break;
            case State.Spin: Spin(delta); break;
            case State.Recovery:
                SetX(0); timer -= Time.deltaTime;
                if (timer <= 0) { state = State.Patrol; direction *= -1; }
                break;
        }
    }

    private bool CanSeePlayer(Vector2 delta)
    {
        var playerSprite = player.GetComponentInChildren<SpriteRenderer>();
        bool facingBot = true;
        if (playerSprite) facingBot = (delta.x >= 0) == !playerSprite.flipX;
        float range = facingBot ? frontRange : backRange;
        return Mathf.Abs(delta.x) <= range && Mathf.Abs(delta.y) <= verticalRange;
    }

    private void Patrol()
    {
        Vector2 pos = rb.position;
        int groundMask = GameLayers.GroundMaskValue;
        bool wall = Physics2D.Raycast(pos + Vector2.up * 0.1f, Vector2.right * direction, wallCheckDistance, groundMask);
        Vector2 ahead = pos + Vector2.right * direction * 0.45f + Vector2.down * 0.2f;
        bool floor = Physics2D.Raycast(ahead, Vector2.down, groundCheckDistance, groundMask);
        if (wall || !floor) direction *= -1;
        SetX(direction * patrolSpeed);
        if (sprite) sprite.flipX = direction < 0;
    }

    private void Charge(Vector2 delta)
    {
        SetX(direction * patrolSpeed * chargeSpeedMultiplier);
        bool wall = Physics2D.Raycast(rb.position + Vector2.up * 0.1f, Vector2.right * direction, wallCheckDistance, GameLayers.GroundMaskValue);
        bool close = Mathf.Abs(delta.x) <= attackRange && Mathf.Abs(delta.y) <= attackVerticalRange;
        if (kind == Kind.MixerBot && close) { BeginSpin(); return; }
        if (kind == Kind.MixerChargeBot && (close || wall)) { BeginSpin(); return; }
        if (kind == Kind.ChargeBot && (close || wall)) BeginRecovery();
    }

    private void CreateAttackVisual()
    {
        // 플레이어의 X키 공격 범위 표시와 같은 반투명 흰색 사각형.
        // 믹서기 봇 계열에만 생성하고, 실제 공격 중에만 표시한다.
        if (kind == Kind.ChargeBot) return;
        GameObject visual = new GameObject("MixerAttackRangeVisual");
        attackVisualTransform = visual.transform;
        attackVisualTransform.SetParent(transform, false);
        attackVisualRenderer = visual.AddComponent<SpriteRenderer>();
        attackVisualRenderer.sprite = RuntimeSprites.White;
        attackVisualRenderer.color = new Color(1f, 1f, 1f, 0.35f);
        attackVisualRenderer.sortingOrder = 5;
        attackVisualRenderer.enabled = false;
    }

    private void BeginSpin()
    {
        state = State.Spin; timer = spinDuration; hitTimer = 0; hitsLeft = spinHitCount; SetX(0);
        if (attackVisualRenderer != null) attackVisualRenderer.enabled = true;
    }

    private void Spin(Vector2 delta)
    {
        SetX(0); timer -= Time.deltaTime; hitTimer -= Time.deltaTime;
        if (attackVisualTransform != null && attackVisualRenderer != null)
        {
            float progress = 1f - Mathf.Clamp01(timer / Mathf.Max(0.01f, spinDuration));
            // 2번의 휘두르기: 첫 번째는 한쪽에서 반대쪽으로, 두 번째는 다시 반대로 쓸어준다.
            float phase = progress * 2f;
            float swingT = phase < 1f ? phase : phase - 1f;
            float angle = phase < 1f
                ? Mathf.Lerp(-55f, 55f, swingT)
                : Mathf.Lerp(55f, -55f, swingT);
            attackVisualTransform.localPosition = new Vector3(direction * attackRange * 0.5f, 0f, -0.1f);
            attackVisualTransform.localRotation = Quaternion.Euler(0f, 0f, angle * direction);
            attackVisualTransform.localScale = new Vector3(attackRange, attackVerticalRange * 2f, 1f);
        }
        if (hitsLeft > 0 && hitTimer <= 0)
        {
            if (Mathf.Abs(delta.x) <= attackRange && Mathf.Abs(delta.y) <= attackVerticalRange) DamagePlayer();
            hitsLeft--; hitTimer = spinHitInterval;
        }
        if (timer <= 0)
        {
            if (attackVisualRenderer != null) attackVisualRenderer.enabled = false;
            if (attackVisualTransform != null) attackVisualTransform.localRotation = Quaternion.identity;
            BeginRecovery(); cooldown = attackCooldown;
        }
    }

    private void BeginRecovery() { state = State.Recovery; timer = recoverySeconds; SetX(0); }
    private void DamagePlayer()
    {
        if (!player) return;
        Health target = player.GetComponentInParent<Health>();
        if (target == null || target.Team != Team.Player) return;
        target.TakeDamage(new DamageInfo { amount = Mathf.Max(1, attackDamage), knockback = new Vector2(direction * knockback.x, knockback.y), sourceTeam = Team.Enemy, source = gameObject, hitPoint = player.position });
    }
    private void SetX(float x) { Vector2 v = rb.velocity; v.x = x; rb.velocity = v; }
    private void OnCollisionEnter2D(Collision2D c)
    {
        if (!dead && state == State.Charge && c.collider.gameObject.layer == GameLayers.Ground && kind != Kind.MixerBot) BeginRecovery();
    }
    private void OnDied(Health h)
    {
        if (dead) return; dead = true; GearReward.Spawn(transform.position, goldReward); Destroy(gameObject);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, frontRange);
        Gizmos.color = Color.red; Gizmos.DrawWireCube(transform.position + Vector3.right * attackRange * 0.5f, new Vector3(attackRange, attackVerticalRange * 2, 0.05f));
    }
    private void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
        if (attackVisualTransform != null) Destroy(attackVisualTransform.gameObject);
    }
}
