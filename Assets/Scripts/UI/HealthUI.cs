using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 좌상단 체력 UI. (할로우 나이트처럼 좌상단에 붉은 정사각형 칸이 가로로 늘어선다)
/// 플레이어(Team.Player) Health를 자동으로 찾아 연결한다.
///  - 칸 개수는 Health.Max를 따라가고, 남은 칸은 fullColor(빨강) / 빈 칸은 emptyColor(어두운 빨강)
///  - 칸 간격 = 칸 크기 × spacingRatio (기본 0.5 → 칸 반 개 만큼 띄움)
///  - 사각형은 RuntimeSprites.White(코드 생성)라 에셋이 필요 없다. 디자인이 나오면 fullSprite / emptySprite만 넣으면 교체됨
///  - 피격 시 전체가 순간 하얗게 번쩍이고, 방금 잃은 칸은 커졌다가 사라지는 연출이 나옴
///  - 코드로 Canvas를 만들기 때문에 씬에 따로 배치할 필요 없음 (CombatBootstrap이 자동 생성)
/// </summary>
public class HealthUI : MonoBehaviour
{
    [SerializeField] private Health target;

    [Header("모양")]
    [Tooltip("비워두면 단색 정사각형을 사용")]
    [SerializeField] private Sprite fullSprite;
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private Color fullColor = new Color(0.9f, 0.1f, 0.1f, 1f);          // 붉은색
    [SerializeField] private Color emptyColor = new Color(0.3f, 0.06f, 0.06f, 0.6f);     // 빈 칸: 어두운 빨강

    [Header("배치 (1920x1080 기준 픽셀, 좌상단 기준)")]
    [Tooltip("칸 하나의 크기 (정사각형)")]
    [SerializeField] private Vector2 cellSize = new Vector2(48f, 48f);
    [Tooltip("칸 사이 간격 = 칸 가로 크기 × 이 값. 0.5면 칸 반 개 너비만큼 띄움")]
    [SerializeField] private float spacingRatio = 0.5f;
    [SerializeField] private Vector2 margin = new Vector2(70f, 50f);

    [Header("피격 연출")]
    [SerializeField] private Color hurtFlashColor = new Color(1f, 1f, 1f, 1f);   // 칸이 빨강이라 피격 번쩍임은 흰색
    [SerializeField] private float flashDuration = 0.3f;
    [Tooltip("방금 잃은 칸이 커졌다 사라지는 시간(초)")]
    [SerializeField] private float lostPulseDuration = 0.35f;
    [SerializeField] private float lostPulseScale = 1.6f;

    private Transform canvasRoot;
    private Image[] cells = new Image[0];
    private float flashTimer;
    private float pulseTimer;
    private int pulseFrom, pulseTo;      // 방금 잃은 칸 인덱스 범위 [from, to)
    private float findTimer;
    private float searchTime;
    private bool warned;

    /// <summary>씬에 HealthUI가 없으면 자동 생성 (CombatBootstrap과 별개로, 이것만으로도 동작)</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindObjectOfType<HealthUI>() == null)
            new GameObject("HealthUI").AddComponent<HealthUI>();
    }

    private void Awake()
    {
        BuildCanvas();
    }

    private void Start()
    {
        Bind(FindPlayerHealth());
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if (target == null)
        {
            findTimer -= Time.unscaledDeltaTime;
            if (findTimer <= 0f)
            {
                findTimer = 0.5f;
                Bind(FindPlayerHealth());
            }

            searchTime += Time.unscaledDeltaTime;
            if (target == null && !warned && searchTime > 2f)
            {
                warned = true;
                Debug.LogWarning("[HealthUI] 플레이어의 Health를 못 찾았어요. Player 오브젝트에 PlayerCombat + Health 컴포넌트가 있는지 확인하세요.");
            }
            return;
        }

        // 히트스톱(timeScale 저하) 중에도 같은 속도로 움직이도록 unscaled 사용
        if (flashTimer > 0f) flashTimer -= Time.unscaledDeltaTime;
        if (pulseTimer > 0f) pulseTimer -= Time.unscaledDeltaTime;
        ApplyColors();
    }

    // ---------------------------------------------------------------- binding

    private Health FindPlayerHealth()
    {
        // 1순위: PlayerCombat이 붙은 오브젝트의 Health (Team 설정이 틀려도 찾아냄)
        PlayerCombat pc = FindObjectOfType<PlayerCombat>();
        if (pc != null)
        {
            Health ph = pc.GetComponent<Health>();
            if (ph != null) return ph;
        }

        // 2순위: Team.Player인 Health
        Health[] all = FindObjectsOfType<Health>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i].Team == Team.Player) return all[i];
        return null;
    }

    private void Bind(Health h)
    {
        if (h == null || h == target) return;
        Unbind();

        target = h;
        target.Changed += OnChanged;
        target.Damaged += OnDamaged;

        RebuildCells(target.Max);
        ApplyColors();
    }

    private void Unbind()
    {
        if (target == null) return;
        target.Changed -= OnChanged;
        target.Damaged -= OnDamaged;
        target = null;
    }

    private void OnChanged(int current, int max)
    {
        if (max != cells.Length) RebuildCells(max);
        ApplyColors();
    }

    private void OnDamaged(DamageInfo info)
    {
        flashTimer = flashDuration;

        // Health가 Damaged를 부르는 시점엔 이미 Current가 줄어 있으므로 [Current, Current+amount)가 방금 잃은 칸
        pulseFrom = target != null ? target.Current : 0;
        pulseTo = pulseFrom + Mathf.Max(1, info.amount);
        pulseTimer = lostPulseDuration;
    }

    // ---------------------------------------------------------------- build / draw

    private void BuildCanvas()
    {
        // Use this object's Canvas directly. A nested Canvas under a World Space Canvas
        // can remain invisible or inherit confusing render setup in existing scenes.
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.planeDistance = 100f;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot = transform;
        RebuildCells(5);
        ApplyColors();
    }

    private void RebuildCells(int count)
    {
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] != null) Destroy(cells[i].gameObject);

        float step = cellSize.x * (1f + spacingRatio);   // 칸 + 간격

        cells = new Image[Mathf.Max(0, count)];
        for (int i = 0; i < cells.Length; i++)
        {
            GameObject go = new GameObject("HP" + i);
            go.transform.SetParent(canvasRoot, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // 좌상단 기준
            rt.pivot = new Vector2(0.5f, 0.5f);                  // 커졌다 사라지는 연출이 중앙 기준이 되도록
            rt.sizeDelta = cellSize;
            rt.anchoredPosition = new Vector2(margin.x + cellSize.x * 0.5f + i * step,
                                              -(margin.y + cellSize.y * 0.5f));

            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.sprite = RuntimeSprites.White;
            cells[i] = img;
        }
    }

    private void ApplyColors()
    {
        // Health 연결 전에는 기본 5칸을 모두 채운 상태로 표시한다.
        int current = target != null ? target.Current : cells.Length;
        float flash = flashDuration > 0f ? Mathf.Clamp01(flashTimer / flashDuration) : 0f;
        float pulse = lostPulseDuration > 0f ? Mathf.Clamp01(pulseTimer / lostPulseDuration) : 0f;
        Sprite square = RuntimeSprites.White;

        for (int i = 0; i < cells.Length; i++)
        {
            bool full = i < current;
            Color baseColor = full ? fullColor : emptyColor;

            Color c = Color.Lerp(baseColor, hurtFlashColor, flash);
            c.a = baseColor.a;

            Image img = cells[i];
            Sprite custom = full ? fullSprite : emptySprite;
            img.sprite = custom != null ? custom : square;

            // 방금 잃은 칸: 붉게 커졌다가(1→lostPulseScale) 빈 칸 색으로 가라앉음
            bool justLost = pulse > 0f && i >= pulseFrom && i < pulseTo;
            float scale = 1f;
            if (justLost)
            {
                float t = 1f - pulse;                       // 0 → 1
                scale = Mathf.Lerp(lostPulseScale, 1f, t);
                c = Color.Lerp(hurtFlashColor, emptyColor, t);
            }

            img.color = c;
            img.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
