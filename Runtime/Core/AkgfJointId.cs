using System;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Joint order matches Microsoft.Azure.Kinect.BodyTracking.JointId.
    /// Keep this enum stable because recorded gesture JSON depends on the numeric order.
    /// </summary>
    public enum AkgfJointId
    {
        Pelvis = 0,
        SpineNavel = 1,
        SpineChest = 2,
        Neck = 3,
        ClavicleLeft = 4,
        ShoulderLeft = 5,
        ElbowLeft = 6,
        WristLeft = 7,
        HandLeft = 8,
        HandTipLeft = 9,
        ThumbLeft = 10,
        ClavicleRight = 11,
        ShoulderRight = 12,
        ElbowRight = 13,
        WristRight = 14,
        HandRight = 15,
        HandTipRight = 16,
        ThumbRight = 17,
        HipLeft = 18,
        KneeLeft = 19,
        AnkleLeft = 20,
        FootLeft = 21,
        HipRight = 22,
        KneeRight = 23,
        AnkleRight = 24,
        FootRight = 25,
        Head = 26,
        Nose = 27,
        EyeLeft = 28,
        EarLeft = 29,
        EyeRight = 30,
        EarRight = 31,
        Count = 32
    }

    public static class AkgfJointIdExtensions
    {
        public const int JointCount = (int)AkgfJointId.Count;

        public static bool IsValidJoint(this AkgfJointId jointId)
        {
            return jointId >= AkgfJointId.Pelvis && jointId < AkgfJointId.Count;
        }

        public static bool IsLowerBody(this AkgfJointId jointId)
        {
            return jointId == AkgfJointId.HipLeft ||
                   jointId == AkgfJointId.KneeLeft ||
                   jointId == AkgfJointId.AnkleLeft ||
                   jointId == AkgfJointId.FootLeft ||
                   jointId == AkgfJointId.HipRight ||
                   jointId == AkgfJointId.KneeRight ||
                   jointId == AkgfJointId.AnkleRight ||
                   jointId == AkgfJointId.FootRight;
        }

        public static bool IsHandOrArm(this AkgfJointId jointId)
        {
            return jointId == AkgfJointId.ClavicleLeft ||
                   jointId == AkgfJointId.ShoulderLeft ||
                   jointId == AkgfJointId.ElbowLeft ||
                   jointId == AkgfJointId.WristLeft ||
                   jointId == AkgfJointId.HandLeft ||
                   jointId == AkgfJointId.HandTipLeft ||
                   jointId == AkgfJointId.ThumbLeft ||
                   jointId == AkgfJointId.ClavicleRight ||
                   jointId == AkgfJointId.ShoulderRight ||
                   jointId == AkgfJointId.ElbowRight ||
                   jointId == AkgfJointId.WristRight ||
                   jointId == AkgfJointId.HandRight ||
                   jointId == AkgfJointId.HandTipRight ||
                   jointId == AkgfJointId.ThumbRight;
        }

        public static string ToDisplayName(this AkgfJointId jointId)
        {
            return Enum.GetName(typeof(AkgfJointId), jointId) ?? jointId.ToString();
        }
    }
}
