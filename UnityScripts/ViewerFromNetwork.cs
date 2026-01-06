using UnityEngine;

public class ViewerFromNetwork : MonoBehaviour
{
    [Header("Network Source")]
    [SerializeField] private UdpPoseProvider poseProvider;

    [Header("Tracking Settings")]
    [SerializeField] private float smoothing = 0.3f;   // 0..1 per-frame lerp factor
    [SerializeField] private float positionScale = 1.0f;

    [Header("Camera Setup")]
    [SerializeField] private Transform virtualCamera; // Real camera reference
    [SerializeField] private float cameraFOV_X = 90f; // Match Python fallback FOVX
    [SerializeField] private float cameraFOV_Y = 45f; // Match Python fallback FOVY

    private bool hasReceivedFirstPosition = false;

    void Reset()
    {
        // Try to auto-assign provider
        poseProvider = FindObjectOfType<UdpPoseProvider>();
    }

    void Start()
    {
        if (poseProvider == null)
        {
            Debug.LogError("ViewerFromNetwork: UdpPoseProvider reference is missing!");
            enabled = false;
            return;
        }

        if (virtualCamera == null)
        {
            Debug.LogError("ViewerFromNetwork: virtualCamera reference is missing!");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        if (!poseProvider.HasPosition) return;

        // Get latest position in camera space
        Vector3 camSpacePos = poseProvider.GetLatestPosition();

        // Scale to desired units
        Vector3 scaledCamSpacePos = camSpacePos * positionScale;

        // Convert camera-space to world-space (using the virtualCamera transform)
        Vector3 worldSpaceTarget = virtualCamera.TransformPoint(scaledCamSpacePos);

        // Apply smoothing in world space
        transform.position = Vector3.Lerp(
            transform.position,
            worldSpaceTarget,
            Mathf.Clamp01(smoothing)
        );

        hasReceivedFirstPosition = true;
    }

    void OnDrawGizmos()
    {
        if (virtualCamera != null)
        {
            Gizmos.color = Color.yellow;
            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(virtualCamera.position, virtualCamera.rotation, Vector3.one);

            float near = 0.1f;
            float far = 2f;

            float nearHeight = 2f * near * Mathf.Tan(cameraFOV_Y * 0.5f * Mathf.Deg2Rad);
            float nearWidth = 2f * near * Mathf.Tan(cameraFOV_X * 0.5f * Mathf.Deg2Rad);

            float farHeight = 2f * far * Mathf.Tan(cameraFOV_Y * 0.5f * Mathf.Deg2Rad);
            float farWidth = 2f * far * Mathf.Tan(cameraFOV_X * 0.5f * Mathf.Deg2Rad);

            // Near plane
            Gizmos.DrawLine(new Vector3(-nearWidth / 2, -nearHeight / 2, near), new Vector3(nearWidth / 2, -nearHeight / 2, near));
            Gizmos.DrawLine(new Vector3(-nearWidth / 2, nearHeight / 2, near), new Vector3(nearWidth / 2, nearHeight / 2, near));
            Gizmos.DrawLine(new Vector3(-nearWidth / 2, -nearHeight / 2, near), new Vector3(-nearWidth / 2, nearHeight / 2, near));
            Gizmos.DrawLine(new Vector3(nearWidth / 2, -nearHeight / 2, near), new Vector3(nearWidth / 2, nearHeight / 2, near));

            // Far plane
            Gizmos.DrawLine(new Vector3(-farWidth / 2, -farHeight / 2, far), new Vector3(farWidth / 2, -farHeight / 2, far));
            Gizmos.DrawLine(new Vector3(-farWidth / 2, farHeight / 2, far), new Vector3(farWidth / 2, farHeight / 2, far));
            Gizmos.DrawLine(new Vector3(-farWidth / 2, -farHeight / 2, far), new Vector3(-farWidth / 2, farHeight / 2, far));
            Gizmos.DrawLine(new Vector3(farWidth / 2, -farHeight / 2, far), new Vector3(farWidth / 2, -farHeight / 2, far));

            // Connecting lines
            Gizmos.DrawLine(new Vector3(-nearWidth / 2, -nearHeight / 2, near), new Vector3(-farWidth / 2, -farHeight / 2, far));
            Gizmos.DrawLine(new Vector3(nearWidth / 2, -nearHeight / 2, near), new Vector3(farWidth / 2, -farHeight / 2, far));
            Gizmos.DrawLine(new Vector3(-nearWidth / 2, nearHeight / 2, near), new Vector3(-farWidth / 2, farHeight / 2, far));
            Gizmos.DrawLine(new Vector3(nearWidth / 2, nearHeight / 2, near), new Vector3(farWidth / 2, farHeight / 2, far));

            Gizmos.matrix = originalMatrix;
        }
    }
}