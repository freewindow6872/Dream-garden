using UnityEngine;

public class STAGECLEARTRIGGER : MonoBehaviour
{
    bool cleared;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (cleared) return;
        if (!other.CompareTag("Player")) return;

        cleared = true;

        RESULTSCREEN result = FindObjectOfType<RESULTSCREEN>(true);
        if (result == null)
        {
            Debug.LogWarning("RESULTSCREEN이 씬에 없습니다.");
            return;
        }

        result.Show();
        Time.timeScale = 0f;      // 정산 화면 동안 게임 정지
    }
}