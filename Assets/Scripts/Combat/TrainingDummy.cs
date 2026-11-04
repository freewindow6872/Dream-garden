using System.Collections;
using UnityEngine;

/// <summary>
/// 허수아비(샌드백). 움직이지도 공격하지도 않고, 맞으면 흔들리며 붉게 깜빡인다.
///  - 체력이 0이 되면 쓰러진 척 하다가 잠시 뒤 완전히 회복해서 다시 일어난다 (죽지 않음)
///  - 한동안 안 맞으면 체력이 자동으로 가득 찬다 → 계속 때려보며 데미지를 확인할 수 있음
///  - 데미지 숫자/체력바는 DamagePopupEmitter / EnemyHealthBar가 담당 (이 컴포넌트와 독립)
///  - 스프라이트는 자식 오브젝트(visual)를 흔든다. 그림을 바꿔도 그대로 동작
/// </summary>
[RequireComponent(typeof(Health))]
public class TrainingDummy : MonoBehaviour
{
    [Tooltip("흔들릴 그림 오브젝트 (비워두면 첫 번째 자식 SpriteRenderer를 사용). 피벗은 발 밑에 두는 것을 권장")]
    [SerializeField] private Transform visual;

    [Header("회복")]
    [Tooltip("마지막으로 맞은 뒤 이 시간(초)이 지나면 체력이 가득 참. 0이면 자동 회복 없음")]
    [SerializeField] private float refillAfterIdle = 2.5f;
    [Tooltip("체력이 0이 된 뒤 다시 일어나기까지(초)")]
    [SerializeField] private float reviveDelay = 1.0f;

    [Header("흔들림")]
    [SerializeField] private float wobbleStrength = 260f;
    [SerializeField] private float spring = 140f;
    [SerializeField] private float damping = 9f;
    [SerializeField] private float knockedDownAngle = 80f;

    [Header("피격 색")]
    [SerializeField] private Color flashColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private float flashTime = 0.15f;

    private Health health;
    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private float angle, angVel;
    private float flash;
    private float lastHitTime;
    private bool down;
    private float downAngle;

    private void Awake()
    {
        health = GetComponent<Health>();

        if (visual == null)
        {
            SpriteRenderer first = GetComponentInChildren<SpriteRenderer>();
            if (first != null) visual = first.transform;
        }

        renderers = GetComponentsInChildren<SpriteRenderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;

        lastHitTime = Time.time;
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
    }

    private void OnDamaged(DamageInfo info)
    {
        lastHitTime = Time.time;
        flash = flashTime;

        // 맞은 방향의 반대쪽으로 기울어짐
        float dir = Mathf.Abs(info.knockback.x) > 0.01f ? Mathf.Sign(info.knockback.x) : 1f;
        angVel += -dir * wobbleStrength;
    }

    private void OnDied(Health h)
    {
        down = true;
        float dir = angVel != 0f ? Mathf.Sign(angVel) : 1f;
        downAngle = dir * knockedDownAngle;
        StartCoroutine(ReviveRoutine());
    }

    private IEnumerator ReviveRoutine()
    {
        yield return new WaitForSecondsRealtime(reviveDelay);
        health.ResetToMax();
        down = false;
        lastHitTime = Time.time;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // 가만히 두면 체력 가득 채움
        if (!down && refillAfterIdle > 0f && health.Current < health.Max && Time.time - lastHitTime >= refillAfterIdle)
            health.ResetToMax();

        // 스프링 흔들림 (쓰러진 상태면 목표 각도로)
        float target = down ? downAngle : 0f;
        float acc = -spring * (angle - target) - damping * angVel;
        angVel += acc * dt;
        angle += angVel * dt;
        if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, angle);

        // 붉은 깜빡임
        if (flash > 0f) flash -= dt;
        float k = flashTime > 0f ? Mathf.Clamp01(flash / flashTime) : 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = Color.Lerp(baseColors[i], flashColor, k);
            c.a = baseColors[i].a;
            renderers[i].color = c;
        }
    }
}
