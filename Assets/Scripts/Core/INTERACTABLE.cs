using UnityEngine;

public abstract class INTERACTABLE : MonoBehaviour
{
    [Tooltip("화면 안내에 표시할 이름 (예: 화분, 스테이지 선택)")]
    public string promptName = "상호작용";

    // 상태에 따라 안내 문구를 바꾸고 싶으면 override
    public virtual string GetPrompt() { return promptName; }

    public abstract void Interact();
}