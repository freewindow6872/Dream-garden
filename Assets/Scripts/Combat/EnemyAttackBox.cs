using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적의 공격 판정 (사각형). 플레이어 Health에만 피해를 준다.
///
/// 사용법
///  - 적 AI 코드에서 공격 모션의 "때리는 순간"에 `attackBox.Strike();` 를 호출 (판정이 defaultActiveTime 동안 유지)
///  - 몸통 박치기형 적은 contactDamage를 켜면 항상 판정이 켜져 있다 (연타는 플레이어 무적시간이 막아줌)
///  - 방향: 기본은 transform.lossyScale.x 부호. 다른 방식이면 SetFacing(+1/-1)을 매 프레임/방향 전환 때 호출
///  - 판정 위치/크기는 "오른쪽을 볼 때" 기준이며 왼쪽이면 x가 자동 반전
///
/// 피해 흐름: 판정 발생 → Health.TakeDamage (무적/사망이면 자동 무시) → 체력 칸 감소 + Damaged 이벤트(넉백/경직/UI)
/// </summary>
public class EnemyAttackBox : MonoBehaviour
{
    [Header("피해")]
    [Tooltip("플레이어 체력 '칸' 단위. 기본 1칸")]
    [SerializeField] private int damage = 1;
    [Tooltip("오른쪽 기준 넉백 (x는 공격자→플레이어 방향으로 자동 적용). 약한 넉백 권장")]
    [SerializeField] private Vector2 knockback = new Vector2(5f, 3f);

    [Header("판정 사각형 (오른쪽을 볼 때 기준)")]
    [SerializeField] private Vector2 size = new Vector2(1.2f, 1f);
    [SerializeField] private Vector2 offset = new Vector2(0.8f, 0f);

    [Header("동작")]
    [Tooltip("Strike() 호출 시 판정이 유지되는 시간(초)")]
    [SerializeField] private float defaultActiveTime = 0.12f;
    [Tooltip("켜면 항상 판정이 활성 (몸통 박치기형)")]
    [SerializeField] private bool contactDamage = false;
    [Tooltip("0보다 크면 이 간격(초)마다 자동으로 Strike. AI 없이 피격/체력 UI를 테스트할 때 사용")]
    [SerializeField] private float testStrikeInterval = 0f;

    private Health ownerHealth;
    private readonly HashSet<Health> hitSet = new HashSet<Health>();
    private float activeTimer;
    private float testTimer;
    private int facing = 1;
    private bool facingOverridden;

    public bool IsActive { get { return contactDamage || activeTimer > 0f; } }
    public int FacingDir
    {
        get { return facingOverridden ? facing : (transform.lossyScale.x < 0f ? -1 : 1); }
    }

    /// <summary>플레이어에게 피해를 입혔을 때</summary>
    public event System.Action<Health, DamageInfo> HitPlayer;

    private void Awake()
    {
        ownerHealth = GetComponentInParent<Health>();
        testTimer = testStrikeInterval;
    }

    public void SetFacing(int dir)
    {
        facing = dir < 0 ? -1 : 1;
        facingOverridden = true;
    }

    public void Strike() { Strike(defaultActiveTime); }

    public void Strike(float activeTime)
    {
        hitSet.Clear();
        activeTimer = Mathf.Max(activeTimer, activeTime);
    }

    public void Cancel()
    {
        activeTimer = 0f;
        contactDamage = false;
        testStrikeInterval = 0f;
    }

    private void Update()
    {
        if (ownerHealth != null && ownerHealth.IsDead) return;

        if (testStrikeInterval > 0f && !IsActive)
        {
            testTimer -= Time.deltaTime;
            if (testTimer <= 0f)
            {
                Strike();
                testTimer = testStrikeInterval;
            }
        }

        if (!IsActive) return;

        CheckHit();
        if (!contactDamage) activeTimer -= Time.deltaTime;
    }

    private Vector2 Center()
    {
        return (Vector2)transform.position + new Vector2(offset.x * FacingDir, offset.y);
    }

    private void CheckHit()
    {
        Vector2 center = Center();
        Collider2D[] cols = Physics2D.OverlapBoxAll(center, size, 0f);

        for (int i = 0; i < cols.Length; i++)
        {
            Health target = cols[i].GetComponentInParent<Health>();
            if (target == null || target.Team != Team.Player || target.IsDead) continue;
            if (hitSet.Contains(target)) continue;

            float dirX = Mathf.Sign(target.transform.position.x - transform.position.x);
            if (Mathf.Approximately(dirX, 0f)) dirX = FacingDir;

            DamageInfo info = new DamageInfo();
            info.amount = damage;
            info.knockback = new Vector2(knockback.x * dirX, knockback.y);
            info.sourceTeam = Team.Enemy;
            info.source = gameObject;
            info.hitPoint = cols[i].ClosestPoint(center);

            // 무적/사망이면 TakeDamage가 false → 맞지 않은 것으로 치고 판정 시간 안에서 재시도
            if (target.TakeDamage(info))
            {
                hitSet.Add(target);
                if (HitPlayer != null) HitPlayer(target, info);
            }
        }
    }

    private void OnDrawGizmos()
    {
        int dir = Application.isPlaying ? FacingDir : (transform.lossyScale.x < 0f ? -1 : 1);
        Vector2 c = (Vector2)transform.position + new Vector2(offset.x * dir, offset.y);
        Gizmos.color = IsActive && Application.isPlaying ? new Color(1f, 0f, 0f, 0.9f) : new Color(1f, 0.5f, 0.1f, 0.5f);
        Gizmos.DrawWireCube(c, size);
    }
}
