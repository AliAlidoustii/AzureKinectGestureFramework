#if AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
using Microsoft.Azure.Kinect.BodyTracking;
using UnityEngine;
using K4ABody = Microsoft.Azure.Kinect.BodyTracking.Body;
using K4ASkeleton = Microsoft.Azure.Kinect.BodyTracking.Skeleton;
using K4AJoint = Microsoft.Azure.Kinect.BodyTracking.Joint;
using K4AJointId = Microsoft.Azure.Kinect.BodyTracking.JointId;
using K4AJointConfidenceLevel = Microsoft.Azure.Kinect.BodyTracking.JointConfidenceLevel;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// AKGF-owned body data container inspired by the Microsoft Unity sample body data object.
    /// It is intentionally prefixed/namespaced so it does not collide with Microsoft's Body.cs.
    /// </summary>
    public sealed class AkgfKinectBodyData
    {
        public int id;
        public double timestampSeconds;
        public readonly Vector3[] jointPositions = new Vector3[AkgfJointIdExtensions.JointCount];
        public readonly Quaternion[] jointRotations = new Quaternion[AkgfJointIdExtensions.JointCount];
        public readonly float[] jointConfidences = new float[AkgfJointIdExtensions.JointCount];

        public void CopyFromSdkBody(
            K4ABody sdkBody,
            int bodyId,
            double timestamp,
            bool convertMillimetersToMeters,
            bool invertYForUnity,
            bool invertZForUnity)
        {
            id = bodyId;
            timestampSeconds = timestamp;

            K4ASkeleton skeleton = sdkBody.Skeleton;
            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                K4AJoint joint = skeleton.GetJoint((K4AJointId)i);
                Vector3 p = new Vector3(joint.Position.X, joint.Position.Y, joint.Position.Z);

                if (convertMillimetersToMeters)
                {
                    p *= 0.001f;
                }

                if (invertYForUnity)
                {
                    p.y = -p.y;
                }

                if (invertZForUnity)
                {
                    p.z = -p.z;
                }

                jointPositions[i] = p;
                jointRotations[i] = new Quaternion(
                    joint.Quaternion.X,
                    joint.Quaternion.Y,
                    joint.Quaternion.Z,
                    joint.Quaternion.W);
                jointConfidences[i] = ConfidenceToFloat(joint.ConfidenceLevel);
            }
        }

        public void CopyToTrackedBody(AkgfTrackedBody target)
        {
            if (target == null)
            {
                return;
            }

            target.BeginFrame(id, timestampSeconds);
            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                target.SetJoint((AkgfJointId)i, jointPositions[i], jointConfidences[i]);
            }
        }

        public AkgfTrackedBody ToTrackedBody()
        {
            AkgfTrackedBody trackedBody = new AkgfTrackedBody();
            CopyToTrackedBody(trackedBody);
            return trackedBody;
        }

        private static float ConfidenceToFloat(K4AJointConfidenceLevel confidence)
        {
            switch (confidence)
            {
                case K4AJointConfidenceLevel.High:
                    return 1f;
                case K4AJointConfidenceLevel.Medium:
                    return 0.75f;
                case K4AJointConfidenceLevel.Low:
                    return 0.35f;
                case K4AJointConfidenceLevel.None:
                default:
                    return 0f;
            }
        }
    }
}
#endif
