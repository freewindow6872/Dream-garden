using UnityEngine;

/// <summary>단색 사각형 보상. 플레이어가 실제로 접촉했을 때 골드를 추가한다.</summary>
[RequireComponent(typeof(Collider2D))]
public class GearReward : MonoBehaviour
{
    [SerializeField, Min(0)] private int amount = 1;
    private bool collected;

    public static GearReward Spawn(Vector2 position, int value)
    {
        GameObject go = new GameObject("Gear Reward");
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.38f, 0.38f, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprites.White;
        sr.color = new Color(1f, 0.72f, 0.12f, 1f);
        sr.sortingOrder = 8;
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = false;
        col.radius = 0.7f;
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1.2f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.AddForce(new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 5f)), ForceMode2D.Impulse);
        GearReward reward = go.AddComponent<GearReward>();
        reward.amount = Mathf.Max(0, value);
        return reward;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collected) return;
        Health health = collision.collider.GetComponentInParent<Health>();
        if (health == null || health.Team != Team.Player) return;
        collected = true;
        GoldCounter.Add(amount);
        Destroy(gameObject);
    }
}
