using UnityEngine;

/// <summary>
/// 총알을 쏘는 테스트용 봇. AI 없음 — 제자리에서 일정 간격으로 같은 방향(또는 플레이어 쪽)으로 쏘기만 한다.
///  - 플레이어 피격 연출 / 체력 UI 테스트용
///  - Health가 있어서 플레이어가 때릴 수 있고(데미지 숫자 + 체력바), 죽으면 EnemyDeath가 처리
///  - 피격 경직(HitReaction.IsStunned) 중에는 발사 타이머가 멈춘다
///
/// 진짜 적 AI가 생기면 이 스크립트는 지우거나, Fire()만 가져다 써도 된다.
/// </summary>
[RequireComponent(typeof(Health))]
public class ShooterBot : MonoBehaviour
{
    [Header("발사")]
    [SerializeField] private float fireInterval = 1.5f;
    [Tooltip("게임 시작 후 첫 발까지 대기(초)")]
    [SerializeField] private float firstShotDelay = 1f;
    [SerializeField] private float bulletSpeed = 8f;
    [SerializeField] private float bulletLifetime = 3f;
    [Tooltip("플레이어 체력 '칸' 단위")]
    [SerializeField] private int damage = 1;
    [SerializeField] private Vector2 knockback = new Vector2(5f, 3f);

    [Header("방향")]
    [Tooltip("그림이 오른쪽을 보는 기준. 체크 해제하면 왼쪽을 보고 쏜다 (스프라이트 좌우 반전)")]
    [SerializeField] private bool facingRight = false;
    [Tooltip("켜면 발사 순간의 플레이어 위치를 향해 쏜다 (이동/판단 없이 한 번 조준만 함). 끄면 바라보는 방향으로 직선 발사")]
    [SerializeField] private bool aimAtPlayer = true;
    [Tooltip("오른쪽을 볼 때 총구 위치 (왼쪽이면 x 자동 반전)")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0.65f, 0.4f);

    private Health health;
    private HitReaction hitReaction;
    private SpriteRenderer sprite;
    private Transform playerTarget;
    private float timer;

    public int FacingDir { get { return facingRight ? 1 : -1; } }

    private void Awake()
    {
        health = GetComponent<Health>();
        hitReaction = GetComponent<HitReaction>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        timer = firstShotDelay;
        ApplyFacing();
    }

    private void Update()
    {
        if (health.IsDead) return;
        if (hitReaction != null && hitReaction.IsStunned) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Fire();
            timer = fireInterval;
        }
    }

    public void SetFacing(bool right)
    {
        facingRight = right;
        ApplyFacing();
    }

    private void ApplyFacing()
    {
        if (sprite != null) sprite.flipX = !facingRight;
    }

    /// <summary>총알 1발 발사</summary>
    public void Fire()
    {
        Vector2 muzzle = MuzzlePosition();
        Vector2 dir = new Vector2(FacingDir, 0f);

        if (aimAtPlayer)
        {
            if (playerTarget == null)
            {
                PlayerCombat p = FindObjectOfType<PlayerCombat>();
                if (p != null) playerTarget = p.transform;
            }
            if (playerTarget != null)
            {
                dir = (Vector2)playerTarget.position - muzzle;
                // 조준 방향에 맞춰 바라보는 방향도 갱신
                if (Mathf.Abs(dir.x) > 0.05f) { facingRight = dir.x > 0f; ApplyFacing(); muzzle = MuzzlePosition(); dir = (Vector2)playerTarget.position - muzzle; }
            }
        }

        EnemyBullet.Spawn(muzzle, dir, bulletSpeed, damage, knockback, bulletLifetime, gameObject);
        CombatEffect.SpawnDebugBurst(muzzle, new Color(1f, 0.9f, 0.5f, 1f), 0.35f);   // 총구 섬광
    }

    private Vector2 MuzzlePosition()
    {
        return (Vector2)transform.position + new Vector2(muzzleOffset.x * FacingDir, muzzleOffset.y);
    }

    private void OnDrawGizmosSelected()
    {
        int dir = Application.isPlaying ? FacingDir : (facingRight ? 1 : -1);
        Vector2 m = (Vector2)transform.position + new Vector2(muzzleOffset.x * dir, muzzleOffset.y);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(m, 0.08f);
        Gizmos.DrawLine(m, m + new Vector2(dir * 3f, 0f));
    }
}
