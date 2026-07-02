using System;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Clean event payload used by the high-level API, logger, profiler, and demo scripts.
    /// bodyId is -1 for single-user mode.
    /// </summary>
    [Serializable]
    public sealed class AkgfGestureEventData
    {
        public int bodyId = -1;
        public string gestureName = string.Empty;
        public float confidence;
        public AkgfGestureKind gestureKind = AkgfGestureKind.StaticPose;
        public AkgfGesturePhase phase = AkgfGesturePhase.Detected;
        public string groupName = string.Empty;
        public int priority;
        public float trackingQuality;
        public bool wasMirrored;
        public Vector3 bodyPosition;
        public float unityTimeSeconds;
        public string mode = "SingleUser";

        public static AkgfGestureEventData FromMatch(AkgfGestureMatchResult match, AkgfTrackingMode trackingMode)
        {
            return new AkgfGestureEventData
            {
                bodyId = match.bodyId,
                gestureName = match.gestureName,
                confidence = match.similarity,
                gestureKind = match.gestureKind,
                phase = match.phase,
                groupName = match.groupName,
                priority = match.priority,
                trackingQuality = match.trackingQuality,
                wasMirrored = match.wasMirrored,
                bodyPosition = match.bodyPosition,
                unityTimeSeconds = Time.time,
                mode = trackingMode.ToString()
            };
        }
    }
}
