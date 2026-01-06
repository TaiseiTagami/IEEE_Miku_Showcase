using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/// <summary>
/// Receives yaw/pitch from Python via UDP and rotates+moves the object "Webcam"
/// around the pivot "WebcamCenter" at a fixed radius (5 cm).
/// Message format (UTF-8): "yawDeg,pitchDeg"
/// Example: "12.5,-3.0"
/// </summary>
public class WebcamOrbitDriver : MonoBehaviour
{
    [Header("Scene References")]
    public Transform WebcamCenter;   // pivot of rotation (Webcam orbits around this)
    public Transform Webcam;         // the object representing the camera

    [Header("Network")]
    public int UdpPort = 5005;       // must match Python sender

    [Header("Orbit Settings")]
    public float OrbitRadiusMeters = 0.05f;     // 5 cm radius
    public Vector3 OrbitOffsetLocal = Vector3.forward; // local direction of the offset before scaling by radius

    [Header("Smoothing (optional)")]
    [Range(0f, 1f)] public float RotationSmooth = 0.3f;   // 0 = no smoothing, 1 = frozen
    [Range(0f, 1f)] public float PositionSmooth = 0.3f;   // 0 = no smoothing, 1 = frozen

    // UDP internals
    private UdpClient _udp;
    private Thread _thread;
    private volatile bool _running;

    // Latest data from Python (servos)
    private volatile float _yawDeg;   // rotate around Y axis (left/right)
    private volatile float _pitchDeg; // rotate around X axis (up/down)
    private volatile bool _hasData;

    // Smoothed state
    private Quaternion _smoothedRot = Quaternion.identity;
    private Vector3 _smoothedOffsetWorld = Vector3.forward * 0.05f;

    void Start()
    {
        if (WebcamCenter == null || Webcam == null)
        {
            Debug.LogError("WebcamOrbitDriver: Assign WebcamCenter and Webcam in the inspector.");
            enabled = false;
            return;
        }

        // Normalize and scale the local orbit offset
        if (OrbitOffsetLocal == Vector3.zero) OrbitOffsetLocal = Vector3.forward;
        OrbitOffsetLocal = OrbitOffsetLocal.normalized * OrbitRadiusMeters;

        // Setup UDP listener
        _udp = new UdpClient(UdpPort);
        _running = true;
        _thread = new Thread(UdpListen) { IsBackground = true };
        _thread.Start();
    }

    void Update()
    {
        // Build target rotation from received yaw/pitch
        // Unity convention: yaw about Y, pitch about X, roll about Z
        Quaternion targetRot = Quaternion.Euler(_pitchDeg, _yawDeg, 0f);
        _smoothedRot = SlerpAlpha(_smoothedRot, targetRot, RotationSmooth);

        // Compute world-space offset from pivot using the current rotation
        Vector3 targetOffsetWorld = _smoothedRot * OrbitOffsetLocal;
        _smoothedOffsetWorld = Vector3.Lerp(_smoothedOffsetWorld, targetOffsetWorld, Mathf.Clamp01(PositionSmooth));

        // Place and orient the Webcam
        Webcam.position = WebcamCenter.position + _smoothedOffsetWorld;
        Webcam.rotation = _smoothedRot;
    }

    void OnDestroy()
    {
        _running = false;
        try { _udp?.Close(); } catch { }
        try { _thread?.Join(200); } catch { }
    }

    private void UdpListen()
    {
        IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref any);
                string msg = Encoding.UTF8.GetString(data).Trim();
                // Example: "x, y, z, yaw, pitch"
                
                var parts = msg.Split(',');
                if (parts.Length >= 2)
                {
                    float yaw = Parse(parts[3]);
                    float pitch = Parse(parts[4]);

                    _yawDeg = yaw;
                    _pitchDeg = pitch;
                    _hasData = true;
                }
            }
            catch
            {
                // Ignore transient socket errors
            }
        }
    }

    private static float Parse(string s)
    {
        if (float.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v))
            return v;
        return 0f;
    }

    // Exponential-like smoothing for quaternions using Slerp with alpha
    private static Quaternion SlerpAlpha(Quaternion current, Quaternion target, float alpha)
    {
        if (alpha <= 0f) return target;
        if (alpha >= 1f) return current;
        return Quaternion.Slerp(current, target, alpha);
    }
}