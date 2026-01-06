using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class FaceTrackingReceiver : MonoBehaviour
{
    private UdpClient udpClient;
    private Thread receiveThread;
    private object lockObject = new object();
    private bool isRunning = true;

    [Header("Tracking Settings")]
    [SerializeField] private float smoothing = 0.3f;
    [SerializeField] private float positionScale = 1.0f;
    
    [Header("Camera Setup")]
    [SerializeField] private Transform virtualCamera; // Reference to the object representing real camera
    [SerializeField] private float cameraFOV_X = 90f; // Should match Python FOV_X
    [SerializeField] private float cameraFOV_Y = 45f; // Should match Python FOV_Y

    private Vector3 targetPosition;
    private bool hasReceivedFirstPosition = false;

    void Start()
    {
        if (virtualCamera == null)
        {
            Debug.LogError("Virtual camera reference is missing!");
            enabled = false;
            return;
        }

        udpClient = new UdpClient(5005);
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void ReceiveData()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(data);
                
                string[] positions = message.Split(',');
                if (positions.Length == 3)
                {
                    float x = float.Parse(positions[0]) * positionScale;
                    float y = float.Parse(positions[1]) * positionScale;
                    float z = float.Parse(positions[2]) * positionScale;

                    lock (lockObject)
                    {
                        // Store the position relative to camera space
                        targetPosition = new Vector3(x, y, z);
                        hasReceivedFirstPosition = true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error receiving UDP data: {e.Message}");
            }
        }
    }

    void Update()
    {
        if (hasReceivedFirstPosition)
        {
            Vector3 currentTarget;
            lock (lockObject)
            {
                currentTarget = targetPosition;
            }

            // Convert the target position from camera space to world space
            Vector3 worldSpaceTarget = virtualCamera.TransformPoint(currentTarget);

            // Apply smoothing in world space
            transform.position = Vector3.Lerp(
                transform.position, 
                worldSpaceTarget, 
                smoothing
            );
        }
    }

    void OnDrawGizmos()
    {
        if (virtualCamera != null)
        {
            // Draw camera frustum
            Gizmos.color = Color.yellow;
            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(virtualCamera.position, virtualCamera.rotation, Vector3.one);
            
            float near = 0.1f;
            float far = 2f;
            
            float nearHeight = 2f * near * Mathf.Tan(cameraFOV_Y * 0.5f * Mathf.Deg2Rad);
            float nearWidth = nearHeight * Mathf.Tan(cameraFOV_X * 0.5f * Mathf.Deg2Rad);
            
            float farHeight = 2f * far * Mathf.Tan(cameraFOV_Y * 0.5f * Mathf.Deg2Rad);
            float farWidth = farHeight * Mathf.Tan(cameraFOV_X * 0.5f * Mathf.Deg2Rad);

            // Near plane
            Gizmos.DrawLine(new Vector3(-nearWidth/2, -nearHeight/2, near), new Vector3(nearWidth/2, -nearHeight/2, near));
            Gizmos.DrawLine(new Vector3(-nearWidth/2, nearHeight/2, near), new Vector3(nearWidth/2, nearHeight/2, near));
            Gizmos.DrawLine(new Vector3(-nearWidth/2, -nearHeight/2, near), new Vector3(-nearWidth/2, nearHeight/2, near));
            Gizmos.DrawLine(new Vector3(nearWidth/2, -nearHeight/2, near), new Vector3(nearWidth/2, nearHeight/2, near));

            // Far plane
            Gizmos.DrawLine(new Vector3(-farWidth/2, -farHeight/2, far), new Vector3(farWidth/2, -farHeight/2, far));
            Gizmos.DrawLine(new Vector3(-farWidth/2, farHeight/2, far), new Vector3(farWidth/2, farHeight/2, far));
            Gizmos.DrawLine(new Vector3(-farWidth/2, -farHeight/2, far), new Vector3(-farWidth/2, farHeight/2, far));
            Gizmos.DrawLine(new Vector3(farWidth/2, -farHeight/2, far), new Vector3(farWidth/2, farHeight/2, far));

            // Connecting lines
            Gizmos.DrawLine(new Vector3(-nearWidth/2, -nearHeight/2, near), new Vector3(-farWidth/2, -farHeight/2, far));
            Gizmos.DrawLine(new Vector3(nearWidth/2, -nearHeight/2, near), new Vector3(farWidth/2, -farHeight/2, far));
            Gizmos.DrawLine(new Vector3(-nearWidth/2, nearHeight/2, near), new Vector3(-farWidth/2, farHeight/2, far));
            Gizmos.DrawLine(new Vector3(nearWidth/2, nearHeight/2, near), new Vector3(farWidth/2, farHeight/2, far));

            Gizmos.matrix = originalMatrix;
        }
    }

    void OnDisable()
    {
        isRunning = false;
        if (receiveThread != null) receiveThread.Abort();
        if (udpClient != null) udpClient.Close();
    }
}