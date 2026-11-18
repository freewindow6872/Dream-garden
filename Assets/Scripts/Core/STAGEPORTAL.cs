using UnityEngine;
using UnityEngine.SceneManagement;

public class STAGEPORTAL : INTERACTABLE
{
    public string sceneName = "STAGESELECT";

    void Reset() { promptName = "스테이지 선택"; }

    public override void Interact()
    {
        SceneManager.LoadScene(sceneName);
    }
}