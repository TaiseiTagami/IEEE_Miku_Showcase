using UnityEngine;

public class DisplayManager : MonoBehaviour
{
    void Start()
    {
        // Display.displays[0] is Display 1 (always active)
        // Activate Display 2 and Display 3 if present
        for (int i = 1; i < Display.displays.Length; i++)
        {
            // You can specify resolution and refresh rate if desired:
            // Display.displays[i].Activate(width, height, refreshRate);
            Display.displays[i].Activate();
        }
    }
}