using UnityEngine;

namespace AzureKinectGestureFramework
{
    public static class AkgfMath
    {
        public static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool TryNormalize(Vector3 input, out Vector3 normalized, float epsilon = 1e-6f)
        {
            float magnitude = input.magnitude;
            if (magnitude <= epsilon || !IsFinite(magnitude))
            {
                normalized = Vector3.zero;
                return false;
            }

            normalized = input / magnitude;
            return true;
        }

        public static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Gesture";
            }

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string cleaned = value.Trim();
            for (int i = 0; i < invalid.Length; i++)
            {
                cleaned = cleaned.Replace(invalid[i], '_');
            }

            cleaned = cleaned.Replace(' ', '_');
            return string.IsNullOrWhiteSpace(cleaned) ? "Gesture" : cleaned;
        }
    }
}
