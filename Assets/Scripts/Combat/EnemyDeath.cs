using System.Collections;
using UnityEngine;

/// <summary>
/// 적 사망 처리: 사망 애니메이션 재생 → 서서히 사라짐 → 오브젝트 삭제.
/// 적 AI 담당자는 `health.IsDead` 또는 `Health.Died` 이벤트로 이동/공격을 멈추면 된다.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyDeath : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string dieTrigger = "Die";
    [SerializeField] private SpriteRenderer sprite;

    [Tooltip("사망 후 오브젝트가 삭제되기까지의 시간(초). 사망 애니메이션 길이 이상으로 설정")]
    [SerializeField] private float destroyDelay = 0.8f;
    [Tooltip("삭제 직전 서서히 투명해지는 시간(초)")]
    [SerializeField] private float fadeTime = 0.3f;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= OnDied;
    }

    private void OnDied(Health h)
    {
        // 공격 판정 끄기
        EnemyAttackBox[] boxes = GetComponentsInChildren<EnemyAttackBox>();
        for (int i = 0; i < boxes.Length; i++) boxes[i].Cancel();

        if (animator != null && !string.IsNullOrEmpty(dieTrigger) && HasParameter(dieTrigger))
            animator.SetTrigger(dieTrigger);

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        float wait = Mathf.Max(0f, destroyDelay - fadeTime);
        yield return new WaitForSeconds(wait);

        float t = 0f;
        Color start = sprite != null ? sprite.color : Color.white;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            if (sprite != null)
            {
                Color c = start;
                c.a = Mathf.Lerp(start.a, 0f, t / Mathf.Max(0.01f, fadeTime));
                sprite.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    private bool HasParameter(string name)
    {
        if (animator.runtimeAnimatorController == null) return false;
        AnimatorControllerParameter[] ps = animator.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].name == name) return true;
        return false;
    }
}
