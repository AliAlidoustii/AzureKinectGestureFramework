using AzureKinectGestureFramework;
using UnityEngine;

public sealed class AKGFPoseConsolePrinter : MonoBehaviour
{
    [Header("AKGF")]
    [SerializeField] private AkgfGestureSystemApi gestureSystem;

    private void Awake()
    {
        if (gestureSystem == null)
        {
#if UNITY_2023_1_OR_NEWER
            gestureSystem = Object.FindFirstObjectByType<AkgfGestureSystemApi>();
#else
            gestureSystem = Object.FindObjectOfType<AkgfGestureSystemApi>();
#endif
        }
    }

    private void OnEnable()
    {
        if (gestureSystem != null)
        {
            gestureSystem.Gesture += OnGestureRecognized;
        }
        else
        {
            Debug.LogWarning("[AKGF] No AkgfGestureSystemApi found in the scene.");
        }
    }

    private void OnDisable()
    {
        if (gestureSystem != null)
        {
            gestureSystem.Gesture -= OnGestureRecognized;
        }
    }

    private void OnGestureRecognized(AkgfGestureEventData data)
    {
        Debug.Log($"Detected Pose/Gesture: {data.gestureName}");

        // Write your own actions here later.
        // Example:
        //
        // if (data.gestureName == "CrossArms")
        // {
        //     // Do something
        // }
    }
}