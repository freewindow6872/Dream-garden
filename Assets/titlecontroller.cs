using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleController : MonoBehaviour
{
    public void OnStartButton()
    {
        SceneManager.LoadScene("GAMETITLE");
    }

    public void OnQuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 플레이 모드 종료
#else
        Application.Quit(); // 빌드된 게임에서는 프로그램 종료
#endif
    }
}