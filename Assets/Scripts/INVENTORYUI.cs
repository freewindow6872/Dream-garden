using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class INVENTORYUI : MonoBehaviour
{
    [Header("버튼 / 패널")]
    public Button openButton;
    public Button closeButton;
    public GameObject panel;

    [Header("표시")]
    public Text gearText;
    public Transform plantContainer;
    public Transform animalContainer;
    public GameObject entryPrefab;

    [Header("아이템 정보")]
    public ITEMDATABASE database;

    void Start()
    {
        panel.SetActive(false);
        openButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        Refresh();
        panel.SetActive(true);
    }

    public void Close()
    {
        panel.SetActive(false);
    }

    void Refresh()
    {
        gearText.text = "톱니바퀴  " + INVENTORY.Data.gears;

        Clear(plantContainer);
        Clear(animalContainer);

        var counts = new Dictionary<string, int>();
        foreach (string name in INVENTORY.Data.itemNames)
        {
            if (counts.ContainsKey(name)) counts[name]++;
            else counts[name] = 1;
        }

        bool anyPlant = false, anyAnimal = false;

        foreach (var pair in counts)
        {
            ITEMDATA item = database.items.Find(i => i.itemName == pair.Key);
            if (item == null) continue;

            if (item.type == ITEMTYPE.Plant)
            {
                AddEntry(plantContainer, item, pair.Value);
                anyPlant = true;
            }
            else if (item.type == ITEMTYPE.Animal)
            {
                AddEntry(animalContainer, item, pair.Value);
                anyAnimal = true;
            }
        }

        if (!anyPlant) AddEmpty(plantContainer);
        if (!anyAnimal) AddEmpty(animalContainer);
    }

    void AddEntry(Transform parent, ITEMDATA item, int count)
    {
        GameObject go = Instantiate(entryPrefab, parent);
        go.SetActive(true);

        Text label = go.GetComponentInChildren<Text>();
        if (label != null) label.text = item.itemName + "  x" + count;

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
        foreach (Transform c in parent) Destroy(c.gameObject);
    }
}