using UnityEngine;

namespace AzureKinectGestureFramework
{
    public static class AkgfCalibrationProcessor
    {
        /// <summary>
        /// Converts a normalized pose into a calibrated feature pose by expressing selected joints as offsets from the user's neutral pose.
        /// Torso anchor joints are kept mostly absolute so old uncalibrated templates still behave reasonably.
        /// </summary>
        public static AkgfNormalizedPose Apply(AkgfNormalizedPose pose, AkgfCalibrationProfile profile, float strength)
        {
            if (pose == null || !pose.IsValid || profile == null || !profile.IsUsable || strength <= 0f)
            {
                return pose;
            }

            strength = Mathf.Clamp01(strength);
            profile.EnsureValid();

            AkgfNormalizedPose calibrated = pose.Clone();
            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                AkgfJointId joint = (AkgfJointId)i;
                float influence = GetJointCalibrationInfluence(joint) * strength;
                if (influence <= 0f)
                {
                    continue;
                }

                Vector3 current = pose.GetJoint(joint);
                Vector3 neutral = profile.neutralPose.GetJoint(joint);
                Vector3 neutralRelative = current - neutral;
                Vector3 blended = Vector3.Lerp(current, neutralRelative, influence);
                calibrated.SetJoint(joint, blended, pose.weights[i]);
            }

            return calibrated;
        }

        private static float GetJointCalibrationInfluence(AkgfJointId joint)
        {
            if (joint == AkgfJointId.Head || joint == AkgfJointId.Nose || joint == AkgfJointId.EyeLeft || joint == AkgfJointId.EyeRight || joint == AkgfJointId.EarLeft || joint == AkgfJointId.EarRight)
            {
                return 1.0f;
            }

            if (joint.IsHandOrArm())
            {
                return 0.55f;
            }

            if (joint == AkgfJointId.SpineNavel || joint == AkgfJointId.SpineChest || joint == AkgfJointId.Neck)
            {
                return 0.35f;
            }

            return 0.15f;
        }
    }
}
