using UnityEngine;

/// <summary>
/// 적이 쏘는 직선 총알. 플레이어 Health에만 피해를 준다.
///  - 플레이어에게 맞으면 피해 + 소멸 (무적/대시 무적 중이면 TakeDamage가 false → 그대로 통과)
///  - 바닥/벽(Trigger가 아닌 콜라이더)에 닿으면 소멸
///  - 다른 적(Health 있는 오브젝트)은 무시
///  - 수명이 다하면 자동 삭제
/// 직접 만들 필요 없이 EnemyBullet.Spawn()으로 생성한다. (에셋 불필요: 단색 사각형 + 코드 콜라이더)
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    private int damage = 1;
    private Vector2 knockback = new Vector2(5f, 3f);
    private GameObject owner;

    public static EnemyBullet Spawn(Vector2 position, Vector2 direction, float speed, int damage,
                                    Vector2 knockback, float lifetime, GameObject owner)
    {
        direction = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;

        GameObject go = new GameObject("EnemyBullet");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        // "EnemyProjectile" 레이어(프로젝트 설정에 등록됨)가 있으면 사용. 없으면 Default
        int layer = LayerMask.NameToLayer(CombatPhysicsSetup.ProjectileLayerName);
        if (layer >= 0) go.layer = layer;

        // 모양 (자식에 두어서 콜라이더가 찌그러지지 않게 함)
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = new Vector3(0.42f, 0.18f, 1f);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprites.White;
        sr.color = new Color(1f, 0.55f, 0.15f, 1f);
        sr.sortingOrder = 8;

        // 물리: 키네마틱 + 트리거 (플레이어 리지드바디와 트리거 이벤트가 발생)
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.velocity = direction * speed;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.13f;

        EnemyBullet bullet = go.AddComponent<EnemyBullet>();
        bullet.damage = damage;
        bullet.knockback = knockback;
        bullet.owner = owner;

        Destroy(go, Mathf.Max(0.1f, lifetime));
        return bullet;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Health target = other.GetComponentInParent<Health>();

        if (target != null)
        {
            if (target.Team != Team.Player || target.IsDead) return;   // 다른 적/발사한 본인은 통과

            Vector2 vel = GetComponent<Rigidbody2D>().velocity;
            float dirX = Mathf.Abs(vel.x) > 0.01f ? Mathf.Sign(vel.x) : Mathf.Sign(target.transform.position.x - transform.position.x);

            DamageInfo info = new DamageInfo();
            info.amount = damage;
            info.knockback = new Vector2(knockback.x * dirX, knockback.y);
            info.sourceTeam = Team.Enemy;
            info.source = owner != null ? owner : gameObject;
            info.hitPoint = transform.position;

            // 무적 중이면 false → 총알은 계속 날아감 (대시로 회피 가능)
            if (target.TakeDamage(info)) Impact();
            return;
        }

        // Health가 없는 일반 콜라이더: 바닥/벽이면 소멸 (트리거는 무시)
        if (!other.isTrigger) Impact();
    }

    private void Impact()
    {
        CombatEffect.SpawnDebugBurst(transform.position, new Color(1f, 0.6f, 0.2f, 1f), 0.45f);
        Destroy(gameObject);
    }
}
