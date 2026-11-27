using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 화면 좌상단 체력 UI. HPManager 및 UIManager와 연동되어 체력 변화를 표시한다.
/// </summary>
public class HealthUI : MonoBehaviour
{
    [SerializeField] private Health target;

    [Header("모양")]
    [Tooltip("비워두면 단색 정사각형을 사용")]
    [SerializeField] private Sprite fullSprite;
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private Color fullColor = new Color(0.9f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color emptyColor = new Color(0.3f, 0.06f, 0.06f, 0.6f);

    [Header("배치 (1920x1080 기준 픽셀, 좌상단 기준)")]
    [SerializeField, Min(1)] private int defaultCellCount = 5;
    [SerializeField] private Vector2 cellSize = new Vector2(48f, 48f);
    [SerializeField] private float spacingRatio = 0.5f;
    [SerializeField] private Vector2 margin = new Vector2(70f, 50f);

    [Header("피격 연출")]
    [SerializeField] private Color hurtFlashColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float flashDuration = 0.3f;
    [SerializeField] private float lostPulseDuration = 0.35f;
    [SerializeField] private float lostPulseScale = 1.6f;

    private Transform canvasRoot;
    private Canvas uiCanvas;
    private Image[] cells = new Image[0];
    private float flashTimer;
    private float pulseTimer;
    private int pulseFrom, pulseTo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HealthUI[] healthUIs = FindObjectsOfType<HealthUI>(true);
        bool visible = UIManager.Instance != null ? UIManager.Instance.IsHudAllowed(scene.name) : true;

        if (healthUIs.Length == 0)
        {
            HealthUI created = new GameObject("HealthUI").AddComponent<HealthUI>();
            created.SetHudVisible(visible);
            return;
        }

        for (int i = 0; i < healthUIs.Length; i++)
            healthUIs[i].SetHudVisible(visible);
    }

    private void Awake()
    {
        BuildCanvas();
        bool visible = UIManager.Instance != null
            ? UIManager.Instance.IsHudAllowed(SceneManager.GetActiveScene().name)
            : true;
        SetHudVisible(visible);
    }

    private void OnEnable()
    {
        if (HPManager.Instance != null)
        {
            HPManager.Instance.PlayerBound += OnPlayerBound;
            if (HPManager.Instance.PlayerHealth != null)
                Bind(HPManager.Instance.PlayerHealth);
        }
    }

    private void OnDisable()
    {
        if (HPManager.Instance != null)
            HPManager.Instance.PlayerBound -= OnPlayerBound;
    }

    private void Start()
    {
        if (target == null && HPManager.Instance != null)
            Bind(HPManager.Instance.PlayerHealth != null ? HPManager.Instance.PlayerHealth : HPManager.Instance.FindPlayerHealth());
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if (uiCanvas != null && !uiCanvas.enabled) return;

        if (flashTimer > 0f || pulseTimer > 0f)
        {
            float dt = Time.unscaledDeltaTime;
            flashTimer = Mathf.Max(0f, flashTimer - dt);
            pulseTimer = Mathf.Max(0f, pulseTimer - dt);
            ApplyColors();
        }
    }

    public void SetHudVisible(bool visible)
    {
        if (uiCanvas == null) uiCanvas = GetComponent<Canvas>();
        if (uiCanvas != null) uiCanvas.enabled = visible;
    }

    private void OnPlayerBound(Health h)
    {
        Bind(h);
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
        pulseFrom = target != null ? target.Current : 0;
        pulseTo = pulseFrom + Mathf.Max(1, info.amount);
        pulseTimer = lostPulseDuration;
    }

    private void BuildCanvas()
    {
        uiCanvas = GetComponent<Canvas>();
        if (uiCanvas == null) uiCanvas = gameObject.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.worldCamera = null;
        uiCanvas.planeDistance = 100f;
        uiCanvas.overrideSorting = true;
        uiCanvas.sortingOrder = 1000;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot = transform;
        RebuildCells(defaultCellCount);
        ApplyColors();
    }

    private void RebuildCells(int count)
    {
        for (int i = 0; i < cells.Length; i++)
            if (cells[i] != null) Destroy(cells[i].gameObject);

        float step = cellSize.x * (1f + spacingRatio);

        cells = new Image[Mathf.Max(0, count)];
        for (int i = 0; i < cells.Length; i++)
        {
            GameObject go = new GameObject("HP" + i);
            go.transform.SetParent(canvasRoot, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
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

            bool justLost = pulse > 0f && i >= pulseFrom && i < pulseTo;
            float scale = 1f;
            if (justLost)
            {
                float t = 1f - pulse;
                scale = Mathf.Lerp(lostPulseScale, 1f, t);
                c = Color.Lerp(hurtFlashColor, emptyColor, t);
            }

            img.color = c;
            img.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}