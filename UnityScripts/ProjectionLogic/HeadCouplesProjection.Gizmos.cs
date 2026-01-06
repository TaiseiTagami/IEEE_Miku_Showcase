using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class HeadCoupledProjection
{
#if UNITY_EDITOR
    // Draw helpful gizmos in the Scene view:
    // Lines from the eye position to each screen corner, so you can visualize the frustum rays.
    void OnDrawGizmos()
    {
        if (!drawGizmos || hcpCamera == null || personEye == null || monitor == null) return;

        Gizmos.color = frustumColor;

        Transform t = monitor.transform;
        Vector3 right = t.right.normalized;
        Vector3 up = t.up.normalized;

        float halfW = monitor.width * 0.5f;
        float halfH = monitor.height * 0.5f;
        Vector3 center = t.position;

        // Compute all 4 corners for drawing lines:
        Vector3 c0 = center + (-right * halfW) + (-up * halfH); // bottom-left
        Vector3 c1 = center + ( right * halfW) + (-up * halfH); // bottom-right
        Vector3 c2 = center + ( right * halfW) + ( up * halfH); // top-right
        Vector3 c3 = center + (-right * halfW) + ( up * halfH); // top-left

        Vector3 eye = personEye.position;

        // Draw rays from eye to each corner
        Gizmos.DrawLine(eye, c0);
        Gizmos.DrawLine(eye, c1);
        Gizmos.DrawLine(eye, c2);
        Gizmos.DrawLine(eye, c3);
    }
#endif
}