using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public static class AkgfSkeletonSourceUtility
    {
        public static AkgfTrackedBody CloneBody(AkgfTrackedBody source)
        {
            AkgfTrackedBody clone = new AkgfTrackedBody();
            clone.CopyFrom(source);
            return clone;
        }

        public static bool TryGetClosestBody(IReadOnlyList<AkgfTrackedBody> bodies, out AkgfTrackedBody closestBody)
        {
            closestBody = null;
            if (bodies == null || bodies.Count == 0)
            {
                return false;
            }

            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < bodies.Count; i++)
            {
                AkgfTrackedBody body = bodies[i];
                if (body == null || !body.IsTracked)
                {
                    continue;
                }

                Vector3 pelvis = body.GetJoint(AkgfJointId.Pelvis);
                float distance = pelvis.sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestBody = body;
                }
            }

            return closestBody != null && closestBody.IsTracked;
        }
    }
}
