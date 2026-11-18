using UnityEngine;

public class STAGECLEARTRIGGER : MonoBehaviour
{
    private bool cleared;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (cleared) return;
        if (!other.CompareTag("Player")) return;

        cleared = true;
        UIManager.Instance.ShowStageResult();
    }
}