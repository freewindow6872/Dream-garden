using UnityEngine;

/// <summary>
/// 전투용 레이어 충돌 규칙. 게임 시작 시 한 번 적용된다. (Project Settings > Physics 2D 의 충돌 매트릭스와 같은 효과)
///
/// 레이어 (Project Settings > Tags and Layers 에 등록됨)
///   6 Player / 7 Enemy / 8 EnemyProjectile
///
/// 규칙
///   - EnemyProjectile(적 총알) ↔ Enemy, EnemyProjectile : 충돌 안 함  (총알이 적/다른 총알에 걸리지 않음)
///   - Enemy ↔ Enemy                                        : 충돌 안 함  (적끼리 밀거나 막지 않음)
///   - 그 외(Player, Ground 등)는 기존 그대로
///
/// 레이어 이름이 프로젝트에 없으면 해당 규칙은 건너뛴다. (에러 없음)
/// 매트릭스를 에디터에서 직접 설정하고 싶으면 위 규칙대로 체크를 해제해도 되고, 그러면 이 파일은 지워도 된다.
/// </summary>
public static class CombatPhysicsSetup
{
    public const string PlayerLayerName = "Player";
    public const string EnemyLayerName = "Enemy";
    public const string ProjectileLayerName = "EnemyProjectile";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        int enemy = LayerMask.NameToLayer(EnemyLayerName);
        int projectile = LayerMask.NameToLayer(ProjectileLayerName);

        if (enemy >= 0) Physics2D.IgnoreLayerCollision(enemy, enemy, true);
        if (projectile >= 0)
        {
            Physics2D.IgnoreLayerCollision(projectile, projectile, true);
            if (enemy >= 0) Physics2D.IgnoreLayerCollision(projectile, enemy, true);
        }
    }
}
