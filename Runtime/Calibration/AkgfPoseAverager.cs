using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public static class AkgfPoseAverager
    {
        public static AkgfNormalizedPose Average(IReadOnlyList<AkgfNormalizedPose> poses)
        {
            if (poses == null || poses.Count == 0)
            {
                return null;
            }

            AkgfNormalizedPose result = new AkgfNormalizedPose();
            result.EnsureArrays();
            float[] totals = new float[result.values.Length];
            float[] weights = new float[result.weights.Length];

            for (int p = 0; p < poses.Count; p++)
            {
                AkgfNormalizedPose pose = poses[p];
                if (pose == null || !pose.IsValid)
                {
                    continue;
                }

                for (int j = 0; j < AkgfJointIdExtensions.JointCount; j++)
                {
                    float weight = Mathf.Clamp01(pose.weights[j]);
                    int bi = j * 3;
                    totals[bi] += pose.values[bi] * weight;
                    totals[bi + 1] += pose.values[bi + 1] * weight;
                    totals[bi + 2] += pose.values[bi + 2] * weight;
                    weights[j] += weight;
                }
            }

            for (int j = 0; j < AkgfJointIdExtensions.JointCount; j++)
            {
                int bi = j * 3;
                if (weights[j] > 0.0001f)
                {
                    result.values[bi] = totals[bi] / weights[j];
                    result.values[bi + 1] = totals[bi + 1] / weights[j];
                    result.values[bi + 2] = totals[bi + 2] / weights[j];
                    result.weights[j] = Mathf.Clamp01(weights[j] / poses.Count);
                }
                else
                {
                    result.values[bi] = 0f;
                    result.values[bi + 1] = 0f;
                    result.values[bi + 2] = 0f;
                    result.weights[j] = 0f;
                }
            }

            return result;
        }
    }
}
