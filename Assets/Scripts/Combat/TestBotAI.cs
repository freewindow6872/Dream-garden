using UnityEngine;
using System.Collections;

public class TestBotAI : MonoBehaviour
{
    public enum BotType { Scout, Sniper, Drone }
    public BotType type;
    public float patrolSpeed = 1.2f;
    public float chargeSpeed = 4.5f;
    public float patrolMinX = -4f, patrolMaxX = 4f;
    public Transform player;
    public float healthPoints = 3f;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Health health;
    int dir = 1;
    bool alerted, dead, aiming;
    float fireTimer;
    LineRenderer laser;
    float alertTimer;
    Vector3 startPos;

    void Awake() {
        rb = GetComponent<Rigidbody2D>(); sr = GetComponent<SpriteRenderer>(); health = GetComponent<Health>();
        if (health == null) health = gameObject.AddComponent<Health>();
        health.Configure(Team.Enemy, 3, 0);
        health.Died += OnDied;
        startPos = transform.position;
        if (type == BotType.Sniper) {
            laser = gameObject.AddComponent<LineRenderer>();
            laser.positionCount = 2; laser.startWidth = .055f; laser.endWidth = .055f;
            laser.material = new Material(Shader.Find("Sprites/Default")); laser.startColor = Color.red; laser.endColor = Color.red;
            laser.enabled = false; laser.sortingOrder = 5;
        }
    }
    void Update() {
        if (dead || player == null) return;
        Vector2 delta = player.position - transform.position;
        float dx = Mathf.Abs(delta.x), dy = Mathf.Abs(delta.y);
        bool sees = type == BotType.Scout ? (dx <= (player.position.x * dir >= transform.position.x * dir ? 6f : 3f) && dy < 2.5f)
            : type == BotType.Sniper ? dx <= 7f && dy < 2.5f
            : delta.magnitude <= 8f;
        if (!alerted && sees) { alerted = true; alertTimer = .35f; }
        if (type == BotType.Sniper) {
            if (alerted) {
                aiming = true; dir = delta.x >= 0 ? 1 : -1; sr.flipX = dir < 0;
                fireTimer -= Time.deltaTime;
                if (laser != null) {
                    Vector3 from = transform.position + Vector3.up * .45f + Vector3.right * dir * .35f;
                    laser.SetPosition(0, from); laser.SetPosition(1, from + Vector3.right * dir * 12f); laser.enabled = true;
                    if (fireTimer <= 0f) { fireTimer = 1.8f; RaycastHit2D hit = Physics2D.Raycast(from, Vector2.right * dir, 12f); if (hit && hit.transform == player) { Health ph = player.GetComponent<Health>(); if (ph) ph.TakeDamage(new DamageInfo { amount = 1, knockback = Vector2.right * dir * 3f, sourceTeam = Team.Enemy, source = gameObject, hitPoint = hit.point }); } }
                }
            } else Patrol();
            return;
        }
        if (alerted) {
            alertTimer -= Time.deltaTime;
            if (alertTimer > 0) return; // recognition animation stand-in
            dir = player.position.x >= transform.position.x ? 1 : -1;
            transform.position += Vector3.right * dir * chargeSpeed * Time.deltaTime;
            sr.flipX = dir < 0;
        } else Patrol();
    }
    void Patrol() {
        if (transform.position.x <= patrolMinX) dir = 1;
        if (transform.position.x >= patrolMaxX) dir = -1;
        Vector2 origin = transform.position + Vector3.down * .45f;
        bool groundAhead = Physics2D.Raycast(origin + Vector2.right * dir * .45f, Vector2.down, 1.3f);
        bool wall = Physics2D.Raycast(transform.position, Vector2.right * dir, .55f);
        if (wall || (!groundAhead && type != BotType.Drone)) dir *= -1;
        if (type == BotType.Drone) { transform.position += Vector3.right * dir * patrolSpeed * Time.deltaTime; if (wall) dir *= -1; }
        else { transform.position += Vector3.right * dir * patrolSpeed * Time.deltaTime; }
        sr.flipX = dir < 0;
    }
    void OnDied(Health h) {
        if (dead) return; dead = true;
        if (laser) laser.enabled = false;
        GameObject reward = new GameObject("Gear Reward");
        reward.transform.position = transform.position;
        var srw = reward.AddComponent<SpriteRenderer>(); srw.sprite = RuntimeSprites.White; srw.color = new Color(1f,.72f,.12f); srw.sortingOrder = 3;
        reward.transform.localScale = new Vector3(.38f,.38f,1);
        var col = reward.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = .8f;
        reward.AddComponent<GearReward>();
        var body = reward.AddComponent<Rigidbody2D>(); body.gravityScale = 1.2f; body.AddForce(new Vector2(Random.Range(-3f,3f), Random.Range(3f,5f)), ForceMode2D.Impulse);
        Destroy(gameObject);
    }
    void OnDestroy() { if (health) health.Died -= OnDied; }
}
public class GearReward : MonoBehaviour {
    bool got;
    void OnTriggerEnter2D(Collider2D other) {
        if (got || !other.CompareTag("Player")) return;
        got = true;
        var sr = GetComponent<SpriteRenderer>(); if (sr) sr.color = Color.green;
        // Collection is intentionally physical; feedback is shown by a brief scale pop.
        transform.localScale *= 1.5f; Destroy(gameObject, .08f);
    }
}
