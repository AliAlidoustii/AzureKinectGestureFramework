using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfCalibrationProfile
    {
        public string profileName = "DefaultUser";
        public string createdUtc = string.Empty;
        public string modifiedUtc = string.Empty;
        public string notes = "Neutral standing pose calibration.";
        public AkgfNormalizedPose neutralPose;
        public float shoulderWidth = 1f;
        public float torsoHeight = 1f;
        public float leftArmLength = 1f;
        public float rightArmLength = 1f;
        public float bodyScale = 1f;
        public int sampleCount = 0;

        public bool IsUsable
        {
            get
            {
                return neutralPose != null && neutralPose.IsValid && sampleCount > 0;
            }
        }

        public void EnsureValid()
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                profileName = "DefaultUser";
            }

            neutralPose?.EnsureArrays();
            shoulderWidth = Mathf.Max(0.0001f, shoulderWidth);
            torsoHeight = Mathf.Max(0.0001f, torsoHeight);
            leftArmLength = Mathf.Max(0.0001f, leftArmLength);
            rightArmLength = Mathf.Max(0.0001f, rightArmLength);
            bodyScale = Mathf.Max(0.0001f, bodyScale);
            sampleCount = Mathf.Max(0, sampleCount);
        }

        public static AkgfCalibrationProfile FromSamples(string profileName, IReadOnlyList<AkgfNormalizedPose> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                return null;
            }

            AkgfNormalizedPose average = AkgfPoseAverager.Average(samples);
            if (average == null || !average.IsValid)
            {
                return null;
            }

            AkgfCalibrationProfile profile = new AkgfCalibrationProfile
            {
                profileName = string.IsNullOrWhiteSpace(profileName) ? "DefaultUser" : profileName.Trim(),
                createdUtc = DateTime.UtcNow.ToString("o"),
                modifiedUtc = DateTime.UtcNow.ToString("o"),
                neutralPose = average,
                sampleCount = samples.Count
            };

            profile.shoulderWidth = Vector3.Distance(average.GetJoint(AkgfJointId.ShoulderLeft), average.GetJoint(AkgfJointId.ShoulderRight));
            profile.torsoHeight = Vector3.Distance(average.GetJoint(AkgfJointId.Pelvis), average.GetJoint(AkgfJointId.Neck));
            profile.leftArmLength = Vector3.Distance(average.GetJoint(AkgfJointId.ShoulderLeft), average.GetJoint(AkgfJointId.ElbowLeft)) +
                                    Vector3.Distance(average.GetJoint(AkgfJointId.ElbowLeft), average.GetJoint(AkgfJointId.WristLeft));
            profile.rightArmLength = Vector3.Distance(average.GetJoint(AkgfJointId.ShoulderRight), average.GetJoint(AkgfJointId.ElbowRight)) +
                                     Vector3.Distance(average.GetJoint(AkgfJointId.ElbowRight), average.GetJoint(AkgfJointId.WristRight));
            profile.bodyScale = (profile.shoulderWidth + profile.torsoHeight + profile.leftArmLength + profile.rightArmLength) / 4f;
            profile.EnsureValid();
            return profile;
        }
    }
}
