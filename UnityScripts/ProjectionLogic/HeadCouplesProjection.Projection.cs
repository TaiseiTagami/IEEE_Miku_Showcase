using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class HeadCoupledProjection
{
    // ------------- CORE PROJECTION MATH -------------

    // This is the heart of head-coupled perspective (HCP).
    // We compute a custom projection so that the screen plane acts as the projection plane,
    // with the camera located at the viewer's eye (personEye).
    //
    // Steps:
    // 1) Get the 3D positions of the screen corners (based on the monitor's transform and width/height).
    // 2) From the eye position, build vectors to the corners.
    // 3) Compute a basis for the screen: right (vr), up (vu), normal (vn).
    // 4) Ensure the normal faces the eye (so the math isn't inverted).
    // 5) Compute distances needed for the off-axis frustum at the near plane.
    // 6) Build the projection matrix and the view (world-to-camera) matrix.
    private void UpdateProjection()
    {
        // We need these references to do anything
        if (monitor == null || personEye == null || hcpCamera == null) return;

        // Get the monitor's orientation in the world:
        // - right = local X axis in world space
        // - up = local Y axis in world space
        // - forward = local Z axis in world space (normal pointing "out" of the screen)
        Transform t = monitor.transform;
        Vector3 right = t.right.normalized;
        Vector3 up = t.up.normalized;
        Vector3 forward = t.forward.normalized;

        // Half-width and half-height (so we can compute corners around the center)
        float halfW = monitor.width * 0.5f;
        float halfH = monitor.height * 0.5f;

        // Screen center in world space
        Vector3 center = t.position;

        // Compute 3 of the screen corners in world space:
        // pa = lower-left, pb = lower-right, pc = upper-left (we can derive the 4th if needed)
        //
        // Note: we use right and up vectors to offset from the center.
        // Negative up = down; negative right = left.
        Vector3 pa = center + (-right * halfW) + (-up * halfH); // lower-left corner
        Vector3 pb = center + ( right * halfW) + (-up * halfH); // lower-right corner
        Vector3 pc = center + (-right * halfW) + ( up * halfH); // upper-left corner

        // Eye position (the viewer's tracked head/eye)
        Vector3 pe = personEye.position;

        // Build the screen basis:
        // vr = vector pointing along the screen's right edge
        // vu = vector pointing along the screen's up edge
        // vn = normal (perpendicular to the screen), computed via cross product
        Vector3 vr = (pb - pa).normalized;
        Vector3 vu = (pc - pa).normalized;
        Vector3 vn = Vector3.Cross(vr, vu).normalized;

        // Make the normal face the eye (important to avoid flipping the projection):
        // If the normal points in roughly the same direction as the vector from eye to pa,
        // invert the normal.
        Vector3 va = pa - pe;
        if (Vector3.Dot(vn, va) > 0f) vn = -vn;

        // Compute the distance from the eye to the screen plane along the normal.
        // d should be positive when the eye is in front of the screen (on the correct side).
        float d = -Vector3.Dot(va, vn);

        // If d is <= 0, the eye is on or behind the screen plane, which breaks the math.
        // Clamp d to a small positive number to keep the projection stable (but the image won't be meaningful).
        if (d <= 1e-4f)
        {
            d = 1e-4f;
        }

        // Define the near plane distance (n) used by the camera.
        // We already clamp nearClip in OnValidate, but clamp again for safety.
        float n = Mathf.Max(nearClip, 0.0001f);

        // Build vectors from the eye to the other two corners (for computing left/right/top/bottom)
        Vector3 vb = pb - pe;
        Vector3 vc = pc - pe;

        // Compute the off-axis frustum bounds at the near plane:
        // These values tell the projection how far left/right/top/bottom the near plane extends,
        // so that rays from the eye through the screen corners end up on the near plane correctly.
        float leftF   = Vector3.Dot(vr, va) * n / d;
        float rightF  = Vector3.Dot(vr, vb) * n / d;
        float bottomF = Vector3.Dot(vu, va) * n / d;
        float topF    = Vector3.Dot(vu, vc) * n / d;

        // Build the off-center projection matrix using those bounds
        Matrix4x4 proj = PerspectiveOffCenter(leftF, rightF, bottomF, topF, n, farClip);
        hcpCamera.projectionMatrix = proj;

        // Build the world-to-camera (view) matrix:
        // We define a camera basis aligned with the screen: vr (right), vu (up), vn (forward),
        // and place the camera at the eye position.
        Matrix4x4 view = MakeViewMatrix(pe, vr, vu, vn);
        hcpCamera.worldToCameraMatrix = view;

        // For inspector readability, also move and rotate the actual Camera transform
        // to match the eye and look direction. This isn't strictly necessary since we
        // set the matrices manually, but it makes the Scene view more intuitive.
        hcpCamera.transform.position = pe;
        hcpCamera.transform.rotation = Quaternion.LookRotation(vn, vu);
    }

    // Build an off-center perspective projection matrix.
    // This is standard math used by OpenGL/DirectX to define skewed frustums.
    // Unity accepts this layout in Camera.projectionMatrix.
    private static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
    {
        // These formulas compute scale (x,y), offsets (a,b), and depth mapping (c,d,e).
        float x = 2.0f * near / (right - left);
        float y = 2.0f * near / (top - bottom);
        float a = (right + left) / (right - left);
        float b = (top + bottom) / (top - bottom);
        float c = -(far + near) / (far - near);
        float d = -(2.0f * far * near) / (far - near);
        float e = -1.0f;

        Matrix4x4 m = new Matrix4x4();
        m[0,0] = x;    m[0,1] = 0f;  m[0,2] = a;   m[0,3] = 0f;
        m[1,0] = 0f;   m[1,1] = y;   m[1,2] = b;   m[1,3] = 0f;
        m[2,0] = 0f;   m[2,1] = 0f;  m[2,2] = c;   m[2,3] = d;
        m[3,0] = 0f;   m[3,1] = 0f;  m[3,2] = e;   m[3,3] = 0f;
        return m;
    }

    // Build a world-to-camera (view) matrix given:
    // - eye: camera position
    // - vr: camera's right axis
    // - vu: camera's up axis
    // - vn: camera's forward axis
    //
    // The view matrix transforms world coordinates into camera space.
    // Here we assemble a matrix R (rotation) and T (translation) and multiply them.
    private static Matrix4x4 MakeViewMatrix(Vector3 eye, Vector3 vr, Vector3 vu, Vector3 vn)
    {
        // Rotation part: rows contain the camera basis vectors.
        Matrix4x4 r = Matrix4x4.identity;
        r[0,0] = vr.x; r[0,1] = vr.y; r[0,2] = vr.z;
        r[1,0] = vu.x; r[1,1] = vu.y; r[1,2] = vu.z;
        r[2,0] = vn.x; r[2,1] = vn.y; r[2,2] = vn.z;

        // Translation part: moves the world so that the eye is at the origin of camera space.
        Matrix4x4 t = Matrix4x4.identity;
        t[0,3] = -eye.x;
        t[1,3] = -eye.y;
        t[2,3] = -eye.z;

        // View = R * T (apply translation, then rotate axes)
        return r * t;
    }
}