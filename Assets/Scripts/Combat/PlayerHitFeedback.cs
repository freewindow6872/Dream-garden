using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어가 맞았을 때 눈에 띄는 연출을 모아둔 컴포넌트. (Health.Damaged 이벤트만 구독 → 다른 코드와 독립)
/// 현재 설정: 붉은 화면 / 캐릭터 붉은 번쩍임 / 히트스톱 / 붉은 섬광은 모두 꺼져 있음.
/// 남아 있는 것: 머리 위 빨간 "-1" 숫자 (끄려면 showDamageNumber를 false로)
/// (켜고 싶으면 아래 시간 값들을 0이 아닌 값으로, 섬광은 SpawnDebugBurst 줄 주석 해제)
///
/// 따로 붙이지 않아도 씬 로드 시 플레이어에게 자동으로 추가된다. 빼려면 enabled를 끄면 됨.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerHitFeedback : MonoBehaviour
{
    [Header("캐릭터 번쩍임")]
    [SerializeField] private Color bodyFlashColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private float bodyFlashTime = 0f;      // 0 = 캐릭터 붉은 번쩍임 없음

    [Header("화면 번쩍임")]
    [SerializeField] private Color screenFlashColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private float screenFlashTime = 0f;    // 0 = 붉은 화면 없음

    [Header("기타")]
    [SerializeField] private bool showDamageNumber = true;
    [SerializeField] private float hitStopTime = 0f;        // 0 = 맞을 때 멈칫 없음

    private Health health;
    private SpriteRenderer sprite;
    private Color baseRgb = Color.white;
    private float bodyTimer;
    private float screenTimer;
    private Image overlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        PlayerCombat player = FindObjectOfType<PlayerCombat>();
        if (player != null && player.GetComponent<PlayerHitFeedback>() == null)
            player.gameObject.AddComponent<PlayerHitFeedback>();
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) baseRgb = sprite.color;
        BuildOverlay();
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay.canvas.gameObject);
    }

    private void OnDamaged(DamageInfo info)
    {
        bodyTimer = bodyFlashTime;
        screenTimer = screenFlashTime;

        Collider2D col = GetComponent<Collider2D>();
        Vector2 top = col != null ? new Vector2(col.bounds.center.x, col.bounds.max.y + 0.2f) : (Vector2)transform.position + Vector2.up;
        if (showDamageNumber) DamagePopup.Spawn(top, "-" + info.amount, DamagePopup.PlayerHurtColor, 1.3f);

        // CombatEffect.SpawnDebugBurst(point, new Color(1f, 0.2f, 0.2f, 1f), 0.9f);   // 붉은 타격 섬광 (꺼둠)

        if (hitStopTime > 0f) HitStop.Do(hitStopTime);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // 캐릭터: RGB만 붉게 → 원래 색으로 (알파는 PlayerController2D의 무적 깜빡임이 관리)
        if (bodyTimer > 0f) bodyTimer -= dt;
        if (sprite != null)
        {
            float k = bodyFlashTime > 0f ? Mathf.Clamp01(bodyTimer / bodyFlashTime) : 0f;
            Color c = sprite.color;
            Color rgb = Color.Lerp(baseRgb, bodyFlashColor, k);
            c.r = rgb.r; c.g = rgb.g; c.b = rgb.b;
            sprite.color = c;
        }

        // 화면
        if (screenTimer > 0f) screenTimer -= dt;
        if (overlay != null)
        {
            float k = screenFlashTime > 0f ? Mathf.Clamp01(screenTimer / screenFlashTime) : 0f;
            Color c = screenFlashColor;
            c.a = screenFlashColor.a * k;
            overlay.color = c;
        }
    }

    private void BuildOverlay()
    {
        GameObject go = new GameObject("PlayerHitOverlay");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;   // HealthUI(100)보다 아래

        GameObject imgGo = new GameObject("Flash");
        imgGo.transform.SetParent(go.transform, false);
        overlay = imgGo.AddComponent<Image>();
        overlay.raycastTarget = false;
        overlay.color = new Color(1f, 0f, 0f, 0f);

        RectTransform rt = overlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
