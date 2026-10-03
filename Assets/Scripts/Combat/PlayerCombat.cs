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
    [SerializeField, Range(0f, 1f)] private float moveMultiplierWhileAttacking = 0.35f;
    [SerializeField] private float hitStopTime = 0.045f;
    [Tooltip("공격 판정 범위를 반투명 박스로 보여줌 (그래픽 들어오기 전 임시 이펙트)")]
    [SerializeField] private bool showSlashDebug = true;

    private enum Phase { Idle, Startup, Active, Recovery }

    private PlayerController2D controller;
    private Health health;

    private Phase phase = Phase.Idle;
    private float phaseTimer;
    private int comboIndex = -1;
    private float comboResetTimer;
    private float bufferTimer;
    private int attackDir = 1;
    private readonly HashSet<Health> hitSet = new HashSet<Health>();
    private SpriteRenderer slash;

    public bool IsAttacking { get { return phase != Phase.Idle; } }
    public int ComboIndex { get { return comboIndex; } }

    /// <summary>공격 시작 (콤보 인덱스) - 애니메이션/사운드 연결용</summary>
    public event System.Action<int> AttackStarted;
    /// <summary>적중 - 화면 흔들림/이펙트 연결용</summary>
    public event System.Action<Health, DamageInfo> HitLanded;

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
        controller.DashStarted += CancelAttack;
    }

    private void OnDisable()
    {
        if (controller != null) controller.DashStarted -= CancelAttack;
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
            if (phase != Phase.Idle) CancelAttack();
            return;
        }

        if (GameInput.AttackDown) bufferTimer = inputBufferTime;
        else if (bufferTimer > 0f) bufferTimer -= Time.deltaTime;

        float dt = Time.deltaTime;

        switch (phase)
        {
            case Phase.Idle:
                if (comboResetTimer > 0f)
                {
                    comboResetTimer -= dt;
                    if (comboResetTimer <= 0f) comboIndex = -1;
                }
                if (bufferTimer > 0f && controller.CanAct)
                    StartStep(comboIndex < 0 ? 0 : (comboIndex + 1) % weapon.combo.Length);
                break;

            case Phase.Startup:
                phaseTimer -= dt;
                if (phaseTimer <= 0f) EnterActive();
                break;

            case Phase.Active:
                phaseTimer -= dt;
                DoHitCheck();
                if (phaseTimer <= 0f) EnterRecovery();
                break;

            case Phase.Recovery:
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

        phase = Phase.Startup;
        phaseTimer = step.startup;
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
        phase = Phase.Active;
        phaseTimer = step.active;
        controller.AddHorizontalImpulse(attackDir * step.lunge);
        DoHitCheck();
    }

    private void EnterRecovery()
    {
        phase = Phase.Recovery;
        phaseTimer = weapon.combo[comboIndex].recovery;
        HideSlash();
    }

    private void FinishRecovery()
    {
        phase = Phase.Idle;
        controller.FacingLocked = false;
        controller.MoveSpeedMultiplier = 1f;

        float window = weapon.combo[comboIndex].comboWindow;
        if (window > 0f) comboResetTimer = window;
        else comboIndex = -1;
    }

    public void CancelAttack()
    {
        phase = Phase.Idle;
        comboIndex = -1;
        comboResetTimer = 0f;
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
