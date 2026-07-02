using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public static class AkgfPoseMirrorUtility
    {
        private static readonly Dictionary<AkgfJointId, AkgfJointId> SwapMap = new Dictionary<AkgfJointId, AkgfJointId>
        {
            { AkgfJointId.ClavicleLeft, AkgfJointId.ClavicleRight },
            { AkgfJointId.ShoulderLeft, AkgfJointId.ShoulderRight },
            { AkgfJointId.ElbowLeft, AkgfJointId.ElbowRight },
            { AkgfJointId.WristLeft, AkgfJointId.WristRight },
            { AkgfJointId.HandLeft, AkgfJointId.HandRight },
            { AkgfJointId.HandTipLeft, AkgfJointId.HandTipRight },
            { AkgfJointId.ThumbLeft, AkgfJointId.ThumbRight },
            { AkgfJointId.HipLeft, AkgfJointId.HipRight },
            { AkgfJointId.KneeLeft, AkgfJointId.KneeRight },
            { AkgfJointId.AnkleLeft, AkgfJointId.AnkleRight },
            { AkgfJointId.FootLeft, AkgfJointId.FootRight },
            { AkgfJointId.EyeLeft, AkgfJointId.EyeRight },
            { AkgfJointId.EarLeft, AkgfJointId.EarRight }
        };

        public static AkgfNormalizedPose CreateMirroredPose(AkgfNormalizedPose pose)
        {
            if (pose == null || !pose.IsValid)
            {
                return pose;
            }

            AkgfNormalizedPose mirrored = new AkgfNormalizedPose();
            mirrored.EnsureArrays();

            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                AkgfJointId targetJoint = (AkgfJointId)i;
                AkgfJointId sourceJoint = GetSwapSource(targetJoint);
                Vector3 p = pose.GetJoint(sourceJoint);
                p.x = -p.x;
                mirrored.SetJoint(targetJoint, p, pose.weights[(int)sourceJoint]);
            }

            return mirrored;
        }

        public static AkgfPoseSequence CreateMirroredSequence(AkgfPoseSequence sequence)
        {
            if (sequence == null || !sequence.IsValid)
            {
                return sequence;
            }

            AkgfPoseSequence mirrored = new AkgfPoseSequence
            {
                durationSeconds = sequence.durationSeconds,
                sampleRate = sequence.sampleRate,
                frames = new List<AkgfNormalizedPose>()
            };

            for (int i = 0; i < sequence.frames.Count; i++)
            {
                mirrored.frames.Add(CreateMirroredPose(sequence.frames[i]));
            }

            mirrored.EnsureValid();
            return mirrored;
        }

        private static AkgfJointId GetSwapSource(AkgfJointId targetJoint)
        {
            foreach (KeyValuePair<AkgfJointId, AkgfJointId> pair in SwapMap)
            {
                if (pair.Value == targetJoint)
                {
                    return pair.Key;
                }
            }

            if (SwapMap.TryGetValue(targetJoint, out AkgfJointId source))
            {
                return source;
            }

            return targetJoint;
        }
    }
}
