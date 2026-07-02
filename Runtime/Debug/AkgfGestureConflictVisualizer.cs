using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Shows what the static recognizer, sequence recognizer, and coordinator are doing in the same frame.
    /// This is intentionally visual/debug-only; it does not change recognition results.
    /// </summary>
    public sealed class AkgfGestureConflictVisualizer : MonoBehaviour
    {
        [Header("References")]
        public AkgfGestureRecognizer staticRecognizer;
        public AkgfSequenceGestureRecognizer sequenceRecognizer;
        public AkgfGestureCoordinator coordinator;
        public AkgfMultiUserGestureManager multiUserManager;
        public AkgfGestureSystemApi api;
        public bool autoFindReferences = true;

        [Header("Overlay")]
        public bool showOverlay = true;
        public KeyCode toggleKey = KeyCode.F6;
        public Rect windowRect = new Rect(410, 16, 410, 250);
        public int recentEventLimit = 6;

        private readonly Queue<string> recentEvents = new Queue<string>();

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (api != null)
            {
                api.Gesture += HandleGesture;
            }
            else
            {
                if (coordinator != null)
                {
                    coordinator.GesturePhase += HandleMatch;
                }
                if (multiUserManager != null)
                {
                    multiUserManager.MultiUserGesturePhase += HandleMatch;
                }
            }
        }

        private void OnDisable()
        {
            if (api != null)
            {
                api.Gesture -= HandleGesture;
            }
            if (coordinator != null)
            {
                coordinator.GesturePhase -= HandleMatch;
            }
            if (multiUserManager != null)
            {
                multiUserManager.MultiUserGesturePhase -= HandleMatch;
            }
        }

        private void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                showOverlay = !showOverlay;
            }

            if (autoFindReferences)
            {
                ResolveReferences();
            }
        }

        public void ResolveReferences()
        {
            if (staticRecognizer == null)
            {
                staticRecognizer = AkgfUnityObjectFinder.FindFirst<AkgfGestureRecognizer>();
            }
            if (sequenceRecognizer == null)
            {
                sequenceRecognizer = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureRecognizer>();
            }
            if (coordinator == null)
            {
                coordinator = AkgfUnityObjectFinder.FindFirst<AkgfGestureCoordinator>();
            }
            if (multiUserManager == null)
            {
                multiUserManager = AkgfUnityObjectFinder.FindFirst<AkgfMultiUserGestureManager>();
            }
            if (api == null)
            {
                api = AkgfUnityObjectFinder.FindFirst<AkgfGestureSystemApi>();
            }
        }

        private void HandleGesture(AkgfGestureEventData data)
        {
            if (data == null)
            {
                return;
            }
            AddRecent($"{data.mode} body {data.bodyId}: {data.gestureName} {data.phase} {data.confidence:0.00}");
        }

        private void HandleMatch(AkgfGestureMatchResult match)
        {
            AddRecent($"Body {match.bodyId}: {match.gestureName} {match.phase} {match.similarity:0.00}");
        }

        private void AddRecent(string message)
        {
            recentEvents.Enqueue(message);
            while (recentEvents.Count > Mathf.Max(1, recentEventLimit))
            {
                recentEvents.Dequeue();
            }
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }
            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "AKGF Conflict Visualizer");
        }

        private void DrawWindow(int id)
        {
            AkgfGestureMatchResult staticMatch = staticRecognizer != null ? staticRecognizer.LastMatch : AkgfGestureMatchResult.None;
            AkgfGestureMatchResult sequenceMatch = sequenceRecognizer != null ? sequenceRecognizer.LastMatch : AkgfGestureMatchResult.None;
            AkgfGestureMatchResult output = coordinator != null ? coordinator.LastOutput : AkgfGestureMatchResult.None;

            GUILayout.Label($"Static candidate: {Format(staticMatch)}");
            GUILayout.Label($"Sequence candidate: {Format(sequenceMatch)}");
            GUILayout.Label($"Final output: {Format(output)}");
            if (coordinator != null && !string.IsNullOrWhiteSpace(coordinator.LastDecision))
            {
                GUILayout.Label("Decision: " + coordinator.LastDecision);
            }
            else
            {
                GUILayout.Label("Decision: " + ExplainDecision(staticMatch, sequenceMatch, output));
            }
            GUILayout.Space(4);
            GUILayout.Label($"MultiUser visible/active: {(multiUserManager != null ? multiUserManager.VisibleBodyCount : 0)} / {(multiUserManager != null ? multiUserManager.ActiveUserCount : 0)}");
            GUILayout.Space(6);
            GUILayout.Label("Recent events:");
            foreach (string item in recentEvents)
            {
                GUILayout.Label("• " + item);
            }
            GUILayout.Label("Toggle: F6");
            GUI.DragWindow();
        }

        private static string Format(AkgfGestureMatchResult match)
        {
            if (!match.isValid || string.IsNullOrWhiteSpace(match.gestureName))
            {
                return "none";
            }
            return $"{match.gestureName} ({match.gestureKind}) {match.similarity:0.00} phase={match.phase}";
        }

        private static string ExplainDecision(AkgfGestureMatchResult staticMatch, AkgfGestureMatchResult sequenceMatch, AkgfGestureMatchResult output)
        {
            if (!output.isValid)
            {
                if (staticMatch.isValid || sequenceMatch.isValid)
                {
                    return "candidate exists but was probably blocked by cooldown, threshold, group, or phase settings";
                }
                return "no valid candidate";
            }

            if (sequenceMatch.isValid && staticMatch.isValid && output.gestureKind == AkgfGestureKind.Sequence)
            {
                return "sequence won over static pose";
            }

            if (sequenceMatch.isValid && staticMatch.isValid && output.gestureKind == AkgfGestureKind.StaticPose)
            {
                return "static pose won by priority/similarity or sequence was blocked";
            }

            return "single candidate accepted";
        }
    }
}
