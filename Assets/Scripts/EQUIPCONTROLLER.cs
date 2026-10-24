using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class EQUIPCONTROLLER : MonoBehaviour
{
    [Header("패널")]
    public GameObject equipPanel;
    public GameObject listPopup;

    [Header("슬롯")]
    public ITEMSLOT[] plantSlots;
    public ITEMSLOT animalSlot;

    [Header("목록")]
    public ITEMDATABASE database;
    public Transform listContainer;
    public Button listItemPrefab;

    [Header("이동할 씬")]
    public string nextSceneName = "SampleScene";

    ITEMSLOT currentSlot;

    void Start()
    {
        equipPanel.SetActive(false);
        listPopup.SetActive(false);
    }

    public void OpenEquipPanel()
    {
        System.Array.Clear(GAMEDATA.Plants, 0, GAMEDATA.Plants.Length);
        GAMEDATA.Animal = null;
        foreach (var s in plantSlots) s.SetItem(null);
        animalSlot.SetItem(null);
        equipPanel.SetActive(true);
    }

    public void OpenList(ITEMSLOT slot)
    {
        currentSlot = slot;
        foreach (Transform c in listContainer) Destroy(c.gameObject);

        foreach (ITEMDATA item in database.GetByType(slot.type))
        {
            ITEMDATA it = item;
            Button b = Instantiate(listItemPrefab, listContainer);
            b.gameObject.SetActive(true);
            b.GetComponentInChildren<Text>().text = it.itemName;
            b.onClick.AddListener(() => SelectItem(it));
        }
        listPopup.SetActive(true);
    }

    void SelectItem(ITEMDATA item)
    {
        if (currentSlot.type == ITEMTYPE.Plant)
        {
            foreach (var p in GAMEDATA.Plants)
                if (p == item) return;   // 같은 식물 중복 방지
            GAMEDATA.Plants[currentSlot.index] = item;
        }
        else
        {
            GAMEDATA.Animal = item;
        }
        currentSlot.SetItem(item);
        listPopup.SetActive(false);
    }

    public void CloseList() { listPopup.SetActive(false); }
    public void BackToSubStage() { equipPanel.SetActive(false); }

    public void OnStart()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}