using System;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Demonstrates the clean API. It reacts to gesture names without needing UnityEvent bindings.
    /// </summary>
    public sealed class AkgfDemoApiListener : MonoBehaviour
    {
        public AkgfGestureSystemApi api;
        public AkgfDemoActionTarget target;
        public bool reactOnlyToConfirmedPhase = true;

        [Header("Gesture Names")]
        public string crossArmsGesture = "CrossArms";
        public string waveGesture = "Wave";
        public string headUpGesture = "HeadUp";
        public string headDownGesture = "HeadDown";

        private void Awake()
        {
            if (api == null)
            {
                api = AkgfUnityObjectFinder.FindFirst<AkgfGestureSystemApi>();
            }
            if (target == null)
            {
                target = AkgfUnityObjectFinder.FindFirst<AkgfDemoActionTarget>();
            }
        }

        private void OnEnable()
        {
            if (api != null)
            {
                api.Gesture += HandleGesture;
            }
        }

        private void OnDisable()
        {
            if (api != null)
            {
                api.Gesture -= HandleGesture;
            }
        }

        private void HandleGesture(AkgfGestureEventData data)
        {
            if (data == null || target == null)
            {
                return;
            }

            if (reactOnlyToConfirmedPhase && data.phase != AkgfGesturePhase.Confirmed)
            {
                return;
            }

            if (EqualsName(data.gestureName, crossArmsGesture))
            {
                target.ChangeColor();
            }
            else if (EqualsName(data.gestureName, waveGesture))
            {
                target.SpawnObjectForGesture(data);
            }
            else if (EqualsName(data.gestureName, headUpGesture))
            {
                target.LightOn();
            }
            else if (EqualsName(data.gestureName, headDownGesture))
            {
                target.LightOff();
            }
        }

        private static bool EqualsName(string a, string b)
        {
            return string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
