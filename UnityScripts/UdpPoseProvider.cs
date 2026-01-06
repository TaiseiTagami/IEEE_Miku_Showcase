using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;

public class UdpPoseProvider : MonoBehaviour
{
    [Header("UDP")]
    [SerializeField] private int listenPort = 5005;
    [SerializeField] private bool logErrors = true;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning = false;

    private readonly object lockObject = new object();
    private Vector3 latestPosition = Vector3.zero;
    private bool hasPosition = false;

    private Vector2 latestAngles = Vector2.zero; // yaw, pitch (optional)
    private bool hasAngles = false;

    public bool HasPosition
    {
        get { lock (lockObject) return hasPosition; }
    }

    public bool HasAngles
    {
        get { lock (lockObject) return hasAngles; }
    }

    public Vector3 GetLatestPosition()
    {
        lock (lockObject)
        {
            return latestPosition;
        }
    }

    public Vector2 GetLatestAngles()
    {
        lock (lockObject)
        {
            return latestAngles;
        }
    }

    void Start()
    {
        try
        {
            udpClient = new UdpClient(listenPort);
            isRunning = true;
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Debug.Log($"UdpPoseProvider listening on UDP port {listenPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"UdpPoseProvider failed to bind UDP: {e.Message}");
            enabled = false;
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(data);

                // Split by comma, trim spaces
                string[] parts = message.Split(',');
                for (int i = 0; i < parts.Length; i++)
                    parts[i] = parts[i].Trim();

                // Position: "x,y,z"
                if (parts.Length == 3)
                {
                    if (TryParseFloat(parts[0], out float x) &&
                        TryParseFloat(parts[1], out float y) &&
                        TryParseFloat(parts[2], out float z))
                    {
                        lock (lockObject)
                        {
                            latestPosition = new Vector3(x, y, z);
                            hasPosition = true;
                        }
                    }
                }
                // Angles: "yaw, pitch" (optional support)
                else if (parts.Length == 2)
                {
                    if (TryParseFloat(parts[0], out float yaw) &&
                        TryParseFloat(parts[1], out float pitch))
                    {
                        lock (lockObject)
                        {
                            latestAngles = new Vector2(yaw, pitch);
                            hasAngles = true;
                        }
                    }
                }
            }
            catch (SocketException)
            {
                if (!isRunning) break; // likely shutting down
            }
            catch (System.ObjectDisposedException)
            {
                break; // udpClient closed during shutdown
            }
            catch (System.Exception e)
            {
                if (logErrors)
                    Debug.LogError($"UdpPoseProvider receive error: {e.Message}");
            }
        }
    }

    private bool TryParseFloat(string s, out float value)
    {
        return float.TryParse(
            s,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value
        );
    }

    void OnDisable()
    {
        Shutdown();
    }

    void OnDestroy()
    {
        Shutdown();
    }

    private void Shutdown()
    {
        if (!isRunning) return;
        isRunning = false;

        try { udpClient?.Close(); } catch { /* ignore */ }
        udpClient = null;

        if (receiveThread != null && receiveThread.IsAlive)
        {
            try { receiveThread.Join(200); } catch { /* ignore */ }
            receiveThread = null;
        }
    }
}