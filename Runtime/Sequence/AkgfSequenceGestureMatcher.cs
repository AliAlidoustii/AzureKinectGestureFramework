using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfSequenceGestureMatcherSettings
    {
        [Tooltip("Sequence confidence formula: sensitivity / (sensitivity + distance). Higher values make confidence more forgiving. Good range: 0.80 - 1.60.")]
        public float similaritySensitivity = 1.20f;

        [Tooltip("Every sequence is resampled to this frame count before comparison. 18-32 is usually enough.")]
        [Range(8, 60)] public int resampledFrameCount = 24;

        [Tooltip("Dynamic Time Warping helps when the same movement is performed slightly faster or slower.")]
        public bool useDynamicTimeWarping = true;

        [Tooltip("DTW only compares frames within this local window to avoid bad warping. 4-8 is usually good for 24 frames.")]
        [Range(1, 20)] public int dtwWindow = 6;

        [Tooltip("Reuses the static pose distance metric for each frame.")]
        public AkgfGestureMatcherSettings poseMatcherSettings = new AkgfGestureMatcherSettings();
    }

    public static class AkgfSequenceGestureMatcher
    {
        public static AkgfGestureMatchResult FindBestMatch(
            AkgfPoseSequence currentSequence,
            IReadOnlyList<AkgfSequenceGestureData> gestures,
            AkgfSequenceGestureMatcherSettings settings,
            AkgfGestureSettingsDatabase gestureSettingsDatabase = null,
            AkgfGestureGroupController groupController = null)
        {
            if (currentSequence == null || !currentSequence.IsValid || gestures == null || gestures.Count == 0)
            {
                return AkgfGestureMatchResult.None;
            }

            settings = settings ?? new AkgfSequenceGestureMatcherSettings();
            AkgfGestureMatchResult best = AkgfGestureMatchResult.None;

            for (int i = 0; i < gestures.Count; i++)
            {
                AkgfSequenceGestureData gesture = gestures[i];
                if (gesture == null || !gesture.IsUsable)
                {
                    continue;
                }

                gesture.EnsureValid();
                AkgfGestureSettings perGesture = gestureSettingsDatabase != null
                    ? gestureSettingsDatabase.GetSettings(gesture.gestureName, AkgfGestureKind.Sequence)
                    : null;

                if (perGesture != null)
                {
                    if (!perGesture.enabled)
                    {
                        continue;
                    }

                    if (groupController != null && !groupController.IsGroupActive(perGesture.groupName))
                    {
                        continue;
                    }
                }

                bool wasMirrored;
                AkgfMirrorMode mirrorMode = perGesture != null ? perGesture.mirrorMode : AkgfMirrorMode.Strict;
                float distance = DistanceToGesture(currentSequence, gesture, settings, mirrorMode, out wasMirrored);
                if (!AkgfMath.IsFinite(distance))
                {
                    continue;
                }

                float similarity = AkgfGestureMatcher.DistanceToSimilarity(distance, settings.similaritySensitivity);
                if (!best.isValid || similarity > best.similarity)
                {
                    best = new AkgfGestureMatchResult
                    {
                        gestureName = gesture.gestureName,
                        similarity = similarity,
                        distance = distance,
                        sampleCount = gesture.samples.Count,
                        isValid = true,
                        gestureKind = AkgfGestureKind.Sequence,
                        phase = AkgfGesturePhase.Detected,
                        groupName = perGesture != null ? perGesture.groupName : string.Empty,
                        priority = perGesture != null ? perGesture.priority : 10,
                        trackingQuality = 1f,
                        wasMirrored = wasMirrored
                    };
                }
            }

            return best;
        }

        public static float DistanceBetweenSequences(
            AkgfPoseSequence a,
            AkgfPoseSequence b,
            AkgfSequenceGestureMatcherSettings settings,
            out bool wasMirrored,
            AkgfMirrorMode mirrorMode = AkgfMirrorMode.Strict)
        {
            wasMirrored = false;
            if (a == null || b == null || !a.IsValid || !b.IsValid)
            {
                return float.PositiveInfinity;
            }

            settings = settings ?? new AkgfSequenceGestureMatcherSettings();
            int frameCount = Mathf.Clamp(settings.resampledFrameCount, 2, 120);

            List<AkgfNormalizedPose> aFrames = ResampleFrames(a, frameCount);
            List<AkgfNormalizedPose> bFrames = ResampleFrames(b, frameCount);

            if (aFrames == null || bFrames == null || aFrames.Count == 0 || bFrames.Count == 0)
            {
                return float.PositiveInfinity;
            }

            float strictDistance = settings.useDynamicTimeWarping
                ? DynamicTimeWarpingDistance(aFrames, bFrames, settings)
                : AlignedDistance(aFrames, bFrames, settings.poseMatcherSettings);

            if (mirrorMode == AkgfMirrorMode.Strict)
            {
                return strictDistance;
            }

            AkgfPoseSequence mirroredA = AkgfPoseMirrorUtility.CreateMirroredSequence(a);
            List<AkgfNormalizedPose> mirroredAFrames = ResampleFrames(mirroredA, frameCount);
            float mirroredDistance = settings.useDynamicTimeWarping
                ? DynamicTimeWarpingDistance(mirroredAFrames, bFrames, settings)
                : AlignedDistance(mirroredAFrames, bFrames, settings.poseMatcherSettings);

            if (mirroredDistance < strictDistance)
            {
                wasMirrored = true;
                return mirroredDistance;
            }

            return strictDistance;
        }

        private static float DistanceToGesture(
            AkgfPoseSequence currentSequence,
            AkgfSequenceGestureData gesture,
            AkgfSequenceGestureMatcherSettings settings,
            AkgfMirrorMode mirrorMode,
            out bool wasMirrored)
        {
            wasMirrored = false;
            float best = float.PositiveInfinity;

            for (int i = 0; i < gesture.samples.Count; i++)
            {
                AkgfPoseSequence sample = gesture.samples[i];
                if (sample == null || !sample.IsValid)
                {
                    continue;
                }

                bool sampleWasMirrored;
                float distance = DistanceBetweenSequences(currentSequence, sample, settings, out sampleWasMirrored, mirrorMode);
                if (distance < best)
                {
                    best = distance;
                    wasMirrored = sampleWasMirrored;
                }
            }

            return best;
        }

        private static float AlignedDistance(
            IReadOnlyList<AkgfNormalizedPose> a,
            IReadOnlyList<AkgfNormalizedPose> b,
            AkgfGestureMatcherSettings poseSettings)
        {
            int count = Mathf.Min(a.Count, b.Count);
            if (count <= 0)
            {
                return float.PositiveInfinity;
            }

            float sum = 0f;
            int valid = 0;
            for (int i = 0; i < count; i++)
            {
                float d = AkgfGestureMatcher.PoseDistance(a[i], b[i], poseSettings);
                if (!AkgfMath.IsFinite(d))
                {
                    continue;
                }

                sum += d;
                valid++;
            }

            return valid > 0 ? sum / valid : float.PositiveInfinity;
        }

        private static float DynamicTimeWarpingDistance(
            IReadOnlyList<AkgfNormalizedPose> a,
            IReadOnlyList<AkgfNormalizedPose> b,
            AkgfSequenceGestureMatcherSettings settings)
        {
            return AkgfDynamicTimeWarping.Distance(
                a,
                b,
                settings.dtwWindow,
                (left, right) => AkgfGestureMatcher.PoseDistance(left, right, settings.poseMatcherSettings));
        }

        public static List<AkgfNormalizedPose> ResampleFrames(AkgfPoseSequence sequence, int targetFrameCount)
        {
            sequence.EnsureValid();
            int sourceCount = sequence.frames.Count;
            if (sourceCount == 0 || targetFrameCount <= 0)
            {
                return null;
            }

            List<AkgfNormalizedPose> result = new List<AkgfNormalizedPose>(targetFrameCount);

            if (sourceCount == 1)
            {
                for (int i = 0; i < targetFrameCount; i++)
                {
                    result.Add(sequence.frames[0].Clone());
                }

                return result;
            }

            for (int i = 0; i < targetFrameCount; i++)
            {
                float t = targetFrameCount == 1 ? 0f : (float)i / (targetFrameCount - 1);
                float sourcePosition = t * (sourceCount - 1);
                int leftIndex = Mathf.FloorToInt(sourcePosition);
                int rightIndex = Mathf.Min(leftIndex + 1, sourceCount - 1);
                float blend = sourcePosition - leftIndex;

                result.Add(LerpPose(sequence.frames[leftIndex], sequence.frames[rightIndex], blend));
            }

            return result;
        }

        private static AkgfNormalizedPose LerpPose(AkgfNormalizedPose a, AkgfNormalizedPose b, float t)
        {
            AkgfNormalizedPose pose = new AkgfNormalizedPose();
            pose.EnsureArrays();
            t = Mathf.Clamp01(t);

            for (int i = 0; i < pose.values.Length; i++)
            {
                pose.values[i] = Mathf.Lerp(a.values[i], b.values[i], t);
            }

            for (int i = 0; i < pose.weights.Length; i++)
            {
                pose.weights[i] = Mathf.Lerp(a.weights[i], b.weights[i], t);
            }

            return pose;
        }
    }
}
