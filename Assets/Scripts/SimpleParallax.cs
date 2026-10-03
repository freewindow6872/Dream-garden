using UnityEngine;

public class SimpleParallax : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField, Range(0f, 1f)] private float parallaxStrength = 0.3f;

    private Vector3 previousCameraPosition;

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            previousCameraPosition = cameraTransform.position;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        Vector3 delta = cameraTransform.position - previousCameraPosition;
        transform.position += new Vector3(
            delta.x * parallaxStrength,
            delta.y * parallaxStrength,
            0f
        );

        previousCameraPosition = cameraTransform.position;
    }
}
