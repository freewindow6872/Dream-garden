using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class STAGESELECT : MonoBehaviour
{
    [Header("패널")]
    public GameObject stagePanel;      // 스테이지 목록 패널
    public GameObject subStagePanel;   // 1-1 ~ 1-8 패널

    [Header("세부 스테이지 생성")]
    public Transform subStageContainer; // Grid Layout Group이 붙은 오브젝트
    public Button subStageButtonPrefab; // 버튼 프리팹 (자식에 Text)
    public int subStageCount = 8;

    [Header("이동할 씬")]
    public string nextSceneName = "SampleScene";

    int currentStage = 1;

    void Start()
    {
        ShowStagePanel();
    }

    public void OnSelectStage(int stage)
    {
        currentStage = stage;
        BuildSubStageButtons();
        stagePanel.SetActive(false);
        subStagePanel.SetActive(true);
    }


    public void ShowStagePanel()
    {
        stagePanel.SetActive(true);
        subStagePanel.SetActive(false);
    }

    void BuildSubStageButtons()
    {
        // 기존 버튼 정리
        foreach (Transform child in subStageContainer)
            Destroy(child.gameObject);

        for (int i = 1; i <= subStageCount; i++)
        {
            int sub = i; // 클로저 문제 방지용 복사
            Button btn = Instantiate(subStageButtonPrefab, subStageContainer);
            btn.GetComponentInChildren<Text>().text = currentStage + "-" + sub;
            btn.onClick.AddListener(() => OnSelectSubStage(sub));
        }
    }
    public EQUIPCONTROLLER equip;

    void OnSelectSubStage(int sub)
    {
        GAMEDATA.Select(currentStage, sub);
        equip.OpenEquipPanel();
    }
}