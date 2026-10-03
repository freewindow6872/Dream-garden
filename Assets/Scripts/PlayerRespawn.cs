using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float deathY = -10f;

    private Rigidbody2D rb;
    private bool hasCustomPosition;
    private Vector3 customPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (transform.position.y < deathY)
            Respawn();
    }

    /// <summary>StageLoader가 스테이지 시작 지점을 리스폰 위치로 지정할 때 사용</summary>
    public void SetRespawnPosition(Vector3 position)
    {
        hasCustomPosition = true;
        customPosition = position;
    }

    public void Respawn()
    {
        if (hasCustomPosition)
            transform.position = customPosition;
        else if (respawnPoint != null)
            transform.position = respawnPoint.position;
        else
            return;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }
}
