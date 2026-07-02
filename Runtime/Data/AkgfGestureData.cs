using System;
using System.Collections.Generic;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfGestureData
    {
        public string gestureName = "NewGesture";
        public string createdUtc = string.Empty;
        public string modifiedUtc = string.Empty;
        public string jointOrder = "Microsoft.Azure.Kinect.BodyTracking.JointId / AKGF v1";
        public string notes = string.Empty;
        public List<AkgfNormalizedPose> samples = new List<AkgfNormalizedPose>();

        public bool IsUsable
        {
            get
            {
                return !string.IsNullOrWhiteSpace(gestureName) && samples != null && samples.Count > 0;
            }
        }

        public void EnsureValid()
        {
            if (string.IsNullOrWhiteSpace(gestureName))
            {
                gestureName = "NewGesture";
            }

            if (samples == null)
            {
                samples = new List<AkgfNormalizedPose>();
            }

            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (samples[i] == null)
                {
                    samples.RemoveAt(i);
                    continue;
                }

                samples[i].EnsureArrays();
            }
        }
    }
}
