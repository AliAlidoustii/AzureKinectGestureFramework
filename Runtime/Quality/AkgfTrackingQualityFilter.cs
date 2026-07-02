using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfTrackingQualityFilter
    {
        [Range(0f, 1f)] public float defaultMinimumJointConfidence = 0.15f;
        [Range(0f, 1f)] public float minimumOverallBodyQuality = 0.20f;
        public bool requireCoreJoints = true;
        public bool useRequiredJointsFromGestureSettings = true;
        public bool reduceSimilarityWhenQualityIsLow = true;

        public float ComputeBodyQuality(AkgfTrackedBody body)
        {
            if (body == null || !body.IsTracked)
            {
                return 0f;
            }

            if (requireCoreJoints && !body.HasRequiredJoints(defaultMinimumJointConfidence,
                    AkgfJointId.Pelvis,
                    AkgfJointId.SpineChest,
                    AkgfJointId.Neck,
                    AkgfJointId.ShoulderLeft,
                    AkgfJointId.ShoulderRight))
            {
                return 0f;
            }

            float sum = 0f;
            int count = 0;
            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                AkgfJointId joint = (AkgfJointId)i;
                if (!body.HasJoint(joint, 0f))
                {
                    continue;
                }

                sum += Mathf.Clamp01(body.GetConfidence(joint));
                count++;
            }

            return count > 0 ? Mathf.Clamp01(sum / count) : 0f;
        }

        public float ComputeGestureQuality(AkgfTrackedBody body, AkgfGestureSettings settings)
        {
            if (body == null || !body.IsTracked)
            {
                return 0f;
            }

            if (settings == null || !useRequiredJointsFromGestureSettings || settings.requiredJoints == null || settings.requiredJoints.Count == 0)
            {
                return ComputeBodyQuality(body);
            }

            float sum = 0f;
            int count = 0;
            for (int i = 0; i < settings.requiredJoints.Count; i++)
            {
                AkgfJointId joint = settings.requiredJoints[i];
                if (!joint.IsValidJoint())
                {
                    continue;
                }

                if (!body.HasJoint(joint, 0f))
                {
                    return 0f;
                }

                sum += Mathf.Clamp01(body.GetConfidence(joint));
                count++;
            }

            return count > 0 ? Mathf.Clamp01(sum / count) : ComputeBodyQuality(body);
        }

        public bool Passes(AkgfTrackedBody body, AkgfGestureSettings settings, out float quality)
        {
            quality = ComputeGestureQuality(body, settings);
            float minimum = settings != null ? settings.minimumTrackingQuality : minimumOverallBodyQuality;
            return quality >= Mathf.Clamp01(minimum);
        }

        public float ApplyQualityPenalty(float similarity, float quality, AkgfGestureSettings settings)
        {
            if (!reduceSimilarityWhenQualityIsLow)
            {
                return similarity;
            }

            float strength = settings != null ? settings.qualityPenaltyStrength : 0.5f;
            strength = Mathf.Clamp01(strength);
            quality = Mathf.Clamp01(quality);
            float penaltyFactor = Mathf.Lerp(1f, quality, strength);
            return Mathf.Clamp01(similarity * penaltyFactor);
        }
    }
}
