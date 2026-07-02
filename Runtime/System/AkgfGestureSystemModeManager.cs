using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Developer-facing mode switch. Set Tracking Mode in the Inspector.
    /// The selected mode is applied by enabling one root and disabling the other.
    /// </summary>
    public sealed class AkgfGestureSystemModeManager : MonoBehaviour
    {
        public AkgfTrackingMode trackingMode = AkgfTrackingMode.SingleUser;
        public GameObject singleUserSystemRoot;
        public GameObject multiUserSystemRoot;
        public bool applyOnStart = true;
        public bool applyInEditorOnValidate = true;

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyMode();
            }
        }

        private void OnValidate()
        {
            if (applyInEditorOnValidate)
            {
                ApplyMode();
            }
        }

        [ContextMenu("Apply Tracking Mode")]
        public void ApplyMode()
        {
            bool useSingle = trackingMode == AkgfTrackingMode.SingleUser;
            if (singleUserSystemRoot != null)
            {
                singleUserSystemRoot.SetActive(useSingle);
            }

            if (multiUserSystemRoot != null)
            {
                multiUserSystemRoot.SetActive(!useSingle);
            }
        }

        public void SetSingleUserMode()
        {
            trackingMode = AkgfTrackingMode.SingleUser;
            ApplyMode();
        }

        public void SetMultiUserMode()
        {
            trackingMode = AkgfTrackingMode.MultiUser;
            ApplyMode();
        }
    }
}
