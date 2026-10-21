using UnityEngine;

/// <summary>
/// 플레이어 공격 이펙트 / 적중 이펙트 담당. (움직임·애니메이션 코드와 완전히 분리)
/// PlayerCombat의 이벤트만 구독하므로 이 스크립트를 빼도 전투는 그대로 동작한다.
///
/// 에셋을 넣는 법: 플레이어 오브젝트에 이 컴포넌트를 직접 추가하고 인스펙터에 프리팹/프레임을 넣는다.
/// (직접 추가하지 않으면 CombatBootstrap이 빈 상태로 자동 추가해서, 임시 이펙트만 나온다.)
/// </summary>
[RequireComponent(typeof(PlayerCombat))]
public class PlayerCombatEffects : MonoBehaviour
{
    public enum AttackEffectTiming { Startup, Active }

    [Header("공격 이펙트 (공격할 때 나오는 것)")]
    [Tooltip("콤보 1타, 2타, 3타 순서. 타 수보다 적으면 마지막 것을 재사용")]
    [SerializeField] private EffectSpec[] attackEffects = new EffectSpec[0];
    [Tooltip("Startup: 키 누르자마자 / Active: 실제 판정이 나가는 순간")]
    [SerializeField] private AttackEffectTiming attackEffectTiming = AttackEffectTiming.Active;

    [Header("적중 이펙트 (맞췄을 때 추가로 나오는 것)")]
    [SerializeField] private EffectSpec hitEffect = new EffectSpec();
    [Tooltip("적중 이펙트 에셋이 비어 있으면 임시 사각형 이펙트를 띄움")]
    [SerializeField] private bool debugHitEffectWhenEmpty = true;

    private PlayerCombat combat;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
    }

    private void OnEnable()
    {
        if (combat == null) combat = GetComponent<PlayerCombat>();
        combat.PhaseChanged += OnPhaseChanged;
        combat.HitLanded += OnHitLanded;
    }

    private void OnDisable()
    {
        if (combat == null) return;
        combat.PhaseChanged -= OnPhaseChanged;
        combat.HitLanded -= OnHitLanded;
    }

    private void OnPhaseChanged(PlayerCombat.AttackPhase phase, int comboIndex)
    {
        if (comboIndex < 0 || attackEffects == null || attackEffects.Length == 0) return;

        PlayerCombat.AttackPhase wanted = attackEffectTiming == AttackEffectTiming.Startup
            ? PlayerCombat.AttackPhase.Startup
            : PlayerCombat.AttackPhase.Active;
        if (phase != wanted) return;

        EffectSpec spec = attackEffects[Mathf.Min(comboIndex, attackEffects.Length - 1)];
        CombatEffect.Spawn(spec, transform.position, combat.AttackDir, transform);
    }

    private void OnHitLanded(Health target, DamageInfo info)
    {
        if (hitEffect != null && !hitEffect.IsEmpty)
            CombatEffect.Spawn(hitEffect, info.hitPoint, combat.AttackDir, null);
        else if (debugHitEffectWhenEmpty)
            CombatEffect.SpawnDebugBurst(info.hitPoint, new Color(1f, 0.9f, 0.4f, 1f), 0.7f);
    }
}
