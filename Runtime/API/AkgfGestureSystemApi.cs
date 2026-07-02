using System;
using UnityEngine;
using UnityEngine.Events;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfGestureSystemEvent : UnityEvent<AkgfGestureEventData>
    {
    }

    /// <summary>
    /// High-level, game-code friendly API. Subscribe once here instead of talking to separate
    /// static, sequence, coordinator, and multi-user components.
    /// </summary>
    public sealed class AkgfGestureSystemApi : MonoBehaviour
    {
        [Header("References")]
        public AkgfGestureSystemModeManager modeManager;
        public AkgfGestureCoordinator singleUserCoordinator;
        public AkgfMultiUserGestureManager multiUserManager;
        public bool autoFindReferences = true;

        [Header("Unity Event")]
        public AkgfGestureSystemEvent onGesture = new AkgfGestureSystemEvent();

        public event Action<AkgfGestureEventData> Gesture;
        public event Action<AkgfGestureEventData> SingleUserGesture;
        public event Action<AkgfGestureEventData> MultiUserGesture;

        public AkgfGestureEventData LastGesture { get; private set; }
        public AkgfTrackingMode CurrentMode => modeManager != null ? modeManager.trackingMode : AkgfTrackingMode.SingleUser;

        private bool singleUserSubscribed;
        private bool multiUserSubscribed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            // The gesture system can be created/configured after this API component wakes up.
            // Keep reconnecting missing event links so final outputs reach user scripts.
            ResolveReferences();
            Subscribe();
        }

        public void ResolveReferences()
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (modeManager == null)
            {
                modeManager = AkgfUnityObjectFinder.FindFirst<AkgfGestureSystemModeManager>();
            }

            if (singleUserCoordinator == null)
            {
                singleUserCoordinator = AkgfUnityObjectFinder.FindFirst<AkgfGestureCoordinator>();
            }

            if (multiUserManager == null)
            {
                multiUserManager = AkgfUnityObjectFinder.FindFirst<AkgfMultiUserGestureManager>();
            }
        }

        public void Reconnect()
        {
            Unsubscribe();
            ResolveReferences();
            Subscribe();
        }

        private void Subscribe()
        {
            if (singleUserCoordinator != null && !singleUserSubscribed)
            {
                singleUserCoordinator.GesturePhase += HandleSingleUserGesture;
                singleUserSubscribed = true;
            }

            if (multiUserManager != null && !multiUserSubscribed)
            {
                multiUserManager.MultiUserGesturePhase += HandleMultiUserGesture;
                multiUserSubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (singleUserCoordinator != null && singleUserSubscribed)
            {
                singleUserCoordinator.GesturePhase -= HandleSingleUserGesture;
            }

            if (multiUserManager != null && multiUserSubscribed)
            {
                multiUserManager.MultiUserGesturePhase -= HandleMultiUserGesture;
            }

            singleUserSubscribed = false;
            multiUserSubscribed = false;
        }

        private void HandleSingleUserGesture(AkgfGestureMatchResult match)
        {
            AkgfGestureEventData data = AkgfGestureEventData.FromMatch(match, AkgfTrackingMode.SingleUser);
            LastGesture = data;
            SingleUserGesture?.Invoke(data);
            Gesture?.Invoke(data);
            onGesture?.Invoke(data);
        }

        private void HandleMultiUserGesture(AkgfGestureMatchResult match)
        {
            AkgfGestureEventData data = AkgfGestureEventData.FromMatch(match, AkgfTrackingMode.MultiUser);
            LastGesture = data;
            MultiUserGesture?.Invoke(data);
            Gesture?.Invoke(data);
            onGesture?.Invoke(data);
        }
    }
}
