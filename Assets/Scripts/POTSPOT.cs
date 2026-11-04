using UnityEngine;
using UnityEngine.EventSystems;

public class POTSPOT : MonoBehaviour
{
    [Tooltip("화분마다 서로 다르게 (POT1, POT2 ...)")]
    public string potId = "POT1";

    [Header("심은 뒤 모습 (선택)")]
    public Sprite plantedSprite;                       // 비우면 색만 변경
    public Color emptyColor = Color.white;
    public Color plantedColor = new Color(0.65f, 1f, 0.65f);

    SpriteRenderer sr;
    Sprite emptySprite;
    Collider2D col;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        if (sr != null) emptySprite = sr.sprite;
    }

    void Start()
    {
        RefreshVisual();
    }

    void Update()
    {
        if (col == null || POTPANEL.Instance == null) return;
        if (POTPANEL.Instance.IsOpen) return;
        if (!Clicked(out Vector2 screenPos)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 sp = new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z));
        Vector2 world = cam.ScreenToWorldPoint(sp);

        if (col.OverlapPoint(world)) POTPANEL.Instance.Open(this);
    }

    bool Clicked(out Vector2 pos)
    {
#if ENABLE_INPUT_SYSTEM
        var m = UnityEngine.InputSystem.Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame)
        {
            pos = m.position.ReadValue();
            return true;
        }
        pos = Vector2.zero;
        return false;
#else
        pos = Input.mousePosition;
        return Input.GetMouseButtonDown(0);
#endif
    }

    public void RefreshVisual()
    {
        if (sr == null) return;
        POTSTATE s = INVENTORY.GetPot(potId);
        sr.color = s.planted ? plantedColor : emptyColor;
        if (plantedSprite != null) sr.sprite = s.planted ? plantedSprite : emptySprite;
    }

    [ContextMenu("TEST 화분 비우기")]
    void TestEmpty()
    {
        POTSTATE s = INVENTORY.GetPot(potId);
        s.planted = false;
        s.fertilized = false;
        INVENTORY.Save();
        RefreshVisual();
    }
}