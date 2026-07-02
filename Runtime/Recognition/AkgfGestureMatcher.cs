using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfGestureMatcherSettings
    {
        [Tooltip("Confidence formula: sensitivity / (sensitivity + distance). Higher values make confidence more forgiving. Good range: 0.45 - 0.90.")]
        public float similaritySensitivity = 0.65f;

        [Tooltip("Use the nearest N samples from each gesture instead of only the single nearest sample.")]
        [Range(1, 10)] public int nearestSampleCount = 3;

        [Tooltip("Useful for hand/head gestures where legs should not dominate the match.")]
        public bool reduceLowerBodyWeight = true;

        [Range(0f, 1f)] public float lowerBodyWeightMultiplier = 0.15f;

        [Tooltip("Hands, wrists and elbows are often the meaningful part of a gesture, so this boosts them slightly.")]
        public bool boostArmAndHandWeight = true;

        [Range(1f, 4f)] public float armAndHandWeightMultiplier = 1.6f;
    }

    public static class AkgfGestureMatcher
    {
        public static AkgfGestureMatchResult FindBestMatch(
            AkgfNormalizedPose currentPose,
            IReadOnlyList<AkgfGestureData> gestures,
            AkgfGestureMatcherSettings settings,
            AkgfGestureSettingsDatabase gestureSettingsDatabase = null,
            AkgfGestureGroupController groupController = null)
        {
            if (currentPose == null || !currentPose.IsValid || gestures == null || gestures.Count == 0)
            {
                return AkgfGestureMatchResult.None;
            }

            settings = settings ?? new AkgfGestureMatcherSettings();
            AkgfGestureMatchResult best = AkgfGestureMatchResult.None;

            for (int i = 0; i < gestures.Count; i++)
            {
                AkgfGestureData gesture = gestures[i];
                if (gesture == null || !gesture.IsUsable)
                {
                    continue;
                }

                gesture.EnsureValid();
                AkgfGestureSettings perGesture = gestureSettingsDatabase != null
                    ? gestureSettingsDatabase.GetSettings(gesture.gestureName, AkgfGestureKind.StaticPose)
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
                float gestureDistance = DistanceToGesture(currentPose, gesture, settings, mirrorMode, out wasMirrored);
                if (!AkgfMath.IsFinite(gestureDistance))
                {
                    continue;
                }

                float similarity = DistanceToSimilarity(gestureDistance, settings.similaritySensitivity);
                if (!best.isValid || similarity > best.similarity)
                {
                    best = new AkgfGestureMatchResult
                    {
                        gestureName = gesture.gestureName,
                        similarity = similarity,
                        distance = gestureDistance,
                        sampleCount = gesture.samples.Count,
                        isValid = true,
                        gestureKind = AkgfGestureKind.StaticPose,
                        phase = AkgfGesturePhase.Detected,
                        groupName = perGesture != null ? perGesture.groupName : string.Empty,
                        priority = perGesture != null ? perGesture.priority : 0,
                        trackingQuality = 1f,
                        wasMirrored = wasMirrored
                    };
                }
            }

            return best;
        }

        public static float DistanceToSimilarity(float distance, float sensitivity)
        {
            if (!AkgfMath.IsFinite(distance) || distance < 0f)
            {
                return 0f;
            }

            // New percentage-friendly confidence formula.
            // Old formula was exp(-distance / sensitivity), which collapses useful Kinect matches
            // into very small numbers such as 0.01 - 0.05. This inverse-distance curve keeps the
            // same ordering (smaller distance still wins), but produces intuitive confidence values:
            // 0.55 = 55%, 0.80 = 80%, etc.
            sensitivity = Mathf.Max(0.0001f, sensitivity);
            return Mathf.Clamp01(sensitivity / (sensitivity + distance));
        }

        public static float SimilarityToPercent(float similarity)
        {
            return Mathf.Clamp01(similarity) * 100f;
        }

        public static string FormatSimilarityPercent(float similarity)
        {
            return $"{SimilarityToPercent(similarity):0}%";
        }

        private static float DistanceToGesture(
            AkgfNormalizedPose currentPose,
            AkgfGestureData gesture,
            AkgfGestureMatcherSettings settings,
            AkgfMirrorMode mirrorMode,
            out bool wasMirrored)
        {
            wasMirrored = false;
            int k = Mathf.Clamp(settings.nearestSampleCount, 1, Mathf.Max(1, gesture.samples.Count));
            float[] nearest = new float[k];
            for (int i = 0; i < k; i++)
            {
                nearest[i] = float.PositiveInfinity;
            }

            AkgfNormalizedPose mirroredPose = null;
            if (mirrorMode == AkgfMirrorMode.AllowMirrored || mirrorMode == AkgfMirrorMode.AnySide)
            {
                mirroredPose = AkgfPoseMirrorUtility.CreateMirroredPose(currentPose);
            }

            bool localMirrored = false;
            for (int i = 0; i < gesture.samples.Count; i++)
            {
                AkgfNormalizedPose sample = gesture.samples[i];
                if (sample == null || !sample.IsValid)
                {
                    continue;
                }

                float d = PoseDistance(currentPose, sample, settings);
                float bestForSample = d;
                bool sampleMirrored = false;

                if (mirroredPose != null)
                {
                    float mirroredDistance = PoseDistance(mirroredPose, sample, settings);
                    if (mirroredDistance < bestForSample)
                    {
                        bestForSample = mirroredDistance;
                        sampleMirrored = true;
                    }
                }

                if (sampleMirrored)
                {
                    localMirrored = true;
                }

                InsertNearest(nearest, bestForSample);
            }

            float sum = 0f;
            int count = 0;
            for (int i = 0; i < nearest.Length; i++)
            {
                if (AkgfMath.IsFinite(nearest[i]))
                {
                    sum += nearest[i];
                    count++;
                }
            }

            wasMirrored = localMirrored;
            return count > 0 ? sum / count : float.PositiveInfinity;
        }

        private static void InsertNearest(float[] nearest, float distance)
        {
            if (!AkgfMath.IsFinite(distance) || nearest == null || nearest.Length == 0)
            {
                return;
            }

            for (int i = 0; i < nearest.Length; i++)
            {
                if (distance >= nearest[i])
                {
                    continue;
                }

                for (int j = nearest.Length - 1; j > i; j--)
                {
                    nearest[j] = nearest[j - 1];
                }

                nearest[i] = distance;
                return;
            }
        }

        public static float PoseDistance(AkgfNormalizedPose a, AkgfNormalizedPose b, AkgfGestureMatcherSettings settings)
        {
            if (a == null || b == null || !a.IsValid || !b.IsValid)
            {
                return float.PositiveInfinity;
            }

            settings = settings ?? new AkgfGestureMatcherSettings();
            float weightedSum = 0f;
            float weightSum = 0f;

            for (int joint = 0; joint < AkgfJointIdExtensions.JointCount; joint++)
            {
                AkgfJointId jointId = (AkgfJointId)joint;
                float jointWeight = JointWeight(jointId, settings) * a.weights[joint] * b.weights[joint];
                if (jointWeight <= 0.0001f)
                {
                    continue;
                }

                int baseIndex = joint * 3;
                float dx = a.values[baseIndex] - b.values[baseIndex];
                float dy = a.values[baseIndex + 1] - b.values[baseIndex + 1];
                float dz = a.values[baseIndex + 2] - b.values[baseIndex + 2];
                float squaredDistance = dx * dx + dy * dy + dz * dz;

                if (!AkgfMath.IsFinite(squaredDistance))
                {
                    continue;
                }

                weightedSum += squaredDistance * jointWeight;
                weightSum += jointWeight;
            }

            if (weightSum <= 0.0001f)
            {
                return float.PositiveInfinity;
            }

            return Mathf.Sqrt(weightedSum / weightSum);
        }

        private static float JointWeight(AkgfJointId jointId, AkgfGestureMatcherSettings settings)
        {
            float weight = 1f;

            if (settings.reduceLowerBodyWeight && jointId.IsLowerBody())
            {
                weight *= settings.lowerBodyWeightMultiplier;
            }

            if (settings.boostArmAndHandWeight && jointId.IsHandOrArm())
            {
                weight *= settings.armAndHandWeightMultiplier;
            }

            if (jointId == AkgfJointId.Head || jointId == AkgfJointId.Nose || jointId == AkgfJointId.Neck)
            {
                weight *= 1.25f;
            }

            return weight;
        }
    }
}
