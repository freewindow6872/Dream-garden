using UnityEngine;

/// <summary>
/// 씬 로드 직후 전투 관련 컴포넌트/UI가 없으면 자동으로 붙여준다.
/// → 팀원들이 SampleScene(.unity)을 건드리지 않아도 돼서, 씬 파일 merge 충돌이 생기지 않는다.
///
/// 이펙트 에셋을 넣고 싶으면: 플레이어 오브젝트에 PlayerCombatEffects / PlayerCombatAnimator를 직접 추가해서
/// 인스펙터를 채우면 된다. 이미 붙어 있으면 이 스크립트는 건드리지 않는다.
/// </summary>
public static class CombatBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        PlayerCombat player = Object.FindObjectOfType<PlayerCombat>();
        if (player == null) return;

        GameObject go = player.gameObject;
        if (go.GetComponent<PlayerCombatEffects>() == null) go.AddComponent<PlayerCombatEffects>();
        if (go.GetComponent<PlayerCombatAnimator>() == null) go.AddComponent<PlayerCombatAnimator>();
        if (go.GetComponent<PlayerDeathHandler>() == null) go.AddComponent<PlayerDeathHandler>();

        if (Object.FindObjectOfType<HealthUI>() == null)
            new GameObject("HealthUI").AddComponent<HealthUI>();
    }
}
