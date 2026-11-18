using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float deathY = -10f;
    [SerializeField] private bool applyFallDamage = false;

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
        {
            if (applyFallDamage && HPManager.Instance != null)
                HPManager.Instance.ApplyFallHazard(this);
            else
                Respawn();
        }
    }

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
            rb.velocity = Vector2.zero;
    }
}