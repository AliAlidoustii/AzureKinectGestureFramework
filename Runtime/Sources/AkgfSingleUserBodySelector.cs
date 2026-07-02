using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public enum AkgfSingleUserSelectionPolicy
    {
        ClosestToCamera,
        LowestBodyId,
        LockedFirstSeen
    }

    /// <summary>
    /// Converts an IAkgfMultiSkeletonSource into an IAkgfSkeletonSource by selecting one body.
    /// Useful when the same Kinect multi-body source should feed SingleUser mode.
    /// </summary>
    public sealed class AkgfSingleUserBodySelector : MonoBehaviour, IAkgfSkeletonSource
    {
        [Tooltip("A MonoBehaviour that implements IAkgfMultiSkeletonSource, for example AkgfManualMultiSkeletonSource.")]
        public MonoBehaviour multiSkeletonSourceBehaviour;
        public AkgfSingleUserSelectionPolicy selectionPolicy = AkgfSingleUserSelectionPolicy.ClosestToCamera;
        public float lockLostTimeoutSeconds = 1.0f;

        public int SelectedBodyId { get; private set; } = -1;

        private IAkgfMultiSkeletonSource multiSource;
        private readonly List<AkgfTrackedBody> bodies = new List<AkgfTrackedBody>(8);
        private float lastSelectedSeenTime = -9999f;

        private void Awake()
        {
            ResolveReferences();
        }

        public void ResolveReferences()
        {
            multiSource = multiSkeletonSourceBehaviour as IAkgfMultiSkeletonSource;
        }

        public void ClearLock()
        {
            SelectedBodyId = -1;
            lastSelectedSeenTime = -9999f;
        }

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            body = null;
            if (multiSource == null)
            {
                ResolveReferences();
            }

            if (multiSource == null)
            {
                return false;
            }

            bodies.Clear();
            multiSource.GetTrackedBodies(bodies);
            if (bodies.Count == 0)
            {
                if (Time.time - lastSelectedSeenTime > Mathf.Max(0f, lockLostTimeoutSeconds))
                {
                    SelectedBodyId = -1;
                }
                return false;
            }

            switch (selectionPolicy)
            {
                case AkgfSingleUserSelectionPolicy.LowestBodyId:
                    body = FindLowestBodyId(bodies);
                    break;
                case AkgfSingleUserSelectionPolicy.LockedFirstSeen:
                    body = FindLockedOrFirstSeen(bodies);
                    break;
                default:
                    AkgfSkeletonSourceUtility.TryGetClosestBody(bodies, out body);
                    break;
            }

            if (body == null || !body.IsTracked)
            {
                return false;
            }

            SelectedBodyId = body.BodyId;
            lastSelectedSeenTime = Time.time;
            return true;
        }

        private AkgfTrackedBody FindLowestBodyId(List<AkgfTrackedBody> sourceBodies)
        {
            AkgfTrackedBody result = null;
            int lowestId = int.MaxValue;
            for (int i = 0; i < sourceBodies.Count; i++)
            {
                AkgfTrackedBody candidate = sourceBodies[i];
                if (candidate == null || !candidate.IsTracked)
                {
                    continue;
                }

                if (candidate.BodyId < lowestId)
                {
                    lowestId = candidate.BodyId;
                    result = candidate;
                }
            }
            return result;
        }

        private AkgfTrackedBody FindLockedOrFirstSeen(List<AkgfTrackedBody> sourceBodies)
        {
            for (int i = 0; i < sourceBodies.Count; i++)
            {
                AkgfTrackedBody candidate = sourceBodies[i];
                if (candidate != null && candidate.IsTracked && candidate.BodyId == SelectedBodyId)
                {
                    return candidate;
                }
            }

            if (Time.time - lastSelectedSeenTime <= Mathf.Max(0f, lockLostTimeoutSeconds) && SelectedBodyId >= 0)
            {
                return null;
            }

            return sourceBodies.Count > 0 ? sourceBodies[0] : null;
        }
    }
}
