using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 적 몸 아래(발밑) 체력바. 회색 배경 + 체력(빨강).
/// 맞으면 빨간 체력은 즉시 줄고, 흰색 "잔상"이 잠깐 뒤 천천히 따라 내려와서 얼마나 깎였는지 보인다.
///
///  - 에셋 불필요 (코드로 만든 단색 사각형). 나중에 디자인이 나오면 모양만 교체하면 됨
///  - 바는 대상의 자식이 아니라 따로 생성되어 따라다닌다 → 적이 좌우로 뒤집혀도 바가 뒤집히지 않음
///  - 시간은 unscaled 기준이라 히트스톱 중에도 같은 속도
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyHealthBar : MonoBehaviour
{
    [Header("크기 / 위치")]
    [SerializeField] private Vector2 size = new Vector2(1.2f, 0.14f);
    [Tooltip("몸(그림/콜라이더) 아랫면에서 얼마나 아래에 띄울지")]
    [FormerlySerializedAs("heightAboveCollider")]
    [SerializeField] private float gapBelowCollider = 0.25f;
    [Tooltip("콜라이더가 없을 때: 오브젝트 기준점(발) 아래로 얼마나 내릴지")]
    [SerializeField] private float fallbackDepth = 0.25f;
    [SerializeField] private float border = 0.03f;

    [Header("색")]
    [SerializeField] private Color backColor = new Color(0.25f, 0.25f, 0.25f, 0.95f);   // 회색 배경
    [SerializeField] private Color fillColor = new Color(0.85f, 0.2f, 0.2f, 1f);         // 현재 체력
    [SerializeField] private Color trailColor = new Color(1f, 1f, 1f, 0.9f);             // 깎인 부분 잔상

    [Header("연출")]
    [Tooltip("맞은 뒤 잔상이 줄어들기 시작하기까지 대기(초)")]
    [SerializeField] private float trailDelay = 0.35f;
    [Tooltip("잔상이 줄어드는 속도 (체력 비율/초)")]
    [SerializeField] private float trailSpeed = 1.2f;
    [Tooltip("켜면 처음 맞기 전까지는 체력바를 숨김")]
    [SerializeField] private bool hideUntilDamaged = false;

    private Health health;
    private Collider2D col;
    private SpriteRenderer body;     // 눈에 보이는 몸 (스프라이트 아랫면 기준으로 바를 붙이기 위해)
    private Transform root;
    private SpriteRenderer back, trail, fill;
    private float trailRatio = 1f;
    private float trailTimer;
    private bool shown;

    private void Awake()
    {
        health = GetComponent<Health>();
        col = GetComponent<Collider2D>();
        body = GetComponentInChildren<SpriteRenderer>();   // 체력바는 따로 생성되는 오브젝트라 여기에 섞이지 않음
        Build();
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        health.Changed += OnChanged;
        if (root != null) root.gameObject.SetActive(!hideUntilDamaged || shown);
    }

    private void OnDisable()
    {
        if (health != null) health.Changed -= OnChanged;
        if (root != null) root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    private void Build()
    {
        GameObject go = new GameObject("EnemyHealthBar");
        root = go.transform;

        back = MakePart("Back", 40);
        trail = MakePart("Trail", 41);
        fill = MakePart("Fill", 42);
        back.color = backColor;
        trail.color = trailColor;
        fill.color = fillColor;

        shown = !hideUntilDamaged;
        go.SetActive(shown);
        Layout(1f, 1f);
    }

    private SpriteRenderer MakePart(string name, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprites.White;
        sr.sortingOrder = order;
        return sr;
    }

    private void OnChanged(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;

        if (!shown)
        {
            shown = true;
            root.gameObject.SetActive(true);
        }

        // 체력이 줄었으면 잔상은 이전 값 유지 후 지연 뒤 감소, 회복이면 잔상도 같이 올림
        if (ratio > trailRatio) trailRatio = ratio;
        else trailTimer = trailDelay;
    }

    private void LateUpdate()
    {
        if (health == null) return;

        float ratio = health.Max > 0 ? (float)health.Current / health.Max : 0f;

        if (trailRatio > ratio)
        {
            if (trailTimer > 0f) trailTimer -= Time.unscaledDeltaTime;
            else trailRatio = Mathf.Max(ratio, trailRatio - trailSpeed * Time.unscaledDeltaTime);
        }
        else trailRatio = ratio;

        // 위치: 대상 콜라이더 아랫면 아래에 고정. 매 프레임 따라다니므로 봇이 움직이면 같이 움직인다.
        root.position = GetAnchorPosition();

        Layout(ratio, trailRatio);
    }

    /// <summary>
    /// 바가 놓일 월드 위치: 몸통 가운데 x, 그리고 "스프라이트 아랫면"과 "콜라이더 아랫면" 중 더 낮은 곳 - gap.
    /// 스프라이트 피벗이 중앙이면 그림이 발 기준점보다 아래로 내려가는데, 그때도 바가 그림과 겹치지 않는다.
    /// 콜라이더 bounds 대신 transform 기준으로 계산 → Rigidbody2D 보간이 켜져도 떨리지 않는다.
    /// </summary>
    private Vector3 GetAnchorPosition()
    {
        float x = transform.position.x;
        float bottom = transform.position.y - fallbackDepth;
        bool has = false;

        BoxCollider2D box = col as BoxCollider2D;
        if (box != null)
        {
            Vector3 center = transform.TransformPoint(box.offset);
            x = center.x;
            bottom = center.y - box.size.y * 0.5f * Mathf.Abs(transform.lossyScale.y);
            has = true;
        }
        else if (col != null)
        {
            x = col.bounds.center.x;
            bottom = col.bounds.min.y;
            has = true;
        }

        if (body != null && body.sprite != null)
        {
            Bounds b = body.bounds;
            if (!has) x = b.center.x;
            bottom = has ? Mathf.Min(bottom, b.min.y) : b.min.y;
            has = true;
        }

        return new Vector3(x, has ? bottom - gapBelowCollider : bottom, 0f);
    }

    /// <summary>가로로 줄어드는 바. 스프라이트 피벗이 중앙이라 왼쪽 기준으로 위치를 보정한다.</summary>
    private void Layout(float ratio, float trailR)
    {
        float w = size.x, h = size.y;

        SetBar(back.transform, w + border * 2f, h + border * 2f, 1f, w + border * 2f);
        SetBar(trail.transform, w, h, trailR, w);
        SetBar(fill.transform, w, h, ratio, w);
    }

    private static void SetBar(Transform t, float fullWidth, float height, float ratio, float baseWidth)
    {
        float wNow = Mathf.Max(0f, fullWidth * ratio);
        t.localScale = new Vector3(wNow, height, 1f);
        t.localPosition = new Vector3(-baseWidth * 0.5f + wNow * 0.5f, 0f, 0f);
    }
}
