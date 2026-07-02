using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public sealed class AkgfSequenceGestureRecognizer : MonoBehaviour
    {
        private struct TimedPose
        {
            public float time;
            public AkgfNormalizedPose pose;
        }

        [Header("References")]
        [Tooltip("Any MonoBehaviour that implements IAkgfSkeletonSource. Example: AkgfTransformSkeletonSource or AkgfOfficialSampleBodySource.")]
        public MonoBehaviour skeletonSourceBehaviour;
        public AkgfSequenceGestureDatabase sequenceGestureDatabase;
        public AkgfGestureSettingsDatabase gestureSettingsDatabase;
        public AkgfGestureGroupController groupController;
        public AkgfCalibrationDatabase calibrationDatabase;

        [Header("Recognition")]
        public AkgfPoseNormalizerSettings normalizerSettings = new AkgfPoseNormalizerSettings();
        public AkgfSequenceGestureMatcherSettings matcherSettings = new AkgfSequenceGestureMatcherSettings();
        public AkgfTrackingQualityFilter trackingQualityFilter = new AkgfTrackingQualityFilter();
        [Range(0f, 1f)] public float minimumSimilarity = 0.72f;
        public float recognitionWindowSeconds = 1.25f;
        public float maxBufferSeconds = 3f;
        public float samplesPerSecond = 15f;
        public float recognitionsPerSecond = 10f;
        public int minimumWindowFrames = 8;
        [Tooltip("How many consecutive comparisons must agree before a sequence fires.")]
        [Range(1, 10)] public int requiredConsecutiveMatches = 2;
        [Tooltip("Minimum time between repeated events for the same sequence gesture. Per-gesture settings override this when present.")]
        public float sameGestureCooldownSeconds = 1.0f;
        public bool autoLoadDatabaseOnStart = true;

        [Header("Events")]
        [Tooltip("Leave false when using AkgfGestureCoordinator, otherwise the sequence recognizer may also fire its own UnityEvent.")]
        public bool fireUnityEventDirectly = true;
        public AkgfGestureRecognizedEvent onSequenceGestureRecognized = new AkgfGestureRecognizedEvent();

        public event Action<AkgfGestureMatchResult> SequenceGestureRecognized;

        public AkgfGestureMatchResult LastMatch { get; private set; } = AkgfGestureMatchResult.None;
        public AkgfPoseSequence LastWindow { get; private set; }
        public bool HasBodyThisFrame { get; private set; }
        public bool HasNormalizedPoseThisFrame { get; private set; }
        public bool HasEnoughBufferedFrames { get; private set; }
        public float LastTrackingQuality { get; private set; }
        public float LastProcessingMilliseconds { get; private set; }
        public int BufferedFrameCount => buffer.Count;

        private IAkgfSkeletonSource source;
        private readonly AkgfPoseNormalizer normalizer = new AkgfPoseNormalizer();
        private readonly List<TimedPose> buffer = new List<TimedPose>(128);
        private float nextSampleTime;
        private float nextRecognitionTime;
        private string consecutiveGestureName = string.Empty;
        private int consecutiveMatchCount;
        private string lastFiredGestureName = string.Empty;
        private float lastFireTime = -9999f;
        private AkgfTrackedBody lastBody;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (autoLoadDatabaseOnStart && sequenceGestureDatabase != null)
            {
                sequenceGestureDatabase.LoadAll();
            }
        }

        private void Update()
        {
            float profilerStart = Time.realtimeSinceStartup;
            Tick();
            LastProcessingMilliseconds = (Time.realtimeSinceStartup - profilerStart) * 1000f;
        }

        public void ResolveReferences()
        {
            source = skeletonSourceBehaviour as IAkgfSkeletonSource;

            if (sequenceGestureDatabase == null)
            {
                sequenceGestureDatabase = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureDatabase>();
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

        public void ClearBuffer()
        {
            buffer.Clear();
            HasEnoughBufferedFrames = false;
            LastWindow = null;
            LastMatch = AkgfGestureMatchResult.None;
            ResetConsecutiveState();
        }

        public bool Tick()
        {
            if (source == null)
            {
                ResolveReferences();
            }

            HasBodyThisFrame = false;
            HasNormalizedPoseThisFrame = false;
            LastTrackingQuality = 0f;

            if (source == null || sequenceGestureDatabase == null)
            {
                LastMatch = AkgfGestureMatchResult.None;
                return false;
            }

            SamplePoseIfDue();
            PruneOldFrames();

            HasEnoughBufferedFrames = CountFramesInsideWindow() >= Mathf.Max(2, minimumWindowFrames);
            if (!HasEnoughBufferedFrames)
            {
                LastMatch = AkgfGestureMatchResult.None;
                LastWindow = null;
                ResetConsecutiveState();
                return false;
            }

            if (Time.time < nextRecognitionTime)
            {
                return false;
            }

            nextRecognitionTime = Time.time + 1f / Mathf.Max(1f, recognitionsPerSecond);
            return RecognizeFromBuffer();
        }

        private void SamplePoseIfDue()
        {
            if (Time.time < nextSampleTime)
            {
                return;
            }

            nextSampleTime = Time.time + 1f / Mathf.Max(1f, samplesPerSecond);

            if (!source.TryGetBody(out AkgfTrackedBody body) || body == null || !body.IsTracked)
            {
                ResetConsecutiveState();
                return;
            }

            lastBody = body;
            HasBodyThisFrame = true;
            LastTrackingQuality = trackingQualityFilter != null ? trackingQualityFilter.ComputeBodyQuality(body) : 1f;
            if (trackingQualityFilter != null && LastTrackingQuality < trackingQualityFilter.minimumOverallBodyQuality)
            {
                ResetConsecutiveState();
                return;
            }

            if (!normalizer.TryNormalize(body, normalizerSettings, out AkgfNormalizedPose normalizedPose))
            {
                ResetConsecutiveState();
                return;
            }

            normalizedPose = calibrationDatabase != null ? calibrationDatabase.ApplyToPose(normalizedPose, false) : normalizedPose;
            HasNormalizedPoseThisFrame = true;
            buffer.Add(new TimedPose
            {
                time = Time.time,
                pose = normalizedPose.Clone()
            });
        }

        private bool RecognizeFromBuffer()
        {
            LastWindow = BuildCurrentWindow();
            if (LastWindow == null || !LastWindow.IsValid)
            {
                LastMatch = AkgfGestureMatchResult.None;
                ResetConsecutiveState();
                return false;
            }

            AkgfGestureMatchResult match = AkgfSequenceGestureMatcher.FindBestMatch(LastWindow, sequenceGestureDatabase.Gestures, matcherSettings, gestureSettingsDatabase, groupController);
            LastMatch = match;
            if (!match.isValid)
            {
                ResetConsecutiveState();
                return false;
            }

            AkgfGestureSettings perGesture = gestureSettingsDatabase != null
                ? gestureSettingsDatabase.GetSettings(match.gestureName, AkgfGestureKind.Sequence)
                : null;

            if (perGesture != null && !perGesture.enabled)
            {
                ResetConsecutiveState();
                return false;
            }

            float gestureQuality = 1f;
            if (trackingQualityFilter != null && lastBody != null)
            {
                if (!trackingQualityFilter.Passes(lastBody, perGesture, out gestureQuality))
                {
                    ResetConsecutiveState();
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
                ResetConsecutiveState();
                return false;
            }

            UpdateConsecutiveAndMaybeFire(match, perGesture);
            return true;
        }

        private void UpdateConsecutiveAndMaybeFire(AkgfGestureMatchResult match, AkgfGestureSettings perGesture)
        {
            int requiredMatches = Mathf.Max(1, requiredConsecutiveMatches);

            // If the user sets Required Consecutive Matches to 1, emit immediately.
            // The old logic still waited for the same sequence candidate twice, which could
            // keep the UI showing candidates without producing a final result.
            if (requiredMatches <= 1)
            {
                consecutiveGestureName = match.gestureName;
                consecutiveMatchCount = 1;
                if (CanFire(match, perGesture))
                {
                    Fire(match);
                }
                return;
            }

            if (!string.Equals(consecutiveGestureName, match.gestureName, StringComparison.OrdinalIgnoreCase))
            {
                consecutiveGestureName = match.gestureName;
                consecutiveMatchCount = 1;
                return;
            }

            consecutiveMatchCount++;
            if (consecutiveMatchCount < requiredMatches)
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
            if (string.Equals(lastFiredGestureName, match.gestureName, StringComparison.OrdinalIgnoreCase) &&
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
            SequenceGestureRecognized?.Invoke(match);

            if (fireUnityEventDirectly)
            {
                onSequenceGestureRecognized?.Invoke(match.gestureName, match.similarity);
            }
        }

        private AkgfPoseSequence BuildCurrentWindow()
        {
            float cutoff = Time.time - Mathf.Max(0.05f, recognitionWindowSeconds);
            List<AkgfNormalizedPose> frames = new List<AkgfNormalizedPose>();

            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].time >= cutoff && buffer[i].pose != null)
                {
                    frames.Add(buffer[i].pose.Clone());
                }
            }

            if (frames.Count < Mathf.Max(2, minimumWindowFrames))
            {
                return null;
            }

            AkgfPoseSequence sequence = new AkgfPoseSequence
            {
                durationSeconds = recognitionWindowSeconds,
                sampleRate = samplesPerSecond,
                frames = frames
            };
            sequence.EnsureValid();
            return sequence;
        }

        private int CountFramesInsideWindow()
        {
            float cutoff = Time.time - Mathf.Max(0.05f, recognitionWindowSeconds);
            int count = 0;
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].time >= cutoff)
                {
                    count++;
                }
            }

            return count;
        }

        private void PruneOldFrames()
        {
            float cutoff = Time.time - Mathf.Max(recognitionWindowSeconds, maxBufferSeconds);
            for (int i = buffer.Count - 1; i >= 0; i--)
            {
                if (buffer[i].time < cutoff)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        private void ResetConsecutiveState()
        {
            consecutiveGestureName = string.Empty;
            consecutiveMatchCount = 0;
        }
    }
}
