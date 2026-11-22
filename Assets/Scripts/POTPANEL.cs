using UnityEngine;
using UnityEngine.UI;

public class POTPANEL : MonoBehaviour
{
    public static POTPANEL Instance;

    [Header("패널")]
    public GameObject panel;
    public Text seedCountText;
    public Text fertilizerCountText;
    public Text messageText;
    public Toggle fertilizerToggle;
    public Button plantButton;
    public Button closeButton;

    [Header("사용할 아이템")]
    public ITEMDATA seedItem;         // 강낭콩 씨앗
    public ITEMDATA fertilizerItem;   // 비료

    POTSPOT current;

    public bool IsOpen { get { return panel != null && panel.activeSelf; } }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        panel.SetActive(false);
        plantButton.onClick.AddListener(OnPlant);
        closeButton.onClick.AddListener(Close);
    }

    void Update()
    {
        if (IsOpen && GARDENINPUT.CancelPressed) Close();
    }

    // 다 자랐는지: 심은 뒤 클리어 횟수가 필요 횟수 이상이고, 자라서 될 식물이 지정되어 있음
    public bool IsReady(POTSTATE state)
    {
        return state != null && state.planted
            && seedItem != null && seedItem.growsInto != null
            && INVENTORY.Data.clearCount - state.plantedAtClear >= seedItem.growAfterClears;
    }

    public void Open(POTSPOT pot)
    {
        if (IsOpen) return;
        current = pot;
        fertilizerToggle.SetIsOnWithoutNotify(false);

        // 다 자란 화분이면 열면서 바로 수확
        POTSTATE state = INVENTORY.GetPot(pot.potId);
        string message = IsReady(state) ? Harvest(state) : "";

        Refresh(message);
        panel.SetActive(true);
    }

    public void Close()
    {
        panel.SetActive(false);
        current = null;
    }

    string Harvest(POTSTATE state)
    {
        ITEMDATA plant = seedItem.growsInto;
        bool isNew = !INVENTORY.IsOwned(plant);

        INVENTORY.Harvest(state, plant);
        current.RefreshVisual();

        return isNew
            ? plant.itemName + "을(를) 얻었습니다!"
            : plant.itemName + " 수확 (이미 가진 식물이라 새로 얻는 것은 없습니다)";
    }

    void Refresh(string message)
    {
        if (current == null || seedItem == null || fertilizerItem == null)
        {
            Debug.LogWarning("[화분] Seed Item / Fertilizer Item이 연결되지 않았습니다.");
            return;
        }

        POTSTATE state = INVENTORY.GetPot(current.potId);
        int seeds = INVENTORY.Count(seedItem.itemName);
        int ferts = INVENTORY.Count(fertilizerItem.itemName);

        seedCountText.text = seedItem.itemName + "  x" + seeds;
        fertilizerCountText.text = fertilizerItem.itemName + "  x" + ferts;

        plantButton.interactable = !state.planted && seeds > 0;
        fertilizerToggle.interactable = !state.planted && ferts > 0;
        if (state.planted || ferts == 0) fertilizerToggle.SetIsOnWithoutNotify(false);

        if (message != "") messageText.text = message;
        else if (state.planted)
        {
            int need = seedItem.growAfterClears;
            int done = Mathf.Min(INVENTORY.Data.clearCount - state.plantedAtClear, need);
            messageText.text = "성장 중  " + done + " / " + need + " 클리어";
        }
        else if (seeds == 0) messageText.text = "씨앗이 없습니다";
        else messageText.text = "";
    }

    void OnPlant()
    {
        if (current == null) return;

        POTSTATE state = INVENTORY.GetPot(current.potId);
        if (state.planted) return;
        if (!INVENTORY.Consume(seedItem.itemName, 1))
        {
            Refresh("씨앗이 없습니다");
            return;
        }

        bool useFert = fertilizerToggle.isOn && INVENTORY.Consume(fertilizerItem.itemName, 1);

        state.planted = true;
        state.seedName = seedItem.itemName;
        state.plantedAtClear = INVENTORY.Data.clearCount;
        state.fertilized = useFert;
        INVENTORY.Save();

        current.RefreshVisual();
        Refresh(useFert ? "씨앗을 심었습니다 (비료 사용)" : "씨앗을 심었습니다");
    }
}