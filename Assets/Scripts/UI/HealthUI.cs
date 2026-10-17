using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 좌상단 체력 칸 UI. 플레이어(Team.Player) Health를 자동으로 찾아 연결한다.
///  - 칸 수는 Health.Max를 따라가고, 남은 칸은 fullColor / 빈 칸은 emptyColor
///  - 피격 시 UI 색이 순간 빨개졌다가 원래 색으로 돌아옴
///  - 코드로 Canvas를 만들기 때문에 씬에 따로 배치할 필요 없음 (CombatBootstrap이 자동 생성)
///  - 나중에 디자인이 나오면 fullSprite / emptySprite만 넣으면 됨 (스프라이트 없으면 단색 사각형)
/// </summary>
public class HealthUI : MonoBehaviour
{
    [SerializeField] private Health target;

    [Header("모양")]
    [SerializeField] private Sprite fullSprite;
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private Color fullColor = new Color(0.35f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.7f);

    [Header("배치 (1920x1080 기준 픽셀)")]
    [SerializeField] private Vector2 cellSize = new Vector2(48f, 48f);
    [SerializeField] private float spacing = 10f;
    [SerializeField] private Vector2 margin = new Vector2(30f, 30f);

    [Header("피격 깜빡임")]
    [SerializeField] private Color hurtFlashColor = new Color(1f, 0.05f, 0.05f, 1f);
    [SerializeField] private float flashDuration = 0.3f;

    private Transform canvasRoot;
    private Image[] cells = new Image[0];
    private float flashTimer;
    private float findTimer;

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
            return;
        }

        // 히트스톱(timeScale 저하) 중에도 같은 속도로 깜빡이도록 unscaled 사용
        if (flashTimer > 0f) flashTimer -= Time.unscaledDeltaTime;
        ApplyColors();
    }

    // ---------------------------------------------------------------- binding

    private Health FindPlayerHealth()
    {
        Health[] all = FindObjectsOfType<Health>();
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
    }

    // ---------------------------------------------------------------- build / draw

    private void BuildCanvas()
    {
        GameObject go = new GameObject("HealthUICanvas");
        go.transform.SetParent(transform, false);
        canvasRoot = go.transform;

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void RebuildCells(int count)
    {
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] != null) Destroy(cells[i].gameObject);

        cells = new Image[Mathf.Max(0, count)];
        for (int i = 0; i < cells.Length; i++)
        {
            GameObject go = new GameObject("Cell" + i);
            go.transform.SetParent(canvasRoot, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);   // 좌상단 기준
            rt.sizeDelta = cellSize;
            rt.anchoredPosition = new Vector2(margin.x + i * (cellSize.x + spacing), -margin.y);

            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            cells[i] = img;
        }
    }

    private void ApplyColors()
    {
        if (target == null) return;

        float flash = flashDuration > 0f ? Mathf.Clamp01(flashTimer / flashDuration) : 0f;

        for (int i = 0; i < cells.Length; i++)
        {
            bool full = i < target.Current;
            Color baseColor = full ? fullColor : emptyColor;

            Color c = Color.Lerp(baseColor, hurtFlashColor, flash);
            c.a = baseColor.a;

            Image img = cells[i];
            img.sprite = full ? fullSprite : emptySprite;
            img.color = c;
        }
    }
}
