#if AKGF_MICROSOFT_AZURE_KINECT_DIRECT
using System.Collections.Generic;
using Microsoft.Azure.Kinect.BodyTracking;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Direct multi-body bridge for Microsoft.Azure.Kinect.BodyTracking.Body values.
    /// Enable AKGF_MICROSOFT_AZURE_KINECT_DIRECT and push all bodies every frame through SetFromSdkBodies().
    /// </summary>
    public sealed class AkgfAzureKinectSdkMultiBodySource : MonoBehaviour, IAkgfMultiSkeletonSource, IAkgfSkeletonSource
    {
        public bool convertMillimetersToMeters = true;
        public bool invertYForUnity = true;

        private readonly List<AkgfTrackedBody> currentBodies = new List<AkgfTrackedBody>(8);

        public void GetTrackedBodies(List<AkgfTrackedBody> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (int i = 0; i < currentBodies.Count; i++)
            {
                results.Add(currentBodies[i]);
            }
        }

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            return AkgfSkeletonSourceUtility.TryGetClosestBody(currentBodies, out body);
        }

        public void ClearFrame()
        {
            currentBodies.Clear();
        }

        public void SetFromSdkBodies(IReadOnlyList<Body> bodies)
        {
            currentBodies.Clear();
            if (bodies == null)
            {
                return;
            }

            for (int i = 0; i < bodies.Count; i++)
            {
                AddSdkBody(bodies[i]);
            }
        }

        public void AddSdkBody(Body body)
        {
            AkgfTrackedBody trackedBody = new AkgfTrackedBody();
            trackedBody.BeginFrame((int)body.Id, Time.time);
            CopySkeleton(body.Skeleton, trackedBody);
            currentBodies.Add(trackedBody);
        }

        private void CopySkeleton(Skeleton skeleton, AkgfTrackedBody trackedBody)
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

                trackedBody.SetJoint((AkgfJointId)i, p, ConfidenceToFloat(joint.ConfidenceLevel));
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
