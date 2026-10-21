using System.Collections;
using UnityEngine;

/// <summary>
/// 적(로봇 등) 피격 반응: 짧은 정지 → 약한 넉백, 그리고 색상 변화.
/// Health의 Damaged 이벤트만 구독하므로 공격 코드와 서로 의존하지 않는다.
///
/// 적 AI 담당자: 이동/공격 코드에서 `if (hitReaction.IsStunned) return;` 로 경직 중 행동을 막으면 된다.
/// (시간은 realtime 기준이라 히트스톱(timeScale 낮춤) 중에도 길이가 일정하다. 일시정지 기능을 만들면 같이 손볼 것)
/// </summary>
[RequireComponent(typeof(Health))]
public class HitReaction : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sprite;

    [Header("짧은 정지 / 경직")]
    [Tooltip("맞은 순간 완전히 멈추는 시간(초)")]
    [SerializeField] private float freezeTime = 0.07f;
    [Tooltip("정지가 끝나고 넉백이 시작된 뒤 추가로 행동 못 하는 시간(초)")]
    [SerializeField] private float staggerTime = 0.15f;

    [Header("넉백")]
    [SerializeField] private bool applyKnockback = true;
    [SerializeField, Range(0f, 2f)] private float knockbackScale = 1f;

    [Header("색상 변화")]
    [SerializeField] private Color flashColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private float flashTime = 0.15f;

    private Health health;
    private Rigidbody2D rb;
    private Color baseColor = Color.white;
    private float savedGravity;
    private bool gravityFrozen;
    private Coroutine stunRoutine;
    private Coroutine flashRoutine;

    /// <summary>정지 + 경직 중이면 true. AI가 확인해서 행동을 멈추면 된다.</summary>
    public bool IsStunned { get; private set; }

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) baseColor = sprite.color;
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
        ResetState();
    }

    private void OnDamaged(DamageInfo info)
    {
        if (!isActiveAndEnabled) return;

        if (stunRoutine != null) StopCoroutine(stunRoutine);
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        stunRoutine = StartCoroutine(StunRoutine(info));
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator StunRoutine(DamageInfo info)
    {
        IsStunned = true;

        // 1) 짧은 정지
        if (rb != null)
        {
            if (!gravityFrozen)
            {
                savedGravity = rb.gravityScale;
                gravityFrozen = true;
            }
            rb.velocity = Vector2.zero;
            rb.gravityScale = 0f;
        }
        if (freezeTime > 0f) yield return new WaitForSecondsRealtime(freezeTime);

        // 2) 약한 넉백
        RestoreGravity();
        if (rb != null && applyKnockback)
            rb.velocity = info.knockback * knockbackScale;

        // 3) 경직 유지
        if (staggerTime > 0f) yield return new WaitForSecondsRealtime(staggerTime);

        IsStunned = false;
        stunRoutine = null;
    }

    private IEnumerator FlashRoutine()
    {
        if (sprite != null) sprite.color = flashColor;
        yield return new WaitForSecondsRealtime(flashTime);
        if (sprite != null) sprite.color = baseColor;
        flashRoutine = null;
    }

    private void RestoreGravity()
    {
        if (rb != null && gravityFrozen) rb.gravityScale = savedGravity;
        gravityFrozen = false;
    }

    private void ResetState()
    {
        RestoreGravity();
        if (sprite != null) sprite.color = baseColor;
        IsStunned = false;
        stunRoutine = null;
        flashRoutine = null;
    }
}
