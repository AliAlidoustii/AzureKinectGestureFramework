#if AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
using System.Collections.Generic;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// AKGF-owned replacement for the Microsoft sample BackgroundData container.
    /// Stores only what the gesture framework needs: body ids, joint positions, rotations, confidences, and timestamps.
    /// </summary>
    public sealed class AkgfKinectFrameData
    {
        public double timestampSeconds;
        public readonly List<AkgfKinectBodyData> bodies = new List<AkgfKinectBodyData>(8);

        public int BodyCount => bodies.Count;

        public void Clear()
        {
            timestampSeconds = 0;
            bodies.Clear();
        }

        public AkgfKinectFrameData DeepCopy()
        {
            AkgfKinectFrameData copy = new AkgfKinectFrameData();
            copy.timestampSeconds = timestampSeconds;

            for (int b = 0; b < bodies.Count; b++)
            {
                AkgfKinectBodyData source = bodies[b];
                AkgfKinectBodyData body = new AkgfKinectBodyData
                {
                    id = source.id,
                    timestampSeconds = source.timestampSeconds
                };

                for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
                {
                    body.jointPositions[i] = source.jointPositions[i];
                    body.jointRotations[i] = source.jointRotations[i];
                    body.jointConfidences[i] = source.jointConfidences[i];
                }

                copy.bodies.Add(body);
            }

            return copy;
        }
    }
}
#endif
