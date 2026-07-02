#if AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// AKGF-owned minimal equivalent of Microsoft's sample main.cs.
    /// Use it only if you want a simple component that starts/stops the AKGF tracker handler.
    /// The gesture framework itself does not require this script.
    /// </summary>
    public sealed class AkgfKinectSampleMain : MonoBehaviour
    {
        public AkgfTrackerHandler trackerHandler;
        public bool startTrackingOnStart = true;

        private void Start()
        {
            if (trackerHandler == null)
            {
                trackerHandler = AkgfUnityObjectFinder.FindFirst<AkgfTrackerHandler>();
            }

            if (startTrackingOnStart && trackerHandler != null)
            {
                trackerHandler.StartTracking();
            }
        }

        private void OnDestroy()
        {
            if (trackerHandler != null)
            {
                trackerHandler.StopTracking();
            }
        }
    }
}
#endif
