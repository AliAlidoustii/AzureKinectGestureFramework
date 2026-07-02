using System;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfGestureRecognizedEvent : UnityEngine.Events.UnityEvent<string, float>
    {
    }

    public sealed class AkgfGestureRecognizer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Any MonoBehaviour that implements IAkgfSkeletonSource. Example: AkgfTransformSkeletonSource or AkgfOfficialSampleBodySource.")]
        public MonoBehaviour skeletonSourceBehaviour;
        public AkgfGestureDatabase gestureDatabase;
        public AkgfGestureSettingsDatabase gestureSettingsDatabase;
        public AkgfGestureGroupController groupController;
        public AkgfCalibrationDatabase calibrationDatabase;

        [Header("Recognition")]
        public AkgfPoseNormalizerSettings normalizerSettings = new AkgfPoseNormalizerSettings();
        public AkgfGestureMatcherSettings matcherSettings = new AkgfGestureMatcherSettings();
        public AkgfTrackingQualityFilter trackingQualityFilter = new AkgfTrackingQualityFilter();
        [Range(0f, 1f)] public float minimumSimilarity = 0.60f;
        [Tooltip("Gesture must remain the best match for this long before it fires. Per-gesture settings override this when present.")]
        public float requiredStableSeconds = 0.20f;
        [Tooltip("Minimum time between repeated events for the same gesture. Per-gesture settings override this when present.")]
        public float sameGestureCooldownSeconds = 0.75f;
        public bool recognizeEveryFrame = false;
        public bool autoLoadDatabaseOnStart = true;

        [Header("Events")]
        [Tooltip("Leave false when using AkgfGestureCoordinator, otherwise the static recognizer may also fire its own UnityEvent.")]
        public bool fireUnityEventDirectly = true;
        public AkgfGestureRecognizedEvent onGestureRecognized = new AkgfGestureRecognizedEvent();

        public event Action<AkgfGestureMatchResult> GestureRecognized;

        public AkgfGestureMatchResult LastMatch { get; private set; } = AkgfGestureMatchResult.None;
        public AkgfNormalizedPose LastPose { get; private set; }
        public bool HasBodyThisFrame { get; private set; }
        public bool HasNormalizedPoseThisFrame { get; private set; }
        public float LastTrackingQuality { get; private set; }
        public float LastProcessingMilliseconds { get; private set; }

        private IAkgfSkeletonSource source;
        private readonly AkgfPoseNormalizer normalizer = new AkgfPoseNormalizer();
        private string stableGestureName = string.Empty;
        private float stableSinceTime;
        private string lastFiredGestureName = string.Empty;
        private float lastFireTime = -9999f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (autoLoadDatabaseOnStart && gestureDatabase != null)
            {
                gestureDatabase.LoadAll();
            }
        }

        private void Update()
        {
            float profilerStart = Time.realtimeSinceStartup;
            RecognizeOnce();
            LastProcessingMilliseconds = (Time.realtimeSinceStartup - profilerStart) * 1000f;
        }

        public void ResolveReferences()
        {
            source = skeletonSourceBehaviour as IAkgfSkeletonSource;

            if (gestureDatabase == null)
            {
                gestureDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureDatabase>();
            }

            if (gestureSettingsDatabase == null)
            {
                gestureSettingsDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureSettingsDatabase>();
            }

            if (groupController == null)
            {
                groupController = AkgfUnityObjectFinder.FindFirst<AkgfGestureGroupController>();
            }

            if (calibrationDatabase == null)
            {
                calibrationDatabase = AkgfUnityObjectFinder.FindFirst<AkgfCalibrationDatabase>();
            }
        }

        public bool RecognizeOnce()
        {
            if (source == null)
            {
                ResolveReferences();
            }

            HasBodyThisFrame = false;
            HasNormalizedPoseThisFrame = false;
            LastTrackingQuality = 0f;
            LastMatch = AkgfGestureMatchResult.None;

            if (source == null || gestureDatabase == null)
            {
                return false;
            }

            if (!source.TryGetBody(out AkgfTrackedBody body) || body == null || !body.IsTracked)
            {
                ResetStability();
                return false;
            }

            HasBodyThisFrame = true;
            LastTrackingQuality = trackingQualityFilter != null ? trackingQualityFilter.ComputeBodyQuality(body) : 1f;
            if (trackingQualityFilter != null && LastTrackingQuality < trackingQualityFilter.minimumOverallBodyQuality)
            {
                ResetStability();
                return false;
            }

            if (!normalizer.TryNormalize(body, normalizerSettings, out AkgfNormalizedPose normalizedPose))
            {
                LastPose = null;
                ResetStability();
                return false;
            }

            normalizedPose = calibrationDatabase != null ? calibrationDatabase.ApplyToPose(normalizedPose, false) : normalizedPose;
            LastPose = normalizedPose;
            HasNormalizedPoseThisFrame = true;

            AkgfGestureMatchResult match = AkgfGestureMatcher.FindBestMatch(normalizedPose, gestureDatabase.Gestures, matcherSettings, gestureSettingsDatabase, groupController);
            LastMatch = match;
            if (!match.isValid)
            {
                ResetStability();
                return false;
            }

            AkgfGestureSettings perGesture = gestureSettingsDatabase != null
                ? gestureSettingsDatabase.GetSettings(match.gestureName, AkgfGestureKind.StaticPose)
                : null;

            if (perGesture != null && !perGesture.enabled)
            {
                ResetStability();
                return false;
            }

            if (perGesture != null && groupController != null && !groupController.IsGroupActive(perGesture.groupName))
            {
                ResetStability();
                return false;
            }

            float gestureQuality = 1f;
            if (trackingQualityFilter != null)
            {
                if (!trackingQualityFilter.Passes(body, perGesture, out gestureQuality))
                {
                    ResetStability();
                    return false;
                }

                match.similarity = trackingQualityFilter.ApplyQualityPenalty(match.similarity, gestureQuality, perGesture);
            }

            match.trackingQuality = gestureQuality;
            match.groupName = perGesture != null ? perGesture.groupName : match.groupName;
            match.priority = perGesture != null ? perGesture.priority : match.priority;
            LastMatch = match;

            float threshold = perGesture != null ? perGesture.minimumSimilarity : minimumSimilarity;
            if (match.similarity < threshold)
            {
                ResetStability();
                return false;
            }

            UpdateStabilityAndMaybeFire(match, perGesture);
            return true;
        }

        private void UpdateStabilityAndMaybeFire(AkgfGestureMatchResult match, AkgfGestureSettings perGesture)
        {
            float requiredSeconds = perGesture != null ? perGesture.requiredStableSeconds : requiredStableSeconds;

            // Important: when requiredStableSeconds is 0, the old logic still waited for the
            // same gesture to appear on a second consecutive frame. With noisy body data this
            // can leave the gesture visible as a candidate forever, but never emit a result.
            // In test/direct mode, accept the candidate immediately after threshold checks.
            if (recognizeEveryFrame || requiredSeconds <= 0.0001f)
            {
                if (CanFire(match, perGesture))
                {
                    stableGestureName = match.gestureName;
                    stableSinceTime = Time.time;
                    Fire(match);
                }
                return;
            }

            if (!string.Equals(stableGestureName, match.gestureName, StringComparison.OrdinalIgnoreCase))
            {
                stableGestureName = match.gestureName;
                stableSinceTime = Time.time;
                return;
            }

            float stableDuration = Time.time - stableSinceTime;
            if (stableDuration < Mathf.Max(0f, requiredSeconds))
            {
                return;
            }

            if (!CanFire(match, perGesture))
            {
                return;
            }

            Fire(match);
        }

        private bool CanFire(AkgfGestureMatchResult match, AkgfGestureSettings perGesture)
        {
            float cooldown = perGesture != null ? perGesture.cooldownSeconds : sameGestureCooldownSeconds;
            if (!recognizeEveryFrame &&
                string.Equals(lastFiredGestureName, match.gestureName, StringComparison.OrdinalIgnoreCase) &&
                Time.time - lastFireTime < Mathf.Max(0f, cooldown))
            {
                return false;
            }

            return true;
        }

        private void Fire(AkgfGestureMatchResult match)
        {
            lastFiredGestureName = match.gestureName;
            lastFireTime = Time.time;
            GestureRecognized?.Invoke(match);

            if (fireUnityEventDirectly)
            {
                onGestureRecognized?.Invoke(match.gestureName, match.similarity);
            }
        }

        private void ResetStability()
        {
            stableGestureName = string.Empty;
            stableSinceTime = Time.time;
        }
    }
}
