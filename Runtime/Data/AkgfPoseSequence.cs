using System;
using System.Collections.Generic;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfPoseSequence
    {
        public int frameCount;
        public float durationSeconds;
        public float sampleRate;
        public List<AkgfNormalizedPose> frames = new List<AkgfNormalizedPose>();

        public bool IsValid
        {
            get
            {
                if (frames == null || frames.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i] == null || !frames[i].IsValid)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void EnsureValid()
        {
            if (frames == null)
            {
                frames = new List<AkgfNormalizedPose>();
            }

            for (int i = frames.Count - 1; i >= 0; i--)
            {
                if (frames[i] == null)
                {
                    frames.RemoveAt(i);
                    continue;
                }

                frames[i].EnsureArrays();
            }

            frameCount = frames.Count;
            if (frameCount > 1 && durationSeconds <= 0f && sampleRate > 0f)
            {
                durationSeconds = frameCount / sampleRate;
            }
        }

        public AkgfPoseSequence Clone()
        {
            EnsureValid();
            AkgfPoseSequence clone = new AkgfPoseSequence
            {
                frameCount = frameCount,
                durationSeconds = durationSeconds,
                sampleRate = sampleRate,
                frames = new List<AkgfNormalizedPose>(frames.Count)
            };

            for (int i = 0; i < frames.Count; i++)
            {
                clone.frames.Add(frames[i].Clone());
            }

            return clone;
        }
    }
}
