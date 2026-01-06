using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// This script makes a monitor act like a "window" into your 3D scene,
// updating the view based on the viewer's tracked head/eye position.
// It does this by computing a custom off-axis perspective projection
// so that the screen plane becomes the projection surface.
//
// Attach this to a monitor GameObject that already has MonitorSetUp.
// Set personEye to your PersonCube transform.
//
// It can also auto-create a child "PreviewQuad" with a material to display
// the camera's RenderTexture, making it easy to preview in the editor.

[ExecuteAlways]                        // Run in Edit Mode as well as Play Mode (so you can see gizmos/previews live)
[RequireComponent(typeof(MonitorSetUp))] // Ensure a MonitorSetUp component exists on the same object
public partial class HeadCoupledProjection : MonoBehaviour
{
    // ------------- USER INPUTS (editable in Inspector) -------------

    [Header("Tracking")]
    [Tooltip("Transform representing the user's eye position (PersonCube).\n" +
             "This should be a Transform that follows the user's head/eye in world space.")]
    public Transform personEye;

    [Header("Camera Settings")]
    [Tooltip("Optional: Provide your own camera. If left empty, one will be created as a child.\n" +
             "This camera will render the view seen through the monitor from the viewer's eye.")]
    public Camera hcpCamera;

    [Min(0.001f)]
    [Tooltip("Near clip distance for the camera (how close objects can be before being clipped).")]
    public float nearClip = 0.01f;

    [Min(0.1f)]
    [Tooltip("Far clip distance for the camera (how far the camera can see).")]
    public float farClip = 1000f;

    [Tooltip("Which layers the camera should render.\n" +
             "You can exclude certain layers (like the preview quad) to avoid recursion.")]
    public LayerMask cullingMask = ~0; // ~0 means "all layers"; Will have to update this to exclude preview layer

    [Tooltip("How the camera clears the screen each frame.\n" +
             "Skybox is fine; Solid Color is also okay for a simple background.")]
    public CameraClearFlags clearFlags = CameraClearFlags.Skybox;

    [Tooltip("Background color if Clear Flags is Solid Color.")]
    public Color backgroundColor = Color.black;

    [Header("Preview Quad (auto)")]
    [Tooltip("Automatically create a PreviewQuad child to display the RenderTexture.\n" +
             "This is helpful for in-editor visualization.")]
    public bool autoCreatePreviewQuad = true;

    [Tooltip("Automatically create an Unlit/Texture material for the PreviewQuad.\n" +
             "Unlit/Texture shows a texture exactly with no lighting effects.")]
    public bool autoCreatePreviewMaterial = true;

    [Tooltip("Keep the PreviewQuad's scale matched to MonitorSetUp width/height.\n" +
             "So changing monitor dimensions adjusts the quad automatically.")]
    public bool autoMatchQuadToMonitorSize = true;

    [Tooltip("Layer name to assign to the PreviewQuad (e.g., 'Screen Surface').\n" +
             "If this layer exists, we can exclude it from the HCP camera to avoid rendering the quad itself.")]
    public string previewLayerName = "Screen Surface";

    [Tooltip("Exclude the preview layer from the HCP camera culling mask to prevent recursion.\n" +
             "This means the camera won't render the preview quad in its own image.")]
    public bool excludePreviewLayerFromCamera = true;

    [Header("Output")]
    [Tooltip("Send camera output directly to a Unity Display (e.g., actual monitor)?\n" +
             "If true, camera renders to targetDisplay instead of a RenderTexture.")]
    public bool outputToDisplay = false;

    [Tooltip("Target Display index (0 = Game view / primary display).\n" +
             "Use >0 if you have multiple displays connected and activated.")]
    public int targetDisplay = 0;

    [Tooltip("Use a RenderTexture for preview instead of a real display output.\n" +
             "This is great for in-editor visualization on the PreviewQuad.")]
    public bool useRenderTexturePreview = true;

    [Tooltip("If assigned, this renderer will receive the RenderTexture.\n" +
             "If left empty and autoCreatePreviewQuad is true, we'll create a child quad automatically.")]
    public Renderer previewRenderer;

    [Tooltip("Desired resolution for the camera's RenderTexture.\n" +
             "Higher values look sharper but cost more performance.")]
    public Vector2Int renderResolution = new Vector2Int(1920, 1080);

    [Header("Debug")]
    [Tooltip("Draw gizmos (lines from eye to screen corners) to visualize the frustum.\n" +
             "Only visible in the editor.")]
    public bool drawGizmos = true;

    [Tooltip("Color of the gizmos that show the frustum lines in the Scene view.")]
    public Color frustumColor = new Color(1f, 0.5f, 0f, 0.75f);

    // ------------- PRIVATE FIELDS -------------

    private MonitorSetUp monitor;  // Reference to the MonitorSetUp (gives us width/height and transform info)
    private RenderTexture rt;      // RenderTexture that the camera renders into (for preview)

    // ------------- UNITY LIFECYCLE -------------

    // Called when the component is first added or "Reset" is hit
    void Reset()
    {
        // Set some sensible defaults
        nearClip = 0.01f;
        farClip = 1000f;
        renderResolution = new Vector2Int(1920, 1080);
        outputToDisplay = false;
        useRenderTexturePreview = true;

        autoCreatePreviewQuad = true;
        autoCreatePreviewMaterial = true;
        autoMatchQuadToMonitorSize = true;
        previewLayerName = "Screen Surface";
        excludePreviewLayerFromCamera = true;
    }

    // Called when the object becomes enabled and active
    void OnEnable()
    {
        // Cache the MonitorSetUp so we can read width/height and transform
        monitor = GetComponent<MonitorSetUp>();

        // Ensure we have a camera to render from the viewer's eye
        EnsureCamera();

        // Automatically create/find and configure a preview quad if desired
        EnsurePreviewQuad();

        // Set up output routing (to RenderTexture or to a display)
        SetupOutput();

        // Apply static (non-changing) camera settings like clear flags and culling mask
        UpdateCameraStaticSettings();

        // Do an initial update of the projection matrices
        UpdateProjection();
    }

    // Called when the object becomes disabled or inactive
    void OnDisable()
    {
        // Release the RenderTexture to avoid memory leaks
        ReleaseRT();
    }

    // Called when script properties change in the inspector (including in Edit Mode)
    void OnValidate()
    {
        // Keep values in a safe range to avoid invalid projections or crashes
        if (renderResolution.x < 16) renderResolution.x = 16;
        if (renderResolution.y < 16) renderResolution.y = 16;
        if (nearClip < 0.0001f) nearClip = 0.0001f;
        if (farClip <= nearClip + 0.001f) farClip = nearClip + 0.001f;

        // In Edit Mode, keep everything up to date so you can see changes immediately
        if (!Application.isPlaying)
        {
            EnsureCamera();
            EnsurePreviewQuad();
            SetupOutput();
            UpdateCameraStaticSettings();
            UpdateProjection();
            MatchQuadToMonitorSize(); // keep quad scale synced to monitor size in the editor
        }
    }

    // Called every frame, after all Updates (good for camera updates)
    void LateUpdate()
    {
        // Recompute the projection each frame using the latest personEye position
        UpdateProjection();

        // Keep the preview quad matched to monitor size if desired
        MatchQuadToMonitorSize();
    }
}