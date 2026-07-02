using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Runtime overlay dedicated to MultiUser recognition. It shows whether MultiUser is receiving
    /// bodies, loading gesture databases, producing candidates, blocking candidates, and emitting
    /// API events. This is intentionally diagnostic-only.
    /// </summary>
    public sealed class AkgfMultiUserLiveDebugUI : MonoBehaviour
    {
        [Header("References")]
        public AkgfMultiUserGestureManager multiUserManager;
        public AkgfGestureSystemApi api;
        public bool autoFindReferences = true;

        [Header("Overlay")]
        public bool showOverlay = true;
        public KeyCode toggleKey = KeyCode.F9;
        public Rect windowRect = new Rect(840, 16, 520, 420);
        public int recentEventLimit = 8;

        private readonly Queue<string> recentEvents = new Queue<string>();

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
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
                Subscribe();
            }
        }

        public void ResolveReferences()
        {
            if (multiUserManager == null)
            {
                multiUserManager = AkgfUnityObjectFinder.FindFirst<AkgfMultiUserGestureManager>();
            }

            if (api == null)
            {
                api = AkgfUnityObjectFinder.FindFirst<AkgfGestureSystemApi>();
            }
        }

        private void Subscribe()
        {
            if (api != null)
            {
                api.MultiUserGesture -= HandleApiEvent;
                api.MultiUserGesture += HandleApiEvent;
            }
            else if (multiUserManager != null)
            {
                multiUserManager.MultiUserGesturePhase -= HandleRawEvent;
                multiUserManager.MultiUserGesturePhase += HandleRawEvent;
            }
        }

        private void Unsubscribe()
        {
            if (api != null)
            {
                api.MultiUserGesture -= HandleApiEvent;
            }

            if (multiUserManager != null)
            {
                multiUserManager.MultiUserGesturePhase -= HandleRawEvent;
            }
        }

        private void HandleApiEvent(AkgfGestureEventData data)
        {
            if (data == null)
            {
                return;
            }

            AddRecent($"API body {data.bodyId}: {data.gestureName} {data.gestureKind} {data.phase} {data.confidencePercent:0}% q={data.trackingQuality:0.00}");
        }

        private void HandleRawEvent(AkgfGestureMatchResult match)
        {
            AddRecent($"RAW body {match.bodyId}: {match.gestureName} {match.gestureKind} {match.phase} {AkgfGestureMatcher.FormatSimilarityPercent(match.similarity)} q={match.trackingQuality:0.00}");
        }

        private void AddRecent(string text)
        {
            recentEvents.Enqueue(text);
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

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "AKGF MultiUser Live Debug");
        }

        private void DrawWindow(int id)
        {
            if (multiUserManager == null)
            {
                GUILayout.Label("No AkgfMultiUserGestureManager found.");
                if (GUILayout.Button("Find Again"))
                {
                    ResolveReferences();
                }
                GUI.DragWindow();
                return;
            }

            GUILayout.Label($"Source: {(multiUserManager.HasMultiSource ? multiUserManager.MultiSourceName : "NONE")}");
            GUILayout.Label($"Bodies: raw {multiUserManager.LastRawBodyCount} | tracked {multiUserManager.LastTrackedBodyCount} | normalized {multiUserManager.LastNormalizedBodyCount} | visible {multiUserManager.VisibleBodyCount} | active {multiUserManager.ActiveUserCount}");
            GUILayout.Label($"Databases: static {multiUserManager.LoadedStaticGestureCount} | sequence {multiUserManager.LoadedSequenceGestureCount}");
            GUILayout.Label($"Static candidate: {Format(multiUserManager.LastStaticCandidate)}");
            GUILayout.Label($"Sequence candidate: {Format(multiUserManager.LastSequenceCandidate)}");
            GUILayout.Label($"Final output: {Format(multiUserManager.LastOutput)}");
            GUILayout.Label("Decision: " + (string.IsNullOrWhiteSpace(multiUserManager.LastDecision) ? "none" : multiUserManager.LastDecision));

            GUILayout.Space(6);
            GUILayout.Label("Summary:");
            GUILayout.TextArea(multiUserManager.LastDebugSummary ?? "none", GUILayout.MinHeight(44));

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load DBs"))
            {
                multiUserManager.ForceLoadDatabases();
            }
            if (GUILayout.Button("Clear Users"))
            {
                multiUserManager.ClearUsers();
            }
            GUILayout.EndHorizontal();

            multiUserManager.debugForceBestCandidateAsResult = GUILayout.Toggle(multiUserManager.debugForceBestCandidateAsResult, "DEBUG Force MultiUser Candidate As Result");
            multiUserManager.debugIgnoreQualityFilter = GUILayout.Toggle(multiUserManager.debugIgnoreQualityFilter, "DEBUG Ignore MultiUser Quality Filter");
            multiUserManager.holdLastDebugValues = GUILayout.Toggle(multiUserManager.holdLastDebugValues, "Hold Last Debug Candidate / Decision");
            GUILayout.Label($"Debug hold seconds: {multiUserManager.debugCandidateHoldSeconds:0.00}");
            multiUserManager.debugLogDiagnosticsToConsole = GUILayout.Toggle(multiUserManager.debugLogDiagnosticsToConsole, "Log MultiUser diagnostics to Console");

            GUILayout.Space(6);
            GUILayout.Label("Recent MultiUser events:");
            foreach (string item in recentEvents)
            {
                GUILayout.Label("• " + item);
            }

            GUILayout.Space(4);
            GUILayout.Label($"Toggle: {toggleKey}");
            GUI.DragWindow();
        }

        private static string Format(AkgfGestureMatchResult match)
        {
            if (!match.isValid || string.IsNullOrWhiteSpace(match.gestureName))
            {
                return "none";
            }

            return $"body {match.bodyId}: {match.gestureName} {match.gestureKind} {AkgfGestureMatcher.FormatSimilarityPercent(match.similarity)} q={match.trackingQuality:0.00} phase={match.phase}";
        }
    }
}
