using UnityEngine;

public class CAMERAFOLLOW : MonoBehaviour
{
    public Transform target;
    public Vector2 offset;
    public float smooth = 8f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 goal = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }
}   