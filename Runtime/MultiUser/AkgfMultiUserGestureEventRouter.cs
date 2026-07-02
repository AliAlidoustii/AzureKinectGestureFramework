using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfMultiUserGestureEventBinding
    {
        public string gestureName;
        public AkgfGestureKind gestureKind = AkgfGestureKind.Any;
        [Tooltip("-1 means any body. Set a specific body ID if you only want one tracked user to trigger this binding.")]
        public int bodyIdFilter = -1;
        public bool listenToDetected = true;
        public bool listenToEnter = true;
        public bool listenToStay = false;
        public bool listenToExit = false;
        public bool listenToConfirmed = true;

        public UnityEvent onDetected = new UnityEvent();
        public UnityEvent onEnter = new UnityEvent();
        public UnityEvent onStay = new UnityEvent();
        public UnityEvent onExit = new UnityEvent();
        public UnityEvent onConfirmed = new UnityEvent();
        public AkgfMultiUserGestureRecognizedEvent onDetectedWithBody = new AkgfMultiUserGestureRecognizedEvent();
        public AkgfMultiUserGesturePhaseEvent onPhaseWithBody = new AkgfMultiUserGesturePhaseEvent();
    }

    /// <summary>
    /// Routes multi-user gesture events to Inspector UnityEvents.
    /// Bindings can listen to all bodies or one specific Azure Kinect body ID.
    /// </summary>
    public sealed class AkgfMultiUserGestureEventRouter : MonoBehaviour
    {
        public AkgfMultiUserGestureManager multiUserManager;
        public bool caseSensitive = false;
        public List<AkgfMultiUserGestureEventBinding> bindings = new List<AkgfMultiUserGestureEventBinding>();

        private bool subscribed;

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

        public void ResolveReferences()
        {
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

        public void AddBinding(string gestureName)
        {
            if (bindings == null)
            {
                bindings = new List<AkgfMultiUserGestureEventBinding>();
            }

            bindings.Add(new AkgfMultiUserGestureEventBinding { gestureName = gestureName });
        }

        private void Subscribe()
        {
            if (subscribed || multiUserManager == null)
            {
                return;
            }

            multiUserManager.MultiUserGesturePhase += HandleGesture;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (multiUserManager != null)
            {
                multiUserManager.MultiUserGesturePhase -= HandleGesture;
            }

            subscribed = false;
        }

        private void HandleGesture(AkgfGestureMatchResult match)
        {
            if (!match.isValid || bindings == null)
            {
                return;
            }

            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            for (int i = 0; i < bindings.Count; i++)
            {
                AkgfMultiUserGestureEventBinding binding = bindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.gestureName))
                {
                    continue;
                }

                if (binding.bodyIdFilter >= 0 && binding.bodyIdFilter != match.bodyId)
                {
                    continue;
                }

                if (!string.Equals(binding.gestureName.Trim(), match.gestureName, comparison))
                {
                    continue;
                }

                if (binding.gestureKind != AkgfGestureKind.Any && binding.gestureKind != match.gestureKind)
                {
                    continue;
                }

                if (!BindingListensToPhase(binding, match.phase))
                {
                    continue;
                }

                InvokeBinding(binding, match);
            }
        }

        private static bool BindingListensToPhase(AkgfMultiUserGestureEventBinding binding, AkgfGesturePhase phase)
        {
            switch (phase)
            {
                case AkgfGesturePhase.Enter:
                    return binding.listenToEnter;
                case AkgfGesturePhase.Stay:
                    return binding.listenToStay;
                case AkgfGesturePhase.Exit:
                    return binding.listenToExit;
                case AkgfGesturePhase.Confirmed:
                    return binding.listenToConfirmed;
                default:
                    return binding.listenToDetected;
            }
        }

        private static void InvokeBinding(AkgfMultiUserGestureEventBinding binding, AkgfGestureMatchResult match)
        {
            switch (match.phase)
            {
                case AkgfGesturePhase.Enter:
                    binding.onEnter?.Invoke();
                    break;
                case AkgfGesturePhase.Stay:
                    binding.onStay?.Invoke();
                    break;
                case AkgfGesturePhase.Exit:
                    binding.onExit?.Invoke();
                    break;
                case AkgfGesturePhase.Confirmed:
                    binding.onConfirmed?.Invoke();
                    break;
                default:
                    binding.onDetected?.Invoke();
                    break;
            }

            binding.onDetectedWithBody?.Invoke(match.bodyId, match.gestureName, match.similarity, match.gestureKind);
            binding.onPhaseWithBody?.Invoke(AkgfGestureEventData.FromMatch(match, AkgfTrackingMode.MultiUser));
        }
    }
}
