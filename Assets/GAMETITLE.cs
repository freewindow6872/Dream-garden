using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GAMETITLE : MonoBehaviour
{
    public Button continueButton;

public Button continueButton2;
    public void OnNewGame()
    {
        SceneManager.LoadScene("STAGESELECT");
    }

    public void OnContinue()
    {
        // TODO: 세이브 데이터 구현 후 작성
    }
}