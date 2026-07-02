#if AKGF_MICROSOFT_AZURE_KINECT_DIRECT
using Microsoft.Azure.Kinect.BodyTracking;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Direct bridge for Microsoft.Azure.Kinect.BodyTracking.Body or Skeleton.
    /// Enable it by adding AKGF_MICROSOFT_AZURE_KINECT_DIRECT to Player Settings > Scripting Define Symbols.
    /// </summary>
    public sealed class AkgfAzureKinectSdkBodySource : MonoBehaviour, IAkgfSkeletonSource
    {
        public bool convertMillimetersToMeters = true;
        public bool invertYForUnity = true;

        private readonly AkgfTrackedBody currentBody = new AkgfTrackedBody();
        private bool hasFrame;

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            body = hasFrame ? currentBody : null;
            return hasFrame && currentBody.IsTracked;
        }

        public void SetFromSdkBody(Microsoft.Azure.Kinect.BodyTracking.Body body)
        {
            currentBody.Clear();
            currentBody.BeginFrame((int)body.Id, Time.time);
            CopySkeleton(body.Skeleton);
            hasFrame = true;
        }

        public void SetFromSdkSkeleton(Skeleton skeleton, int bodyId = 0)
        {
            currentBody.Clear();
            currentBody.BeginFrame(bodyId, Time.time);
            CopySkeleton(skeleton);
            hasFrame = true;
        }

        private void CopySkeleton(Skeleton skeleton)
        {
            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                Joint joint = skeleton.GetJoint((JointId)i);
                Vector3 p = new Vector3(joint.Position.X, joint.Position.Y, joint.Position.Z);
                if (convertMillimetersToMeters)
                {
                    p /= 1000f;
                }

                if (invertYForUnity)
                {
                    p.y = -p.y;
                }

                currentBody.SetJoint((AkgfJointId)i, p, ConfidenceToFloat(joint.ConfidenceLevel));
            }
        }

        private static float ConfidenceToFloat(JointConfidenceLevel confidence)
        {
            switch (confidence)
            {
                case JointConfidenceLevel.High:
                    return 1f;
                case JointConfidenceLevel.Medium:
                    return 0.75f;
                case JointConfidenceLevel.Low:
                    return 0.35f;
                case JointConfidenceLevel.None:
                default:
                    return 0f;
            }
        }
    }
}
#endif
