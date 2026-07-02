using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public static class AkgfDynamicTimeWarping
    {
        public static float Distance<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, int window, Func<T, T, float> distanceFunction)
        {
            if (a == null || b == null || a.Count == 0 || b.Count == 0 || distanceFunction == null)
            {
                return float.PositiveInfinity;
            }

            int n = a.Count;
            int m = b.Count;
            window = Mathf.Max(window, Mathf.Abs(n - m));
            float[,] dp = new float[n + 1, m + 1];

            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= m; j++)
                {
                    dp[i, j] = float.PositiveInfinity;
                }
            }

            dp[0, 0] = 0f;

            for (int i = 1; i <= n; i++)
            {
                int jStart = Mathf.Max(1, i - window);
                int jEnd = Mathf.Min(m, i + window);
                for (int j = jStart; j <= jEnd; j++)
                {
                    float cost = distanceFunction(a[i - 1], b[j - 1]);
                    if (!AkgfMath.IsFinite(cost))
                    {
                        continue;
                    }

                    float previous = Mathf.Min(dp[i - 1, j], Mathf.Min(dp[i, j - 1], dp[i - 1, j - 1]));
                    if (!AkgfMath.IsFinite(previous))
                    {
                        continue;
                    }

                    dp[i, j] = cost + previous;
                }
            }

            float total = dp[n, m];
            if (!AkgfMath.IsFinite(total))
            {
                return float.PositiveInfinity;
            }

            return total / Mathf.Max(n, m);
        }
    }
}
