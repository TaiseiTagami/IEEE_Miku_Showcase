using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class ViewerPOVFollow : MonoBehaviour
{
    [Tooltip("Transform to copy position from (the 'Viewer' object).")]
    public Transform viewer;

    [Tooltip("Transform to always look at (the 'centerpoint' object).")]
    public Transform centerpoint;

    [Tooltip("Use the viewer's up direction when aiming at centerpoint (otherwise world up).")]
    public bool useViewerUp = false;

    [Header("Auto-find by name (optional)")]
    public string viewerObjectName = "Viewer";
    public string centerpointObjectName = "centerpoint";

    void Awake()
    {
        AutoFindRefsIfNeeded();
    }

    void OnValidate()
    {
        AutoFindRefsIfNeeded();
    }

    private void AutoFindRefsIfNeeded()
    {
        if (viewer == null && !string.IsNullOrEmpty(viewerObjectName))
        {
            var go = GameObject.Find(viewerObjectName);
            if (go != null) viewer = go.transform;
        }

        if (centerpoint == null && !string.IsNullOrEmpty(centerpointObjectName))
        {
            var go = GameObject.Find(centerpointObjectName);
            if (go != null) centerpoint = go.transform;
        }
    }

    void LateUpdate()
    {
        if (viewer == null || centerpoint == null) return;

        // Match ViewerPOV camera's position to the Viewer
        transform.position = viewer.position;

        // Always look at centerpoint
        var up = useViewerUp ? viewer.up : Vector3.up;
        transform.LookAt(centerpoint.position, up);
    }
}