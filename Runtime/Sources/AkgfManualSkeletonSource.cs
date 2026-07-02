using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Simple push-based source. Use this when your existing Kinect code already has the 32 joint positions.
    /// Call BeginFrame/SetJoint/EndFrame from your Kinect update code.
    /// </summary>
    public sealed class AkgfManualSkeletonSource : MonoBehaviour, IAkgfSkeletonSource
    {
        private readonly AkgfTrackedBody currentBody = new AkgfTrackedBody();
        private bool hasFrame;

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            body = hasFrame ? currentBody : null;
            return hasFrame && currentBody.IsTracked;
        }

        public void BeginFrame(int bodyId = 0)
        {
            currentBody.Clear();
            currentBody.BeginFrame(bodyId, Time.time);
            hasFrame = true;
        }

        public void SetJoint(AkgfJointId jointId, Vector3 position, float confidence = 1f)
        {
            currentBody.SetJoint(jointId, position, confidence);
        }

        public void SetAllJoints(Vector3[] positions, float[] confidences = null, int bodyId = 0)
        {
            if (positions == null || positions.Length < AkgfJointIdExtensions.JointCount)
            {
                hasFrame = false;
                currentBody.Clear();
                return;
            }

            BeginFrame(bodyId);
            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                float confidence = confidences != null && confidences.Length > i ? confidences[i] : 1f;
                currentBody.SetJoint((AkgfJointId)i, positions[i], confidence);
            }
        }

        public void EndFrame()
        {
            hasFrame = currentBody.IsTracked;
        }

        public void ClearFrame()
        {
            hasFrame = false;
            currentBody.Clear();
        }
    }
}
