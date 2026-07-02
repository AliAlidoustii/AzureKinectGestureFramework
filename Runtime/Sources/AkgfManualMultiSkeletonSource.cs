using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Push-based multi-body source. Use this when your Kinect code already has all tracked bodies.
    /// Call BeginFrame(), BeginBody(bodyId), SetJoint(...), then EndFrame() every Kinect update.
    /// Implements IAkgfSkeletonSource too, returning the closest body for SingleUser mode.
    /// </summary>
    public sealed class AkgfManualMultiSkeletonSource : MonoBehaviour, IAkgfMultiSkeletonSource, IAkgfSkeletonSource
    {
        private readonly Dictionary<int, AkgfTrackedBody> bodiesById = new Dictionary<int, AkgfTrackedBody>();
        private readonly List<AkgfTrackedBody> scratchBodies = new List<AkgfTrackedBody>();
        private AkgfTrackedBody editingBody;

        public int BodyCount => bodiesById.Count;

        public void BeginFrame()
        {
            bodiesById.Clear();
            editingBody = null;
        }

        public void BeginBody(int bodyId)
        {
            editingBody = new AkgfTrackedBody();
            editingBody.BeginFrame(bodyId, Time.time);
            bodiesById[bodyId] = editingBody;
        }

        public void SetJoint(AkgfJointId jointId, Vector3 position, float confidence = 1f)
        {
            if (editingBody == null)
            {
                return;
            }

            editingBody.SetJoint(jointId, position, confidence);
        }

        public void SetAllJoints(Vector3[] positions, float[] confidences = null, int bodyId = 0)
        {
            if (positions == null || positions.Length < AkgfJointIdExtensions.JointCount)
            {
                return;
            }

            BeginBody(bodyId);
            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                float confidence = confidences != null && confidences.Length > i ? confidences[i] : 1f;
                SetJoint((AkgfJointId)i, positions[i], confidence);
            }
        }

        public void EndBody()
        {
            editingBody = null;
        }

        public void EndFrame()
        {
            editingBody = null;
        }

        public void ClearFrame()
        {
            bodiesById.Clear();
            editingBody = null;
        }

        public void GetTrackedBodies(List<AkgfTrackedBody> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            foreach (KeyValuePair<int, AkgfTrackedBody> pair in bodiesById)
            {
                if (pair.Value != null && pair.Value.IsTracked)
                {
                    results.Add(pair.Value);
                }
            }
        }

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            scratchBodies.Clear();
            GetTrackedBodies(scratchBodies);
            return AkgfSkeletonSourceUtility.TryGetClosestBody(scratchBodies, out body);
        }
    }
}
