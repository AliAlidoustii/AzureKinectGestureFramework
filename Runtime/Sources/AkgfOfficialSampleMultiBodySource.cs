#if AKGF_MICROSOFT_AZURE_KINECT_SAMPLE
using System.Collections.Generic;
using Microsoft.Azure.Kinect.BodyTracking;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Multi-body bridge for Microsoft's official sample_unity_bodytracking project.
    /// Enable AKGF_MICROSOFT_AZURE_KINECT_SAMPLE and call SetFromBackgroundData(frameData)
    /// after the sample receives its BackgroundData frame.
    /// </summary>
    public sealed class AkgfOfficialSampleMultiBodySource : MonoBehaviour, IAkgfMultiSkeletonSource, IAkgfSkeletonSource
    {
        public bool invertYToMatchMicrosoftUnitySample = true;

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

        public void SetFromBackgroundData(global::BackgroundData frameData)
        {
            currentBodies.Clear();
            if (frameData == null || frameData.NumOfBodies == 0 || frameData.Bodies == null)
            {
                return;
            }

            int count = Mathf.Min((int)frameData.NumOfBodies, frameData.Bodies.Length);
            for (int i = 0; i < count; i++)
            {
                AddOfficialSampleBody(frameData.Bodies[i]);
            }
        }

        public void AddOfficialSampleBody(global::Body body)
        {
            if (body.JointPositions3D == null || body.JointPrecisions == null)
            {
                return;
            }

            AkgfTrackedBody trackedBody = new AkgfTrackedBody();
            trackedBody.BeginFrame((int)body.Id, Time.time);

            int length = Mathf.Min(body.Length, AkgfJointIdExtensions.JointCount);
            for (int i = 0; i < length; i++)
            {
                System.Numerics.Vector3 p = body.JointPositions3D[i];
                Vector3 unityPosition = new Vector3(p.X, invertYToMatchMicrosoftUnitySample ? -p.Y : p.Y, p.Z);
                float confidence = ConfidenceToFloat(body.JointPrecisions[i]);
                trackedBody.SetJoint((AkgfJointId)i, unityPosition, confidence);
            }

            if (length >= AkgfJointIdExtensions.JointCount)
            {
                currentBodies.Add(trackedBody);
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
