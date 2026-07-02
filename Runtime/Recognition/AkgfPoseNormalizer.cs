using System;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfPoseNormalizerSettings
    {
        [Range(0f, 1f)] public float minimumRequiredConfidence = 0.15f;
        public float minimumBodyScale = 0.0001f;
        public bool mirrorX = false;
        public bool alignToShoulders = true;
        public bool useConfidenceAsWeight = true;
    }

    /// <summary>
    /// Converts raw body joints to body-local normalized coordinates.
    /// Origin: pelvis. X axis: left shoulder to right shoulder. Y axis: pelvis to upper spine/neck.
    /// Scale: average of stable torso/arm bone lengths.
    /// </summary>
    public sealed class AkgfPoseNormalizer
    {
        private readonly Vector3[] rawPositions = new Vector3[AkgfJointIdExtensions.JointCount];
        private readonly float[] rawConfidences = new float[AkgfJointIdExtensions.JointCount];

        public bool TryNormalize(AkgfTrackedBody body, AkgfPoseNormalizerSettings settings, out AkgfNormalizedPose normalizedPose)
        {
            normalizedPose = null;

            if (body == null || !body.IsTracked)
            {
                return false;
            }

            settings = settings ?? new AkgfPoseNormalizerSettings();

            if (!body.HasRequiredJoints(settings.minimumRequiredConfidence,
                    AkgfJointId.Pelvis,
                    AkgfJointId.SpineChest,
                    AkgfJointId.Neck,
                    AkgfJointId.ShoulderLeft,
                    AkgfJointId.ShoulderRight))
            {
                return false;
            }

            body.CopyPositionsTo(rawPositions);
            body.CopyConfidencesTo(rawConfidences);

            Vector3 origin = rawPositions[(int)AkgfJointId.Pelvis];
            Vector3 xAxis = Vector3.right;
            Vector3 yAxis = Vector3.up;
            Vector3 zAxis = Vector3.forward;

            if (settings.alignToShoulders)
            {
                Vector3 leftShoulder = rawPositions[(int)AkgfJointId.ShoulderLeft];
                Vector3 rightShoulder = rawPositions[(int)AkgfJointId.ShoulderRight];
                Vector3 neck = rawPositions[(int)AkgfJointId.Neck];
                Vector3 pelvis = rawPositions[(int)AkgfJointId.Pelvis];
                Vector3 chest = rawPositions[(int)AkgfJointId.SpineChest];

                Vector3 shoulderDirection = rightShoulder - leftShoulder;
                Vector3 upDirection = ((neck - pelvis) + (chest - pelvis)).normalized;

                if (!AkgfMath.TryNormalize(shoulderDirection, out xAxis))
                {
                    return false;
                }

                if (!AkgfMath.TryNormalize(upDirection, out yAxis))
                {
                    return false;
                }

                Vector3 forwardCandidate = Vector3.Cross(xAxis, yAxis);
                if (!AkgfMath.TryNormalize(forwardCandidate, out zAxis))
                {
                    return false;
                }

                yAxis = Vector3.Cross(zAxis, xAxis).normalized;
            }

            if (settings.mirrorX)
            {
                xAxis = -xAxis;
            }

            float scale = EstimateBodyScale(rawPositions, rawConfidences, settings.minimumRequiredConfidence);
            if (scale < settings.minimumBodyScale || !AkgfMath.IsFinite(scale))
            {
                return false;
            }

            normalizedPose = new AkgfNormalizedPose();
            normalizedPose.EnsureArrays();

            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                Vector3 relative = rawPositions[i] - origin;
                Vector3 local = settings.alignToShoulders
                    ? new Vector3(Vector3.Dot(relative, xAxis), Vector3.Dot(relative, yAxis), Vector3.Dot(relative, zAxis)) / scale
                    : relative / scale;

                if (!AkgfMath.IsFinite(local))
                {
                    local = Vector3.zero;
                }

                float weight = settings.useConfidenceAsWeight ? Mathf.Clamp01(rawConfidences[i]) : 1f;
                normalizedPose.SetJoint((AkgfJointId)i, local, weight);
            }

            return true;
        }

        private static float EstimateBodyScale(Vector3[] positions, float[] confidences, float minConfidence)
        {
            float sum = 0f;
            int count = 0;

            AddBone(AkgfJointId.ShoulderLeft, AkgfJointId.ShoulderRight, positions, confidences, minConfidence, ref sum, ref count);
            AddBone(AkgfJointId.Pelvis, AkgfJointId.SpineChest, positions, confidences, minConfidence, ref sum, ref count);
            AddBone(AkgfJointId.SpineChest, AkgfJointId.Neck, positions, confidences, minConfidence, ref sum, ref count);
            AddBone(AkgfJointId.ShoulderLeft, AkgfJointId.ElbowLeft, positions, confidences, minConfidence, ref sum, ref count);
            AddBone(AkgfJointId.ElbowLeft, AkgfJointId.WristLeft, positions, confidences, minConfidence, ref sum, ref count);
            AddBone(AkgfJointId.ShoulderRight, AkgfJointId.ElbowRight, positions, confidences, minConfidence, ref sum, ref count);
            AddBone(AkgfJointId.ElbowRight, AkgfJointId.WristRight, positions, confidences, minConfidence, ref sum, ref count);

            return count > 0 ? sum / count : 0f;
        }

        private static void AddBone(
            AkgfJointId a,
            AkgfJointId b,
            Vector3[] positions,
            float[] confidences,
            float minConfidence,
            ref float sum,
            ref int count)
        {
            int ia = (int)a;
            int ib = (int)b;
            if (confidences[ia] < minConfidence || confidences[ib] < minConfidence)
            {
                return;
            }

            float distance = Vector3.Distance(positions[ia], positions[ib]);
            if (distance > 0f && AkgfMath.IsFinite(distance))
            {
                sum += distance;
                count++;
            }
        }
    }
}
