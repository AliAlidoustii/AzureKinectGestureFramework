using System;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Current body frame copied from Azure Kinect or from a transform-based skeleton.
    /// Positions can be in meters, millimeters, local space, or world space; the normalizer removes scale and origin.
    /// </summary>
    [Serializable]
    public sealed class AkgfTrackedBody
    {
        public const int JointCount = AkgfJointIdExtensions.JointCount;

        private readonly Vector3[] jointPositions = new Vector3[JointCount];
        private readonly float[] jointConfidences = new float[JointCount];

        public bool IsTracked { get; private set; }
        public int BodyId { get; private set; }
        public double TimestampSeconds { get; private set; }

        public AkgfTrackedBody()
        {
            Clear();
        }

        public void Clear()
        {
            IsTracked = false;
            BodyId = -1;
            TimestampSeconds = 0.0;

            for (int i = 0; i < JointCount; i++)
            {
                jointPositions[i] = Vector3.zero;
                jointConfidences[i] = 0f;
            }
        }

        public void BeginFrame(int bodyId, double timestampSeconds)
        {
            IsTracked = true;
            BodyId = bodyId;
            TimestampSeconds = timestampSeconds;
        }

        public void SetJoint(AkgfJointId jointId, Vector3 position, float confidence = 1f)
        {
            if (!jointId.IsValidJoint())
            {
                return;
            }

            int index = (int)jointId;
            jointPositions[index] = position;
            jointConfidences[index] = Mathf.Clamp01(confidence);
        }

        public Vector3 GetJoint(AkgfJointId jointId)
        {
            if (!jointId.IsValidJoint())
            {
                return Vector3.zero;
            }

            return jointPositions[(int)jointId];
        }

        public float GetConfidence(AkgfJointId jointId)
        {
            if (!jointId.IsValidJoint())
            {
                return 0f;
            }

            return jointConfidences[(int)jointId];
        }

        public bool HasJoint(AkgfJointId jointId, float minConfidence)
        {
            if (!jointId.IsValidJoint())
            {
                return false;
            }

            Vector3 p = jointPositions[(int)jointId];
            return jointConfidences[(int)jointId] >= minConfidence &&
                   IsFinite(p.x) && IsFinite(p.y) && IsFinite(p.z);
        }

        public bool HasRequiredJoints(float minConfidence, params AkgfJointId[] joints)
        {
            if (!IsTracked)
            {
                return false;
            }

            for (int i = 0; i < joints.Length; i++)
            {
                if (!HasJoint(joints[i], minConfidence))
                {
                    return false;
                }
            }

            return true;
        }

        public void CopyPositionsTo(Vector3[] target)
        {
            if (target == null || target.Length < JointCount)
            {
                throw new ArgumentException($"Target array must contain at least {JointCount} elements.");
            }

            Array.Copy(jointPositions, target, JointCount);
        }

        public void CopyConfidencesTo(float[] target)
        {
            if (target == null || target.Length < JointCount)
            {
                throw new ArgumentException($"Target array must contain at least {JointCount} elements.");
            }

            Array.Copy(jointConfidences, target, JointCount);
        }

        public void CopyFrom(AkgfTrackedBody other)
        {
            if (other == null)
            {
                Clear();
                return;
            }

            IsTracked = other.IsTracked;
            BodyId = other.BodyId;
            TimestampSeconds = other.TimestampSeconds;
            Array.Copy(other.jointPositions, jointPositions, JointCount);
            Array.Copy(other.jointConfidences, jointConfidences, JointCount);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
