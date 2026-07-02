using System;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Body-local, scale-normalized pose. values contains x,y,z for each joint in AkgfJointId order.
    /// </summary>
    [Serializable]
    public sealed class AkgfNormalizedPose
    {
        public int jointCount = AkgfJointIdExtensions.JointCount;
        public float[] values = new float[AkgfJointIdExtensions.JointCount * 3];
        public float[] weights = new float[AkgfJointIdExtensions.JointCount];

        public bool IsValid
        {
            get
            {
                return jointCount == AkgfJointIdExtensions.JointCount &&
                       values != null && values.Length == jointCount * 3 &&
                       weights != null && weights.Length == jointCount;
            }
        }

        public Vector3 GetJoint(AkgfJointId jointId)
        {
            if (!jointId.IsValidJoint() || !IsValid)
            {
                return Vector3.zero;
            }

            int baseIndex = (int)jointId * 3;
            return new Vector3(values[baseIndex], values[baseIndex + 1], values[baseIndex + 2]);
        }

        public void SetJoint(AkgfJointId jointId, Vector3 value, float weight)
        {
            if (!jointId.IsValidJoint())
            {
                return;
            }

            EnsureArrays();
            int baseIndex = (int)jointId * 3;
            values[baseIndex] = value.x;
            values[baseIndex + 1] = value.y;
            values[baseIndex + 2] = value.z;
            weights[(int)jointId] = Mathf.Clamp01(weight);
        }

        public AkgfNormalizedPose Clone()
        {
            EnsureArrays();
            AkgfNormalizedPose clone = new AkgfNormalizedPose();
            Array.Copy(values, clone.values, values.Length);
            Array.Copy(weights, clone.weights, weights.Length);
            return clone;
        }

        public void EnsureArrays()
        {
            jointCount = AkgfJointIdExtensions.JointCount;

            if (values == null || values.Length != jointCount * 3)
            {
                values = new float[jointCount * 3];
            }

            if (weights == null || weights.Length != jointCount)
            {
                weights = new float[jointCount];
            }
        }
    }
}
