using System;
using System.Collections.Generic;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfSequenceGestureData
    {
        public string gestureName = "NewSequenceGesture";
        public string createdUtc = string.Empty;
        public string modifiedUtc = string.Empty;
        public string jointOrder = "Microsoft.Azure.Kinect.BodyTracking.JointId / AKGF v2 sequence";
        public string notes = string.Empty;
        public List<AkgfPoseSequence> samples = new List<AkgfPoseSequence>();

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
                gestureName = "NewSequenceGesture";
            }

            if (samples == null)
            {
                samples = new List<AkgfPoseSequence>();
            }

            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (samples[i] == null)
                {
                    samples.RemoveAt(i);
                    continue;
                }

                samples[i].EnsureValid();
                if (!samples[i].IsValid)
                {
                    samples.RemoveAt(i);
                }
            }
        }
    }
}
