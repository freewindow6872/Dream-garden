using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정원 가위(기본 무기) 근접 콤보 공격. (기획서 4.2)
///  - 공격 키를 연속 입력하면 콤보가 이어진다.
///  - 대시하면 공격이 취소된다.
///  - 식물 능력(PlantLoadout)은 이 공격과 별개로 함께 사용한다.
/// </summary>
[RequireComponent(typeof(PlayerController2D))]
[RequireComponent(typeof(Health))]
public class PlayerCombat : MonoBehaviour
{
    public WeaponData weapon;

    [SerializeField] private float inputBufferTime = 0.2f;
    [Tooltip("공격 중 이동속도 배율: 0.675 = 평소 속도의 67.5% (기존 35%에서 감속 페널티를 절반 완화)")]
    [SerializeField, Range(0f, 1f)] private float moveMultiplierWhileAttacking = 0.675f;
    [SerializeField] private float hitStopTime = 0.045f;
    [Header("딜레이 튜닝 (WeaponData 값에 곱해짐)")]
    [Tooltip("선딜레이(공격 키 → 판정 발생까지) 배율. 1 = 그대로, 2 = 2배 느림")]
    [SerializeField, Min(0f)] private float startupScale = 1f;
    [Tooltip("후딜레이(판정 끝 → 다음 행동 가능까지) 배율")]
    [SerializeField, Min(0f)] private float recoveryScale = 1f;

    [Header("Debug")]
    [Tooltip("공격 판정 범위를 반투명 박스로 보여줌 (그래픽 들어오기 전 임시 이펙트)")]
    [SerializeField] private bool showSlashDebug = true;

    /// <summary>공격 진행 단계. 애니메이션/이펙트 스크립트가 구독해서 쓴다.</summary>
    public enum AttackPhase { Idle, Startup, Active, Recovery }

    private PlayerController2D controller;
    private Health health;

    private AttackPhase phase = AttackPhase.Idle;
    private float phaseTimer;
    private int comboIndex = -1;
    private float comboResetTimer;
    private float bufferTimer;
    private int attackDir = 1;
    private readonly HashSet<Health> hitSet = new HashSet<Health>();
    private SpriteRenderer slash;

    public bool IsAttacking { get { return phase != AttackPhase.Idle; } }
    public int ComboIndex { get { return comboIndex; } }
    public AttackPhase CurrentPhase { get { return phase; } }
    /// <summary>현재 공격이 바라보는 방향 (+1 오른쪽 / -1 왼쪽)</summary>
    public int AttackDir { get { return attackDir; } }

    /// <summary>현재 타의 총 길이(선딜+판정+후딜, 배율 적용). 애니메이션 속도 맞출 때 사용</summary>
    public float CurrentStepDuration
    {
        get
        {
            if (weapon == null || comboIndex < 0 || comboIndex >= weapon.combo.Length) return 0f;
            ComboStep s = weapon.combo[comboIndex];
            return s.startup * startupScale + s.active + s.recovery * recoveryScale;
        }
    }

    /// <summary>단계가 바뀔 때 (새 단계, 콤보 인덱스). Idle로 돌아올 때도 호출됨</summary>
    public event System.Action<AttackPhase, int> PhaseChanged;

    /// <summary>공격 시작 (콤보 인덱스) - 애니메이션/사운드 연결용</summary>
    public event System.Action<int> AttackStarted;
    /// <summary>적중 - 화면 흔들림/이펙트 연결용</summary>
    public event System.Action<Health, DamageInfo> HitLanded;

    private void SetPhase(AttackPhase next)
    {
        if (phase == next) return;
        phase = next;
        if (PhaseChanged != null) PhaseChanged(next, comboIndex);
    }

    private void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        health = GetComponent<Health>();

        // 플레이어의 Health는 반드시 Player 팀 (체력 5칸, 피격 후 무적 0.8초 기본값)
        if (health.Team != Team.Player)
            health.Configure(Team.Player, 5, 0.8f);

        EnsureWeapon();
    }

    private void OnEnable()
    {
        if (controller == null) controller = GetComponent<PlayerController2D>();
        if (health == null) health = GetComponent<Health>();
        controller.DashStarted += CancelAttack;
        health.Damaged += OnHurt;
    }

    private void OnDisable()
    {
        if (controller != null) controller.DashStarted -= CancelAttack;
        if (health != null) health.Damaged -= OnHurt;
        CancelAttack();
    }

    /// <summary>피격 시 진행 중이던 공격은 끊긴다 (경직)</summary>
    private void OnHurt(DamageInfo info)
    {
        CancelAttack();
    }

    public void SetWeapon(WeaponData newWeapon)
    {
        weapon = newWeapon;
        EnsureWeapon();
        CancelAttack();
    }

    private void EnsureWeapon()
    {
        if (weapon == null || weapon.combo == null || weapon.combo.Length == 0)
            weapon = WeaponData.CreateRuntimeDefault();
    }

    private void Update()
    {
        if (health.IsDead || controller.InputLocked)
        {
            if (phase != AttackPhase.Idle) CancelAttack();
            return;
        }

        if (GameInput.AttackDown) bufferTimer = inputBufferTime;
        else if (bufferTimer > 0f) bufferTimer -= Time.deltaTime;

        float dt = Time.deltaTime;

        switch (phase)
        {
            case AttackPhase.Idle:
                if (comboResetTimer > 0f)
                {
                    comboResetTimer -= dt;
                    if (comboResetTimer <= 0f) comboIndex = -1;
                }
                if (bufferTimer > 0f && controller.CanAct)
                    StartStep(comboIndex < 0 ? 0 : (comboIndex + 1) % weapon.combo.Length);
                break;

            case AttackPhase.Startup:
                phaseTimer -= dt;
                if (phaseTimer <= 0f) EnterActive();
                break;

            case AttackPhase.Active:
                phaseTimer -= dt;
                DoHitCheck();
                if (phaseTimer <= 0f) EnterRecovery();
                break;

            case AttackPhase.Recovery:
                phaseTimer -= dt;
                if (phaseTimer <= 0f) FinishRecovery();
                break;
        }
    }

    // ---------------------------------------------------------------- phases

    private void StartStep(int index)
    {
        comboIndex = index;
        ComboStep step = weapon.combo[index];

        SetPhase(AttackPhase.Startup);
        phaseTimer = step.startup * startupScale;
        bufferTimer = 0f;
        hitSet.Clear();

        attackDir = controller.FacingDir;
        controller.FacingLocked = true;
        controller.MoveSpeedMultiplier = moveMultiplierWhileAttacking;

        if (AttackStarted != null) AttackStarted(index);
    }

    private void EnterActive()
    {
        ComboStep step = weapon.combo[comboIndex];
        SetPhase(AttackPhase.Active);
        phaseTimer = step.active;
        controller.AddHorizontalImpulse(attackDir * step.lunge);
        DoHitCheck();
    }

    private void EnterRecovery()
    {
        SetPhase(AttackPhase.Recovery);
        phaseTimer = weapon.combo[comboIndex].recovery * recoveryScale;
        HideSlash();
    }

    private void FinishRecovery()
    {
        SetPhase(AttackPhase.Idle);
        controller.FacingLocked = false;
        controller.MoveSpeedMultiplier = 1f;

        float window = weapon.combo[comboIndex].comboWindow;
        if (window > 0f) comboResetTimer = window;
        else comboIndex = -1;
    }

    public void CancelAttack()
    {
        comboIndex = -1;
        comboResetTimer = 0f;
        SetPhase(AttackPhase.Idle);
        HideSlash();

        if (controller != null)
        {
            controller.FacingLocked = false;
            controller.MoveSpeedMultiplier = 1f;
        }
    }

    // ---------------------------------------------------------------- hit

    private Vector2 HitCenter(ComboStep step)
    {
        return (Vector2)transform.position + new Vector2(step.hitboxOffset.x * attackDir, step.hitboxOffset.y);
    }

    private void DoHitCheck()
    {
        ComboStep step = weapon.combo[comboIndex];
        Vector2 center = HitCenter(step);
        ShowSlash(center, step.hitboxSize);

        Collider2D[] cols = Physics2D.OverlapBoxAll(center, step.hitboxSize, 0f);
        bool landed = false;

        for (int i = 0; i < cols.Length; i++)
        {
            Health target = cols[i].GetComponentInParent<Health>();
            if (target == null || target == health || target.IsDead) continue;
            if (target.Team == Team.Player) continue;
            if (!hitSet.Add(target)) continue;

            DamageInfo info = new DamageInfo();
            info.amount = weapon.RollDamage(step.damageMultiplier);
            info.knockback = new Vector2(step.knockback.x * attackDir, step.knockback.y);
            info.sourceTeam = Team.Player;
            info.source = gameObject;
            info.hitPoint = cols[i].ClosestPoint(center);

            if (target.TakeDamage(info))
            {
                landed = true;
                if (HitLanded != null) HitLanded(target, info);
            }
        }

        if (landed) HitStop.Do(hitStopTime);
    }

    // ---------------------------------------------------------------- debug slash

    private void ShowSlash(Vector2 center, Vector2 size)
    {
        if (!showSlashDebug) return;

        if (slash == null)
        {
            GameObject go = new GameObject("SlashDebug");
            slash = go.AddComponent<SpriteRenderer>();
            slash.sprite = RuntimeSprites.White;
            slash.color = new Color(1f, 1f, 1f, 0.35f);
            slash.sortingOrder = 5;
        }

        slash.enabled = true;
        slash.transform.position = new Vector3(center.x, center.y, 0f);
        slash.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    private void HideSlash()
    {
        if (slash != null) slash.enabled = false;
    }

    private void OnDestroy()
    {
        if (slash != null) Destroy(slash.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        WeaponData w = weapon;
        if (w == null || w.combo == null || w.combo.Length == 0) return;

        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        int dir = Application.isPlaying ? attackDir : 1;
        for (int i = 0; i < w.combo.Length; i++)
        {
            ComboStep s = w.combo[i];
            Vector2 c = (Vector2)transform.position + new Vector2(s.hitboxOffset.x * dir, s.hitboxOffset.y);
            Gizmos.DrawWireCube(c, s.hitboxSize);
        }
    }
}
