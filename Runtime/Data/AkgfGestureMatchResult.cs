using System;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public struct AkgfGestureMatchResult
    {
        public string gestureName;
        public float similarity;
        public float distance;
        public int sampleCount;
        public bool isValid;
        public AkgfGestureKind gestureKind;
        public AkgfGesturePhase phase;
        public string groupName;
        public int priority;
        public float trackingQuality;
        public bool wasMirrored;
        public int bodyId;
        public Vector3 bodyPosition;

        public float confidencePercent => AkgfGestureMatcher.SimilarityToPercent(similarity);

        public static AkgfGestureMatchResult None
        {
            get
            {
                return new AkgfGestureMatchResult
                {
                    gestureName = string.Empty,
                    similarity = 0f,
                    distance = float.PositiveInfinity,
                    sampleCount = 0,
                    isValid = false,
                    gestureKind = AkgfGestureKind.StaticPose,
                    phase = AkgfGesturePhase.Detected,
                    groupName = string.Empty,
                    priority = 0,
                    trackingQuality = 0f,
                    wasMirrored = false,
                    bodyId = -1,
                    bodyPosition = Vector3.zero
                };
            }
        }
    }
}
