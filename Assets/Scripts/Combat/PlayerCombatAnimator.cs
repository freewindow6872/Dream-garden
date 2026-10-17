using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 "전투" 애니메이션 담당. (이동 애니메이션은 PlayerAnimator2D가 따로 담당)
///
/// 충돌 방지 규칙
///  - 이 스크립트는 아래 파라미터만 만진다. 이동 쪽(Speed / VerticalSpeed / Grounded)은 절대 건드리지 않음.
///  - 좌우 반전은 PlayerController2D가 spriteRenderer.flipX로 처리한다. 애니메이션 클립에서 flipX를 키잡지 말 것.
///  - Animator 컨트롤러에 "있는 파라미터만" 사용하므로, 애니메이션이 아직 없어도 에러 없이 무시된다.
///
/// Animator 파라미터 (이름은 인스펙터에서 변경 가능)
///   Attack(Trigger)      공격 시작
///   ComboIndex(Int)      몇 번째 타인지 (0,1,2)
///   Attacking(Bool)      공격 중(선딜~후딜)
///   AttackSpeed(Float)   애니메이션 재생 배율 = 기준 클립 길이 / 실제 한 타 길이
///                        → 선/후딜 값을 바꿔도 모션 길이가 자동으로 맞춰짐 (State의 Speed Multiplier에 이 파라미터 연결)
///   Hurt(Trigger)        피격
///   Die(Trigger) / Dead(Bool)   사망
/// </summary>
[RequireComponent(typeof(PlayerCombat))]
public class PlayerCombatAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Animator 파라미터 이름 (비워두면 사용 안 함)")]
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string comboIndexInt = "ComboIndex";
    [SerializeField] private string attackingBool = "Attacking";
    [SerializeField] private string attackSpeedFloat = "AttackSpeed";
    [SerializeField] private string hurtTrigger = "Hurt";
    [SerializeField] private string dieTrigger = "Die";
    [SerializeField] private string deadBool = "Dead";

    [Header("공격 애니메이션 속도 맞춤")]
    [Tooltip("공격 클립 원본 길이(초). 한 타의 선딜+판정+후딜 합이 이 값과 다르면 AttackSpeed로 배속 보정")]
    [SerializeField] private float referenceClipLength = 0.26f;

    private PlayerCombat combat;
    private Health health;
    private readonly HashSet<string> paramNames = new HashSet<string>();

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<Health>();
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheParameters();
    }

    private void OnEnable()
    {
        if (combat == null) combat = GetComponent<PlayerCombat>();
        if (health == null) health = GetComponent<Health>();

        combat.AttackStarted += OnAttackStarted;
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (combat != null) combat.AttackStarted -= OnAttackStarted;
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
    }

    private void Update()
    {
        SetBool(attackingBool, combat.IsAttacking);
    }

    // ---------------------------------------------------------------- events

    private void OnAttackStarted(int comboIndex)
    {
        SetInt(comboIndexInt, comboIndex);

        float duration = combat.CurrentStepDuration;
        float speed = duration > 0.001f ? referenceClipLength / duration : 1f;
        SetFloat(attackSpeedFloat, speed);

        SetTrigger(attackTrigger);
    }

    private void OnDamaged(DamageInfo info)
    {
        SetTrigger(hurtTrigger);
    }

    private void OnDied(Health h)
    {
        SetBool(deadBool, true);
        SetTrigger(dieTrigger);
    }

    // ---------------------------------------------------------------- safe setters

    private void CacheParameters()
    {
        paramNames.Clear();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        AnimatorControllerParameter[] ps = animator.parameters;
        for (int i = 0; i < ps.Length; i++) paramNames.Add(ps[i].name);
    }

    private bool Has(string name)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return false;
        if (paramNames.Count == 0) CacheParameters();   // 컨트롤러가 나중에 연결된 경우
        return paramNames.Contains(name);
    }

    private void SetTrigger(string name) { if (Has(name)) animator.SetTrigger(name); }
    private void SetBool(string name, bool v) { if (Has(name)) animator.SetBool(name, v); }
    private void SetInt(string name, int v) { if (Has(name)) animator.SetInteger(name, v); }
    private void SetFloat(string name, float v) { if (Has(name)) animator.SetFloat(name, v); }
}
