using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class HeadCoupledProjection
{
    // ------------- SETUP HELPERS -------------

    // Make sure we have a camera to render with.
    // If none assigned, we look for a child camera or create a new one.
    private void EnsureCamera()
    {
        if (hcpCamera == null)
        {
            // Try to find an existing child camera
            hcpCamera = GetComponentInChildren<Camera>();
            if (hcpCamera == null)
            {
                // No camera found—create one as a child
                GameObject camGO = new GameObject("HCP_Camera");
                camGO.transform.SetParent(transform, false); // parent it to the monitor for organization
                hcpCamera = camGO.AddComponent<Camera>();
            }
        }
    }

    // Make sure we have a preview quad to show the RenderTexture in the scene.
    // This is for in-editor visualization; it won't affect the projection math.
    private void EnsurePreviewQuad()
    {
        // If we're not using a RenderTexture for preview, do nothing
        if (!useRenderTexturePreview) return;

        // If the user manually assigned a Renderer, use it as the preview surface
        if (previewRenderer != null)
        {
            ConfigurePreviewLayerAndMaterial(previewRenderer);
            return;
        }

        // If auto creation is disabled, do nothing
        if (!autoCreatePreviewQuad) return;

        // Try to find an existing child named "PreviewQuad"
        Transform existing = transform.Find("PreviewQuad");
        if (existing != null)
        {
            // If found, get or add a renderer on it
            previewRenderer = existing.GetComponent<Renderer>();
            if (previewRenderer == null) previewRenderer = existing.gameObject.AddComponent<MeshRenderer>();
            ConfigurePreviewLayerAndMaterial(previewRenderer);
            return;
        }

        // Otherwise, create a new Quad primitive as our preview surface
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "PreviewQuad";
        quad.transform.SetParent(transform, false);           // child of the monitor (same transform space)
        quad.transform.localPosition = Vector3.zero;          // centered on the monitor
        quad.transform.localRotation = Quaternion.identity;   // aligned with monitor's plane
        quad.transform.localScale = new Vector3(
            monitor != null ? monitor.width : 1f,             // width matches the monitor setup
            monitor != null ? monitor.height : 1f,            // height matches the monitor setup
            1f
        );

        // Remove the collider that Unity adds by default to primitive meshes
        Collider col = quad.GetComponent<Collider>();
        if (col != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(col); // in editor, remove immediately
#else
            Destroy(col);          // at runtime, schedule for removal
#endif
        }

        // Cache its renderer so we can assign a material and texture later
        previewRenderer = quad.GetComponent<MeshRenderer>();

        // Configure its layer and material (including auto-created Unlit/Texture if enabled)
        ConfigurePreviewLayerAndMaterial(previewRenderer);
    }

    // Configure the preview renderer's layer and material:
    // - Assign a named layer if it exists (so we can exclude it from the camera culling).
    // - Optionally auto-create an Unlit/Texture material.
    // - Disable unnecessary rendering features (shadows, motion vectors).
    private void ConfigurePreviewLayerAndMaterial(Renderer rend)
    {
        if (rend == null) return;

        // Assign the preview layer if it exists
        int layer = LayerMask.NameToLayer(previewLayerName);
        if (layer == -1)
        {
            // Layer not found. You can create it in Project Settings > Tags and Layers.
#if UNITY_EDITOR
            Debug.LogWarning($"Preview layer '{previewLayerName}' does not exist. " +
                             "Create it in Project Settings > Tags and Layers if you want culling to exclude the preview quad.");
#endif
        }
        else
        {
            rend.gameObject.layer = layer;

            // Optionally exclude the preview layer from this camera's culling mask
            if (excludePreviewLayerFromCamera && hcpCamera != null)
            {
                int mask = hcpCamera.cullingMask;
                mask &= ~(1 << layer);         // clear that layer's bit
                hcpCamera.cullingMask = mask;  // apply the new mask
            }
        }

        // Create a simple Unlit/Texture material if requested.
        // This shader shows a texture exactly (no lighting), perfect for screen previews.
        if (autoCreatePreviewMaterial)
        {
            var unlitTex = Shader.Find("Unlit/Texture");
            if (unlitTex != null)
            {
                var mat = new Material(unlitTex);
                mat.name = "MonitorPreviewMat (Auto)";
                rend.sharedMaterial = mat;
            }
            else
            {
#if UNITY_EDITOR
                Debug.LogWarning("Shader 'Unlit/Texture' not found. " +
                                 "Assign a material manually to the PreviewQuad.");
#endif
            }
        }

        // Turn off shadows and motion vectors for the preview quad—it’s just a flat screen.
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    // Configure where the camera sends its image:
    // - If outputToDisplay is true, send it to a hardware display.
    // - Else if useRenderTexturePreview is true, render to a RenderTexture and assign it to the previewRenderer.
    private void SetupOutput()
    {
        if (hcpCamera == null) return;

        if (outputToDisplay)
        {
            // Render straight to a hardware display (Display 0/1/2...)
            hcpCamera.targetTexture = null; // no RenderTexture
            hcpCamera.targetDisplay = Mathf.Clamp(targetDisplay, 0, Display.displays.Length - 1);

#if UNITY_2020_3_OR_NEWER
            // Try to activate that display at runtime (if using multi-display)
            if (Application.isPlaying && hcpCamera.targetDisplay < Display.displays.Length)
            {
                try { Display.displays[hcpCamera.targetDisplay].Activate(); } catch { /* ignore */ }
            }
#endif
        }
        else if (useRenderTexturePreview)
        {
            // Render to a RenderTexture, then show it on the preview quad
            AllocateRTIfNeeded();
            hcpCamera.targetTexture = rt;

            // Assign the RT to the material on the renderer (so it displays the camera image)
            if (previewRenderer != null)
            {
                var mat = previewRenderer.sharedMaterial;
                if (mat != null)
                {
                    mat.mainTexture = rt;
                }
            }
        }
        else
        {
            // Not using a RenderTexture and not outputting to a hardware display.
            // The camera will still compute the projection correctly for consistency.
            hcpCamera.targetTexture = null;
            hcpCamera.targetDisplay = 0;
        }
    }

    // Create or resize the RenderTexture to match the requested resolution
    private void AllocateRTIfNeeded()
    {
        // If we already have an RT with the same size, reuse it
        if (rt != null)
        {
            if (rt.width == renderResolution.x && rt.height == renderResolution.y) return;
            ReleaseRT(); // size mismatch, release old RT
        }

        // Create a new RT
        rt = new RenderTexture(renderResolution.x, renderResolution.y, 24, RenderTextureFormat.ARGB32);
        rt.name = $"HCP_RT_{gameObject.name}";
        rt.Create();
    }

    // Release the RT when disabling or resizing to free memory
    private void ReleaseRT()
    {
        if (rt != null)
        {
            // If our preview material is currently using the RT, clear it to avoid dangling references
            if (previewRenderer != null && previewRenderer.sharedMaterial != null && previewRenderer.sharedMaterial.mainTexture == rt)
            {
                previewRenderer.sharedMaterial.mainTexture = null;
            }

            rt.Release();

#if UNITY_EDITOR
            DestroyImmediate(rt); // in editor, destroy immediately
#else
            Destroy(rt);          // at runtime, schedule destroy
#endif
            rt = null;
        }
    }

    // Apply basic camera settings (clear flags, culling mask, clip planes, etc.)
    private void UpdateCameraStaticSettings()
    {
        if (hcpCamera == null) return;

        hcpCamera.clearFlags = clearFlags;
        hcpCamera.backgroundColor = backgroundColor;
        hcpCamera.cullingMask = cullingMask;
        hcpCamera.usePhysicalProperties = false; // we'll set custom matrices manually
        hcpCamera.nearClipPlane = nearClip;
        hcpCamera.farClipPlane = farClip;
    }

    // Keep the preview quad scaled and aligned to the monitor size and orientation.
    // This ensures the quad matches the physical dimensions defined in MonitorSetUp.
    private void MatchQuadToMonitorSize()
    {
        if (!autoMatchQuadToMonitorSize || previewRenderer == null || monitor == null) return;

        var tf = previewRenderer.transform;

        // Scale X and Y to match monitor width/height, keep Z at 1 (unused for a flat quad)
        tf.localScale = new Vector3(
            Mathf.Max(0.0001f, monitor.width),
            Mathf.Max(0.0001f, monitor.height),
            1f
        );

        // Keep it centered and aligned with the monitor transform
        tf.localPosition = Vector3.zero;
        tf.localRotation = Quaternion.identity;
    }
}