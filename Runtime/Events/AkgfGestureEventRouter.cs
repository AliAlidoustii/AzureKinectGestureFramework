using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfGestureEventBinding
    {
        public string gestureName;
        public AkgfGestureKind gestureKind = AkgfGestureKind.Any;
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
        public AkgfGestureRecognizedEvent onDetectedWithScore = new AkgfGestureRecognizedEvent();
        public AkgfGestureRecognizedDetailedEvent onDetectedDetailed = new AkgfGestureRecognizedDetailedEvent();
        public AkgfGesturePhaseDetailedEvent onPhaseDetailed = new AkgfGesturePhaseDetailedEvent();
    }

    public sealed class AkgfGestureEventRouter : MonoBehaviour
    {
        [Header("Preferred: use Coordinator")]
        public AkgfGestureCoordinator coordinator;

        [Header("Fallback: direct recognizers")]
        public AkgfGestureRecognizer recognizer;
        public AkgfSequenceGestureRecognizer sequenceRecognizer;

        public bool caseSensitive = false;
        public List<AkgfGestureEventBinding> bindings = new List<AkgfGestureEventBinding>();

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
            if (coordinator == null)
            {
                coordinator = AkgfUnityObjectFinder.FindFirst<AkgfGestureCoordinator>();
            }

            if (coordinator == null && recognizer == null)
            {
                recognizer = AkgfUnityObjectFinder.FindFirst<AkgfGestureRecognizer>();
            }

            if (coordinator == null && sequenceRecognizer == null)
            {
                sequenceRecognizer = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureRecognizer>();
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
                bindings = new List<AkgfGestureEventBinding>();
            }

            bindings.Add(new AkgfGestureEventBinding { gestureName = gestureName });
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (coordinator != null)
            {
                coordinator.GesturePhase += HandleGestureRecognized;
                subscribed = true;
                return;
            }

            if (recognizer != null)
            {
                recognizer.GestureRecognized += HandleGestureRecognized;
                subscribed = true;
            }

            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.SequenceGestureRecognized += HandleGestureRecognized;
                subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (coordinator != null)
            {
                coordinator.GesturePhase -= HandleGestureRecognized;
            }

            if (recognizer != null)
            {
                recognizer.GestureRecognized -= HandleGestureRecognized;
            }

            if (sequenceRecognizer != null)
            {
                sequenceRecognizer.SequenceGestureRecognized -= HandleGestureRecognized;
            }

            subscribed = false;
        }

        private void HandleGestureRecognized(AkgfGestureMatchResult match)
        {
            if (!match.isValid || bindings == null)
            {
                return;
            }

            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            for (int i = 0; i < bindings.Count; i++)
            {
                AkgfGestureEventBinding binding = bindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.gestureName))
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

        private static bool BindingListensToPhase(AkgfGestureEventBinding binding, AkgfGesturePhase phase)
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

        private static void InvokeBinding(AkgfGestureEventBinding binding, AkgfGestureMatchResult match)
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

            binding.onDetectedWithScore?.Invoke(match.gestureName, match.similarity);
            binding.onDetectedDetailed?.Invoke(match.gestureName, match.similarity, match.gestureKind);
            binding.onPhaseDetailed?.Invoke(match.gestureName, match.similarity, match.gestureKind, match.phase);
        }
    }
}
