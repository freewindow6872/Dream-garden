using UnityEngine;
using UnityEngine.UI;

public class PLAYERINTERACTOR : MonoBehaviour
{
    [Tooltip("이 거리 안의 대상과 상호작용 가능")]
    public float range = 1.5f;

    [Header("안내 문구 (선택)")]
    public GameObject promptRoot;     // 안내 패널
    public Text promptText;           // "[E] 화분" 이 표시될 글자

    INTERACTABLE current;

    void Start()
    {
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    readonly Collider2D[] hits = new Collider2D[16];   // 클래스 필드로 추가

    INTERACTABLE FindNearest()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;

        int count = Physics2D.OverlapCircle(transform.position, range, filter, hits);

        INTERACTABLE best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            INTERACTABLE it = hits[i].GetComponentInParent<INTERACTABLE>();
            if (it == null || !it.enabled) continue;

            float d = Vector2.Distance(transform.position, it.transform.position);
            if (d < bestDist) { bestDist = d; best = it; }
        }
        return best;
    }

    void SetCurrent(INTERACTABLE it)
    {
        if (it == current) return;
        current = it;

        if (promptRoot != null) promptRoot.SetActive(it != null);
        if (promptText != null && it != null) promptText.text = "[E]  " + it.promptName;

    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);
    }
    void Update()
    {
        SetCurrent(FindNearest());

        if (current != null && GARDENINPUT.InteractPressed)
            current.Interact();
    }
}