using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Diagnostic helper: prints the current recognizer candidates directly, even if the
    /// coordinator blocks them. Use this only while debugging.
    /// </summary>
    public sealed class AkgfCandidateDebugPrinter : MonoBehaviour
    {
        public AkgfGestureRecognizer staticPoseRecognizer;
        public AkgfSequenceGestureRecognizer sequenceRecognizer;
        public bool autoFindReferences = true;
        public bool printStaticCandidates = true;
        public bool printSequenceCandidates = true;
        public float printIntervalSeconds = 0.25f;

        private string lastStaticName = string.Empty;
        private string lastSequenceName = string.Empty;
        private float lastPrintTime = -999f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (autoFindReferences)
            {
                ResolveReferences();
            }

            if (Time.time - lastPrintTime < Mathf.Max(0.02f, printIntervalSeconds))
            {
                return;
            }

            bool printed = false;

            if (printStaticCandidates && staticPoseRecognizer != null)
            {
                AkgfGestureMatchResult match = staticPoseRecognizer.LastMatch;
                if (match.isValid && !string.IsNullOrWhiteSpace(match.gestureName) &&
                    !string.Equals(lastStaticName, match.gestureName, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[AKGF STATIC CANDIDATE] {match.gestureName} similarity={match.similarity:0.00} thresholdCheck=see recognizer/settings");
                    lastStaticName = match.gestureName;
                    printed = true;
                }
            }

            if (printSequenceCandidates && sequenceRecognizer != null)
            {
                AkgfGestureMatchResult match = sequenceRecognizer.LastMatch;
                if (match.isValid && !string.IsNullOrWhiteSpace(match.gestureName) &&
                    !string.Equals(lastSequenceName, match.gestureName, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[AKGF SEQUENCE CANDIDATE] {match.gestureName} similarity={match.similarity:0.00} thresholdCheck=see recognizer/settings");
                    lastSequenceName = match.gestureName;
                    printed = true;
                }
            }

            if (printed)
            {
                lastPrintTime = Time.time;
            }
        }

        public void ResolveReferences()
        {
            if (staticPoseRecognizer == null)
            {
                staticPoseRecognizer = AkgfUnityObjectFinder.FindFirst<AkgfGestureRecognizer>();
            }

            if (sequenceRecognizer == null)
            {
                sequenceRecognizer = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureRecognizer>();
            }
        }
    }
}
