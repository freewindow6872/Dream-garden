using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField] private float smoothTime = 0.2f;

    [Header("Optional Limits")]
    [SerializeField] private bool useLimits = false;
    [SerializeField] private Vector2 minPosition;
    [SerializeField] private Vector2 maxPosition;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        desired.z = offset.z;

        Vector3 smoothed = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref velocity,
            smoothTime
        );

        if (useLimits)
        {
            smoothed.x = Mathf.Clamp(smoothed.x, minPosition.x, maxPosition.x);
            smoothed.y = Mathf.Clamp(smoothed.y, minPosition.y, maxPosition.y);
        }

        transform.position = smoothed;
    }
}
