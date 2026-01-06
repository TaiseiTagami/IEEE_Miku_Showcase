using UnityEngine;

public class WebcamCenterFromAngles : MonoBehaviour
{
    [Header("Network Source")]
    [SerializeField] private UdpPoseProvider poseProvider;

    [Header("Webcam Child")]
    [SerializeField] private Transform webcam;           // The "Webcam" object
    [SerializeField] private float forwardOffsetMeters = 0.05f; // 5 cm

    [Header("Angle Settings")]
    [SerializeField] private float yawScale = 1.0f;      // scale/units for yaw
    [SerializeField] private float pitchScale = 1.0f;    // scale/units for pitch
    [SerializeField] private float smoothing = 0.3f;     // per-frame Slerp factor (0..1)
    [SerializeField] private bool invertYaw = false;
    [SerializeField] private bool invertPitch = false;

    private Quaternion baseRotation; // captured at Start

    void Reset()
    {
        poseProvider = FindObjectOfType<UdpPoseProvider>();
    }

    void Start()
    {
        if (poseProvider == null)
        {
            Debug.LogError("WebcamCenterFromAngles: UdpPoseProvider reference is missing!");
            enabled = false;
            return;
        }

        if (webcam == null)
        {
            Debug.LogError("WebcamCenterFromAngles: Webcam transform reference is missing!");
            enabled = false;
            return;
        }

        // Capture the initial rotation you set in the editor
        baseRotation = transform.rotation;
    }

    void Update()
    {
        if (!poseProvider.HasAngles) 
        {
            // Keep webcam offset even if no angles yet
            MaintainWebcamOffset();
            return;
        }

        Vector2 angles = poseProvider.GetLatestAngles(); // yaw (x), pitch (y)
        float yaw = angles.x * yawScale * (invertYaw ? -1f : 1f);
        float pitch = angles.y * pitchScale * (invertPitch ? -1f : 1f);

        // Build rotation relative to the original facing direction (baseRotation).
        // Yaw: about base up-axis, Pitch: about base right-axis.
        Vector3 yawAxis   = baseRotation * Vector3.up;
        Vector3 pitchAxis = baseRotation * Vector3.right;

        Quaternion targetRotation =
            baseRotation *
            Quaternion.AngleAxis(yaw,   yawAxis) *
            Quaternion.AngleAxis(pitch, pitchAxis);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Mathf.Clamp01(smoothing)
        );

        MaintainWebcamOffset();
    }

    private void MaintainWebcamOffset()
    {
        if (webcam == null) return;
        webcam.position = transform.position + transform.forward * forwardOffsetMeters;
        webcam.rotation = transform.rotation;
    }

    // Optional: call this from a button or context menu to re-capture the current rotation as the new base
    public void RecalibrateBaseRotation()
    {
        baseRotation = transform.rotation;
    }
}