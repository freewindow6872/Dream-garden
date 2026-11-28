using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 탭 하나: 어떤 버튼이 어떤 유형의 아이템을 보여줄지
[System.Serializable]
public class INVENTORYTAB
{
    public string name;
    public Button button;
    public Image background;
    public ITEMTYPE[] types;
}

public class INVENTORYUI : MonoBehaviour
{
    public static INVENTORYUI Instance;

    // I키로 열 수 없는 씬 (타이틀 계열)
    static readonly string[] BLOCKEDSCENES = { "title", "GAMETITLE" };

    [Header("패널")]
    public GameObject panel;
    public Button closeButton;
    public Text gearText;

    [Header("탭 (칼 / 씨앗 / 나뭇잎 / 톱니)")]
    public INVENTORYTAB[] tabs;
    public Color tabColor = new Color(0.88f, 0.89f, 0.84f, 1f);
    public Color tabSelectedColor = new Color(0.72f, 0.78f, 0.55f, 1f);

    [Header("아이템 칸")]
    public ScrollRect scroll;
    public Transform slotContainer;
    public GameObject slotTemplate;          // 비활성 상태의 칸 견본 (ICON, BADGE 자식 필요)
    public int minSlots = 16;                // 아이템이 적어도 이 개수만큼 빈 칸을 보여줌
    public Color slotColor = new Color(0.98f, 0.98f, 0.96f, 1f);
    public Color selectedColor = new Color(1f, 0.95f, 0.66f, 1f);

    [Header("상세 정보 (아이템을 눌렀을 때)")]
    public GameObject detailRoot;
    public Image detailImage;
    public Text detailName;
    public Text detailCount;
    public GameObject detailCountBadge;
    public Text detailDescription;

    [Header("기타")]
    public ITEMDATABASE database;
    [Tooltip("체크하면 소지품이 열린 동안 게임이 멈춤")]
    public bool pauseGame = true;

    bool allowed = true;
    int currentTab;
    Image selectedSlotImage;
    float prevTimeScale = 1f;
    bool paused;
    PlayerController2D lockedPlayer;

    public bool IsOpen { get { return panel != null && panel.activeSelf; } }

    // ---------- 자동 생성: 어느 씬에서 시작해도 Resources/INVENTORYUI 프리팹을 한 번 만듦 ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject prefab = Resources.Load<GameObject>("INVENTORYUI");
        if (prefab == null)
        {
            Debug.LogWarning("[소지품] Assets/Resources/INVENTORYUI 프리팹이 없습니다. Tools > 소지품 UI 자동 만들기를 실행하세요.");
            return;
        }
        GameObject go = Instantiate(prefab);
        go.name = "INVENTORYUI";
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreTime();
    }

    void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (tabs != null)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => ShowTab(index));
            }
        }

        ApplySceneRule(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Close();                                  // 씬이 바뀌면 항상 닫고 시간도 복구
        ApplySceneRule(scene.name);
    }

    void ApplySceneRule(string sceneName)
    {
        allowed = System.Array.IndexOf(BLOCKEDSCENES, sceneName) < 0;
        if (!allowed) Close();
    }

    void Update()
    {
        if (!allowed) return;
        if (PressedI()) Toggle();
        if (IsOpen && GARDENINPUT.CancelPressed) Close();
    }

    bool PressedI()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.iKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.I);
#endif
    }

    // ---------- 열기 / 닫기 ----------
    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (!allowed || IsOpen) return;
        if (Time.timeScale == 0f) return;         // 정산 화면 등으로 이미 멈춰 있으면 열지 않음

        Refresh();
        panel.SetActive(true);

        if (pauseGame)
        {
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            paused = true;
        }
        LockPlayer(true);
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        RestoreTime();
        LockPlayer(false);
    }

    void RestoreTime()
    {
        if (!paused) return;
        Time.timeScale = prevTimeScale;
        paused = false;
    }

    // 소지품이 열린 동안 플레이어가 점프/대시 입력을 받지 않게 잠금 (이미 잠긴 경우는 건드리지 않음)
    void LockPlayer(bool lockIt)
    {
        if (lockIt)
        {
            PlayerController2D p = FindObjectOfType<PlayerController2D>();
            if (p != null && !p.InputLocked)
            {
                p.SetInputLocked(true);
                lockedPlayer = p;
            }
        }
        else if (lockedPlayer != null)
        {
            lockedPlayer.SetInputLocked(false);
            lockedPlayer = null;
        }
    }

    // ---------- 표시 ----------
    void Refresh()
    {
        if (gearText != null) gearText.text = "톱니바퀴  " + INVENTORY.Data.gears;
        ShowTab(currentTab);
    }

    void ShowTab(int index)
    {
        if (tabs == null || tabs.Length == 0 || database == null) return;

        currentTab = Mathf.Clamp(index, 0, tabs.Length - 1);
        for (int i = 0; i < tabs.Length; i++)
            if (tabs[i].background != null)
                tabs[i].background.color = (i == currentTab) ? tabSelectedColor : tabColor;

        BuildSlots();
    }

    void BuildSlots()
    {
        Clear(slotContainer);
        selectedSlotImage = null;

        INVENTORYTAB tab = tabs[currentTab];
        int made = 0;

        foreach (ITEMDATA item in database.items)
        {
            if (item == null || System.Array.IndexOf(tab.types, item.type) < 0) continue;

            int n = CountOf(item);
            if (n <= 0) continue;

            CreateSlot(item, n);
            made++;
        }

        for (; made < minSlots; made++) CreateSlot(null, 0);   // 시안처럼 빈 칸도 표시

        ShowDetail(null);                                      // 아이템을 누르기 전에는 숨김

        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    void CreateSlot(ITEMDATA item, int count)
    {
        GameObject go = Instantiate(slotTemplate, slotContainer);
        go.name = item != null ? "SLOT_" + item.itemName : "SLOT_EMPTY";
        go.SetActive(true);

        Image frame = go.GetComponent<Image>();
        Button btn = go.GetComponent<Button>();
        Transform iconTf = go.transform.Find("ICON");
        Transform badgeTf = go.transform.Find("BADGE");
        Image icon = iconTf != null ? iconTf.GetComponent<Image>() : null;

        if (frame != null) frame.color = slotColor;

        if (item == null)                                      // 빈 칸
        {
            if (icon != null) icon.enabled = false;
            if (badgeTf != null) badgeTf.gameObject.SetActive(false);
            if (btn != null) Destroy(btn);
            return;
        }

        if (icon != null)
        {
            icon.enabled = item.icon != null;
            if (item.icon != null) icon.sprite = item.icon;
        }

        if (badgeTf != null)
        {
            bool stack = IsStackable(item.type);
            badgeTf.gameObject.SetActive(stack);
            Text t = badgeTf.GetComponentInChildren<Text>();
            if (stack && t != null) t.text = "x" + count;
        }

        if (btn != null) btn.onClick.AddListener(() => SelectItem(item, frame));
    }

    void SelectItem(ITEMDATA item, Image slotImage)
    {
        if (selectedSlotImage != null) selectedSlotImage.color = slotColor;   // 이전 선택 해제
        selectedSlotImage = slotImage;
        if (slotImage != null) slotImage.color = selectedColor;

        ShowDetail(item);
    }

    void ShowDetail(ITEMDATA item)
    {
        if (detailRoot == null) return;

        if (item == null)
        {
            detailRoot.SetActive(false);
            return;
        }

        detailRoot.SetActive(true);

        if (detailName != null) detailName.text = item.itemName;
        if (detailDescription != null) detailDescription.text = item.description;

        if (detailImage != null)
        {
            detailImage.sprite = item.icon;
            detailImage.enabled = item.icon != null;
        }

        bool stack = IsStackable(item.type);
        if (detailCountBadge != null) detailCountBadge.SetActive(stack);
        if (stack && detailCount != null) detailCount.text = "x" + INVENTORY.Count(item.itemName);
    }

    // ---------- 보조 ----------
    // 개수가 쌓이는 아이템인지 (식물/동물은 하나만 가지므로 개수 표시 안 함)
    bool IsStackable(ITEMTYPE type)
    {
        return type == ITEMTYPE.Seed || type == ITEMTYPE.Consumable || type == ITEMTYPE.Plant;
    }

    int CountOf(ITEMDATA item)
    {
        if (IsStackable(item.type)) return INVENTORY.Count(item.itemName);
        return INVENTORY.IsOwned(item) ? 1 : 0;
    }

    void Clear(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform c in parent)
        {
            c.gameObject.SetActive(false);        // 레이아웃에서 바로 빠지도록
            Destroy(c.gameObject);
        }
    }

    // ---------- 테스트용 (Play 중 컴포넌트 ⋮ 메뉴) ----------
    [ContextMenu("TEST 데이터베이스 전체 지급")]
    void TestGrantAll()
    {
        foreach (ITEMDATA it in database.items) INVENTORY.Data.itemNames.Add(it.itemName);
        INVENTORY.Save();
        if (IsOpen) Refresh();
    }

    [ContextMenu("TEST 클리어 1회 추가")]
    void TestClear()
    {
        var unlocked = INVENTORY.RegisterClear(database);
        Debug.Log("[소지품] 누적 클리어 " + INVENTORY.Data.clearCount + "회 / 새로 해금 " + unlocked.Count + "개");
        if (IsOpen) Refresh();
    }

    [ContextMenu("TEST 해금된 식물/동물 취소")]
    void TestRevokeUnlocks()
    {
        INVENTORY.Data.unlockedNames.Clear();
        INVENTORY.Data.itemNames.RemoveAll(n =>
        {
            ITEMDATA it = database.items.Find(i => i != null && i.itemName == n);
            return it != null && (it.type == ITEMTYPE.Plant || it.type == ITEMTYPE.Animal);
        });
        INVENTORY.Save();
        if (IsOpen) Refresh();
    }

    [ContextMenu("TEST 소지품 초기화")]
    void TestReset()
    {
        INVENTORY.ResetAll();
        if (IsOpen) Refresh();
    }
}