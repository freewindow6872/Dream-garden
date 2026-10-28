using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RESULTSCREEN : MonoBehaviour
{
    [Header("패널")]
    public GameObject panel;

    [Header("텍스트")]
    public Text playTimeText;
    public Text gearText;
    public Text itemText;
    public Text plantText;    
    public Text animalText;   

    [Header("아이콘 (선택, 비워도 됨)")]
    public Image[] plantIcons;  // 3개
    public Image animalIcon;    // 1개

    [Header("확인 버튼")]
    public Button confirmButton;

    [Header("이동할 씬")]
    public string gardenSceneName = "GARDEN";   

    bool applied;

    void Start()
    {
        RUNTRACKER.StartRun();
        panel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);  
    }

    // 클리어 시 호출
    public void Show()
    {
        RUNTRACKER.EndRun();

        playTimeText.text = "플레이 타임  " + RUNTRACKER.FormatTime();
        gearText.text = "획득한 톱니바퀴  " + RUNTRACKER.Gears;
        itemText.text = "획득한 아이템\n" + BuildItemList();

        ShowAbilities();
        panel.SetActive(true);
    }

    void ShowAbilities()
    {
        // 식물 3개
        string plants = "";
        for (int i = 0; i < GAMEDATA.Plants.Length; i++)
        {
            ITEMDATA p = GAMEDATA.Plants[i];
            if (p != null) plants += p.itemName + "  ";
            SetIcon(plantIcons, i, p);
        }
        plantText.text = "사용한 식물 능력  " + (plants == "" ? "없음" : plants);

        // 동물 1개
        ITEMDATA a = GAMEDATA.Animal;
        animalText.text = "사용한 동물 능력  " + (a != null ? a.itemName : "없음");
        if (animalIcon != null)
        {
            animalIcon.enabled = a != null && a.icon != null;
            if (animalIcon.enabled) animalIcon.sprite = a.icon;
        }
    }

    void SetIcon(Image[] icons, int index, ITEMDATA item)
    {
        if (icons == null || index >= icons.Length || icons[index] == null) return;
        icons[index].enabled = item != null && item.icon != null;
        if (icons[index].enabled) icons[index].sprite = item.icon;
    }

    string BuildItemList()
    {
        if (RUNTRACKER.Items.Count == 0) return "없음";

        var counts = new Dictionary<string, int>();
        foreach (ITEMDATA it in RUNTRACKER.Items)
        {
            if (counts.ContainsKey(it.itemName)) counts[it.itemName]++;
            else counts[it.itemName] = 1;
        }

        string result = "";
        foreach (var pair in counts)
            result += pair.Key + " x" + pair.Value + "\n";
        return result;
    }

    public void OnConfirm()
    {
        if (applied) return;       // 중복 클릭 방지
        applied = true;

        INVENTORY.ApplyRun();      // 톱니바퀴/아이템을 소지품에 저장
        Time.timeScale = 1f;       // 클리어 때 멈춘 시간 복구
        SceneManager.LoadScene(gardenSceneName);
    }
}