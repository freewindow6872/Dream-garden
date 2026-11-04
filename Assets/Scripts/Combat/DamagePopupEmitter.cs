using UnityEngine;

/// <summary>
/// 이 오브젝트가 피해를 입을 때마다 머리 위로 데미지 숫자를 띄운다.
/// 적/허수아비에 붙이면 끝. (Health의 Damaged 이벤트만 구독 → 공격 코드와 독립)
/// </summary>
[RequireComponent(typeof(Health))]
public class DamagePopupEmitter : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1f, 0.95f, 0.8f, 1f);
    [SerializeField] private Color criticalColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private float scale = 1f;
    [Tooltip("켜면 적중 지점(hitPoint) 대신 콜라이더 위쪽에서 숫자가 나온다")]
    [SerializeField] private bool spawnAboveCollider = false;

    private Health health;

    private void Awake() { health = GetComponent<Health>(); }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    private void OnDamaged(DamageInfo info)
    {
        Vector2 pos = info.hitPoint;
        Collider2D col = GetComponent<Collider2D>();

        if (spawnAboveCollider || pos == Vector2.zero)
            pos = col != null ? new Vector2(col.bounds.center.x, col.bounds.max.y) : (Vector2)transform.position;
        else
            pos += Vector2.up * 0.3f;

        DamagePopup.Spawn(pos, info.amount, info.isCritical ? criticalColor : color, info.isCritical ? scale * 1.3f : scale);
    }
}
