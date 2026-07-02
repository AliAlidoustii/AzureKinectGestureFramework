#if AKGF_MICROSOFT_AZURE_KINECT_SAMPLE
using Microsoft.Azure.Kinect.BodyTracking;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Bridge for Microsoft's official sample_unity_bodytracking project.
    /// Enable it by adding AKGF_MICROSOFT_AZURE_KINECT_SAMPLE to Player Settings > Scripting Define Symbols.
    /// Then call SetFromOfficialSampleBody(skeleton) from TrackerHandler.renderSkeleton or updateTracker.
    /// </summary>
    public sealed class AkgfOfficialSampleBodySource : MonoBehaviour, IAkgfSkeletonSource
    {
        public bool invertYToMatchMicrosoftUnitySample = true;

        private readonly AkgfTrackedBody currentBody = new AkgfTrackedBody();
        private bool hasFrame;

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            body = hasFrame ? currentBody : null;
            return hasFrame && currentBody.IsTracked;
        }

        public void SetFromOfficialSampleBody(global::Body body)
        {
            currentBody.Clear();
            currentBody.BeginFrame((int)body.Id, Time.time);

            int length = Mathf.Min(body.Length, AkgfJointIdExtensions.JointCount);
            for (int i = 0; i < length; i++)
            {
                System.Numerics.Vector3 p = body.JointPositions3D[i];
                Vector3 unityPosition = new Vector3(p.X, invertYToMatchMicrosoftUnitySample ? -p.Y : p.Y, p.Z);
                float confidence = ConfidenceToFloat(body.JointPrecisions[i]);
                currentBody.SetJoint((AkgfJointId)i, unityPosition, confidence);
            }

            hasFrame = length >= AkgfJointIdExtensions.JointCount;
        }

        public void SetFromBackgroundData(global::BackgroundData frameData)
        {
            if (frameData == null || frameData.NumOfBodies == 0 || frameData.Bodies == null)
            {
                hasFrame = false;
                currentBody.Clear();
                return;
            }

            int closestIndex = FindClosestBodyIndex(frameData);
            if (closestIndex < 0)
            {
                hasFrame = false;
                currentBody.Clear();
                return;
            }

            SetFromOfficialSampleBody(frameData.Bodies[closestIndex]);
        }

        private static int FindClosestBodyIndex(global::BackgroundData frameData)
        {
            int closestIndex = -1;
            float closestDistance = float.PositiveInfinity;
            int count = Mathf.Min((int)frameData.NumOfBodies, frameData.Bodies.Length);

            for (int i = 0; i < count; i++)
            {
                global::Body body = frameData.Bodies[i];
                if (body.JointPositions3D == null || body.JointPositions3D.Length <= (int)AkgfJointId.Pelvis)
                {
                    continue;
                }

                System.Numerics.Vector3 pelvis = body.JointPositions3D[(int)AkgfJointId.Pelvis];
                float distance = pelvis.Length();
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
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
