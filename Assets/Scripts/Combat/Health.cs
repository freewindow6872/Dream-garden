using UnityEngine;

/// <summary>
/// 체력 컴포넌트. (기획서 2.3)
///  - 플레이어: "체력 칸" (예: 5칸, 피격 1회 = 1칸). 스테이지 중 자유 회복 불가.
///  - 적/보스: 체력바 (EnemyHealthBar)
/// </summary>
public class Health : MonoBehaviour
{
    [SerializeField] private Team team = Team.Enemy;
    [SerializeField] private int maxHealth = 10;
    [Tooltip("피격 후 무적 시간(초). 플레이어는 0.8 정도, 적은 0")]
    [SerializeField] private float hitInvulnerableTime = 0f;

    private float hitInvulnerableUntil;
    private int forcedInvulnerableCount;

    public Team Team { get { return team; } }
    public int Max { get { return maxHealth; } }
    public int Current { get; private set; }
    public bool IsDead { get { return Current <= 0; } }
    public bool IsHitInvulnerable { get { return Time.time < hitInvulnerableUntil; } }
    public bool IsInvulnerable { get { return IsHitInvulnerable || forcedInvulnerableCount > 0; } }

    /// <summary>피해를 실제로 입었을 때</summary>
    public event System.Action<DamageInfo> Damaged;
    /// <summary>(현재, 최대)</summary>
    public event System.Action<int, int> Changed;
    public event System.Action<Health> Died;

    private void Awake()
    {
        Current = maxHealth;
    }

    /// <summary>코드로 생성하는 오브젝트/에디터 셋업용</summary>
    public void Configure(Team newTeam, int max, float invulnerableTime)
    {
        team = newTeam;
        maxHealth = Mathf.Max(1, max);
        hitInvulnerableTime = invulnerableTime;
        Current = maxHealth;
        if (Changed != null) Changed(Current, maxHealth);
    }

    /// <summary>피해를 입혔으면 true (무적/사망 상태면 false)</summary>
    public bool TakeDamage(DamageInfo info)
    {
        if (IsDead || IsInvulnerable || info.amount <= 0) return false;

        Current = Mathf.Max(0, Current - info.amount);
        if (hitInvulnerableTime > 0f) hitInvulnerableUntil = Time.time + hitInvulnerableTime;

        if (Damaged != null) Damaged(info);
        if (Changed != null) Changed(Current, maxHealth);
        if (Current <= 0 && Died != null) Died(this);
        return true;
    }

    /// <summary>스테이지 내 회복은 기본 불가. 향후 식물/스킬 효과용 API.</summary>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        if (Changed != null) Changed(Current, maxHealth);
    }

    public void ResetToMax()
    {
        Current = maxHealth;
        if (Changed != null) Changed(Current, maxHealth);
    }

    /// <summary>대시 무적 등 "켜고 끄는" 무적</summary>
    public void SetForcedInvulnerable(bool on)
    {
        forcedInvulnerableCount = Mathf.Max(0, forcedInvulnerableCount + (on ? 1 : -1));
    }
}
