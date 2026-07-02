using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfGestureSettings
    {
        public string gestureName = "NewGesture";
        public AkgfGestureKind gestureKind = AkgfGestureKind.Any;
        public bool enabled = true;
        public string groupName = "Default";

        [Header("Recognition")]
        [Range(0f, 1f)] public float minimumSimilarity = 0.60f;
        [Tooltip("Static poses use this as hold time. Sequence gestures use recognizer consecutive-match settings too.")]
        public float requiredStableSeconds = 0.20f;
        public float cooldownSeconds = 0.75f;
        public int priority = 0;
        public AkgfMirrorMode mirrorMode = AkgfMirrorMode.Strict;

        [Header("Tracking Quality")]
        [Range(0f, 1f)] public float minimumTrackingQuality = 0.25f;
        [Range(0f, 1f)] public float qualityPenaltyStrength = 0.50f;
        public List<AkgfJointId> requiredJoints = new List<AkgfJointId>();

        [Header("Event Behavior")]
        public bool fireOnEnter = true;
        public bool fireOnStay = false;
        public bool fireOnExit = false;
        public bool fireOnConfirmed = true;

        public bool Matches(string name, AkgfGestureKind kind, bool caseSensitive)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(gestureName))
            {
                return false;
            }

            bool kindMatches = gestureKind == AkgfGestureKind.Any || kind == AkgfGestureKind.Any || gestureKind == kind;
            if (!kindMatches)
            {
                return false;
            }

            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return string.Equals(gestureName.Trim(), name.Trim(), comparison);
        }

        public void EnsureValid()
        {
            if (string.IsNullOrWhiteSpace(gestureName))
            {
                gestureName = "NewGesture";
            }

            if (string.IsNullOrWhiteSpace(groupName))
            {
                groupName = "Default";
            }

            if (requiredJoints == null)
            {
                requiredJoints = new List<AkgfJointId>();
            }

            minimumSimilarity = Mathf.Clamp01(minimumSimilarity);
            minimumTrackingQuality = Mathf.Clamp01(minimumTrackingQuality);
            qualityPenaltyStrength = Mathf.Clamp01(qualityPenaltyStrength);
            requiredStableSeconds = Mathf.Max(0f, requiredStableSeconds);
            cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        }
    }
}
