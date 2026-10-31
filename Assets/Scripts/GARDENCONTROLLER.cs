using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GARDENCONTROLLER : MonoBehaviour
{
    [Header("버튼")]
    public Button stageButton;      // 스테이지 선택으로 가는 버튼

    [Header("이동할 씬")]
    public string stageSelectSceneName = "STAGESELECT";
    public string titleSceneName = "title";
    public Button inventoryButton;
    void Start()
    {
        if (inventoryButton != null)
            inventoryButton.onClick.AddListener(() =>
            {
                if (INVENTORYUI.Instance != null) INVENTORYUI.Instance.Toggle();
            });
        Time.timeScale = 1f;   // 혹시 멈춘 채로 넘어온 경우 복구

        if (stageButton != null)
            stageButton.onClick.AddListener(() => SceneManager.LoadScene(stageSelectSceneName));
    }
}