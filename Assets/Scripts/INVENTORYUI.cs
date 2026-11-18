using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class INVENTORYUI : MonoBehaviour
{
    public static INVENTORYUI Instance;

    // I키로 열 수 없는 씬 (타이틀 계열)
    static readonly string[] BLOCKEDSCENES = { "title", "GAMETITLE" };

    [Header("패널")]
    public GameObject panel;
    public Button closeButton;

    [Header("표시")]
    public Text gearText;
    public Transform plantContainer;
    public Transform animalContainer;
    public Transform materialContainer;   
    public GameObject entryPrefab;

    [Header("아이템 정보")]
    public ITEMDATABASE database;

    bool allowed = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject prefab = Resources.Load<GameObject>("INVENTORYUI");
        if (prefab == null)
        {
            Debug.LogWarning("[소지품] Assets/Resources/INVENTORYUI 프리팹이 없습니다.");
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
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        panel.SetActive(false);
        ApplySceneRule(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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

    public void Toggle()
    {
        if (panel.activeSelf) Close();
        else Open();
    }

    public void Open()
    {
        if (!allowed) return;
        Refresh();
        panel.SetActive(true);
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    void Refresh()
    {
        gearText.text = "톱니바퀴  " + INVENTORY.Data.gears;

        Clear(plantContainer);
        Clear(animalContainer);
        Clear(materialContainer);

        bool anyPlant = false, anyAnimal = false, anyMaterial = false;

        foreach (ITEMDATA item in database.items)
        {
            if (item == null) continue;

            switch (item.type)
            {
                case ITEMTYPE.Plant:
                    if (INVENTORY.IsOwned(item))
                    {
                        AddEntry(plantContainer, item, 0);
                        anyPlant = true;
                    }
                    break;

                case ITEMTYPE.Animal:
                    if (INVENTORY.IsOwned(item))
                    {
                        AddEntry(animalContainer, item, 0);
                        anyAnimal = true;
                    }
                    break;

                case ITEMTYPE.Seed:
                case ITEMTYPE.Consumable:
                    int n = INVENTORY.Count(item.itemName);
                    if (n > 0 && materialContainer != null)
                    {
                        AddEntry(materialContainer, item, n);
                        anyMaterial = true;
                    }
                    break;
            }
        }

        if (!anyPlant) AddEmpty(plantContainer);
        if (!anyAnimal) AddEmpty(animalContainer);
        if (!anyMaterial && materialContainer != null) AddEmpty(materialContainer);
    }

    // count가 0이면 이름만, 1 이상이면 "이름  x개수"
    void AddEntry(Transform parent, ITEMDATA item, int count)
    {
        GameObject go = Instantiate(entryPrefab, parent);
        go.SetActive(true);

        Text label = go.GetComponentInChildren<Text>();
        if (label != null) label.text = count > 0 ? item.itemName + "  x" + count : item.itemName;

        Transform iconTf = go.transform.Find("ICON");
        if (iconTf != null)
        {
            Image icon = iconTf.GetComponent<Image>();
            if (icon != null)
            {
                icon.enabled = item.icon != null;
                if (item.icon != null) icon.sprite = item.icon;
            }
        }
    }

    void AddEmpty(Transform parent)
    {
        GameObject go = Instantiate(entryPrefab, parent);
        go.SetActive(true);
        Text label = go.GetComponentInChildren<Text>();
        if (label != null) label.text = "없음";

        Transform iconTf = go.transform.Find("ICON");
        if (iconTf != null) iconTf.gameObject.SetActive(false);
    }

    void Clear(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform c in parent) Destroy(c.gameObject);
    }

    // ---- 테스트용 (Play 중 컴포넌트 ⋮ 메뉴) ----
    [ContextMenu("TEST 데이터베이스 전체 지급")]
    void TestGrantAll()
    {
        foreach (ITEMDATA it in database.items) INVENTORY.Data.itemNames.Add(it.itemName);
        INVENTORY.Save();
        Refresh();
    }

    [ContextMenu("TEST 클리어 1회 추가")]
    void TestClear()
    {
        var unlocked = INVENTORY.RegisterClear(database);
        Debug.Log("[소지품] 누적 클리어 " + INVENTORY.Data.clearCount + "회 / 새로 해금 " + unlocked.Count + "개");
        Refresh();
    }

    [ContextMenu("TEST 소지품 초기화")]
    void TestReset()
    {
        INVENTORY.ResetAll();
        Refresh();
    }
}