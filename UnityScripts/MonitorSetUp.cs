using UnityEngine;

[ExecuteAlways]
public class MonitorSetUp : MonoBehaviour
{
    [Header("Rectangle")]
    [Min(0f)] public float width = 1f;
    [Min(0f)] public float height = 1f;
    [Header("Gizmo Appearance")]
    public Color gizmoColor = new Color(0f, 0.5f, 1f, 1f);
    public Color selectedColor = new Color(1f, 0.8f, 0f, 1f);
    [Tooltip("Length of the short line pointing in the forward direction (perpendicular to rectangle plane)")]
    public float forwardLineLength = 0.25f;
    [Tooltip("Should the rectangle be filled (editor-only) or just an outline?")]
    public bool filled = false;

    void OnDrawGizmos()
    {
        DrawRectangleGizmo(gizmoColor);
    }

    void OnDrawGizmosSelected()
    {
        DrawRectangleGizmo(selectedColor);
    }

    void DrawRectangleGizmo(Color col)
    {
        Gizmos.color = col;

        // Rectangle lies on local X (right) and local Y (up) plane. The "forward" is local Z.
        Vector3 right = transform.right.normalized;
        Vector3 up = transform.up.normalized;
        Vector3 forward = transform.forward.normalized;

        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

        Vector3 center = transform.position;

        // Four corners in world space
        Vector3 c0 = center + (-right * halfW) + (-up * halfH); // bottom-left
        Vector3 c1 = center + ( right * halfW) + (-up * halfH); // bottom-right
        Vector3 c2 = center + ( right * halfW) + ( up * halfH); // top-right
        Vector3 c3 = center + (-right * halfW) + ( up * halfH); // top-left

        if (filled)
        {
            // Draw filled rectangle by drawing two triangles using Gizmos.DrawMesh
            Mesh m = new Mesh();
            m.vertices = new Vector3[] { transform.InverseTransformPoint(c0), transform.InverseTransformPoint(c1), transform.InverseTransformPoint(c2), transform.InverseTransformPoint(c3) };
            m.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawMesh(m);
            Gizmos.matrix = Matrix4x4.identity;
            Object.DestroyImmediate(m);
        }
        else
        {
            Gizmos.DrawLine(c0, c1);
            Gizmos.DrawLine(c1, c2);
            Gizmos.DrawLine(c2, c3);
            Gizmos.DrawLine(c3, c0);
        }

        // Draw a short line from center pointing along forward (perpendicular to rectangle plane)
        Vector3 lineEnd = center + forward * forwardLineLength;
        Gizmos.DrawLine(center, lineEnd);
    }

}
