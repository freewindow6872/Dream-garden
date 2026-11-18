using UnityEngine;

public class POTSPOT : INTERACTABLE
{
    [Tooltip("화분마다 서로 다르게 (POT1, POT2 ...)")]
    public string potId = "POT1";

    [Header("모습 (선택)")]
    public Sprite plantedSprite;                       // 심은 뒤 그림. 비우면 색만 변경
    public Color emptyColor = Color.white;
    public Color plantedColor = new Color(0.65f, 1f, 0.65f);   // 성장 중
    public Color readyColor = new Color(1f, 0.9f, 0.4f);       // 수확 가능

    SpriteRenderer sr;
    Sprite emptySprite;

    void Reset() { promptName = "화분"; }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) emptySprite = sr.sprite;
    }

    void Start()
    {
        RefreshVisual();
    }

    public override string GetPrompt()
    {
        POTSTATE s = INVENTORY.GetPot(potId);
        if (!s.planted) return promptName + " (씨앗 심기)";
        bool ready = POTPANEL.Instance != null && POTPANEL.Instance.IsReady(s);
        return promptName + (ready ? " (수확하기)" : " (성장 중)");
    }

    public override void Interact()
    {
        if (POTPANEL.Instance != null) POTPANEL.Instance.Open(this);
    }

    public void RefreshVisual()
    {
        if (sr == null) return;

        POTSTATE s = INVENTORY.GetPot(potId);
        bool ready = POTPANEL.Instance != null && POTPANEL.Instance.IsReady(s);

        sr.color = !s.planted ? emptyColor : (ready ? readyColor : plantedColor);
        if (plantedSprite != null) sr.sprite = s.planted ? plantedSprite : emptySprite;
    }

    [ContextMenu("TEST 화분 비우기")]
    void TestEmpty()
    {
        POTSTATE s = INVENTORY.GetPot(potId);
        s.planted = false;
        s.fertilized = false;
        s.seedName = "";
        INVENTORY.Save();
        RefreshVisual();
    }
}