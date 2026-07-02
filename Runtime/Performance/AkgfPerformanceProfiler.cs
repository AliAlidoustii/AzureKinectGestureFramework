using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Lightweight runtime profiler overlay. It avoids Unity Profiler APIs so it can run in builds.
    /// </summary>
    public sealed class AkgfPerformanceProfiler : MonoBehaviour
    {
        [Header("References")]
        public AkgfGestureSystemModeManager modeManager;
        public AkgfGestureRecognizer staticRecognizer;
        public AkgfSequenceGestureRecognizer sequenceRecognizer;
        public AkgfMultiUserGestureManager multiUserManager;
        public AkgfUserIdentityTracker identityTracker;
        public bool autoFindReferences = true;

        [Header("Overlay")]
        public bool showOverlay = true;
        public KeyCode toggleKey = KeyCode.F8;
        public Rect windowRect = new Rect(16, 16, 360, 210);
        public float smoothing = 0.10f;

        public float SmoothedFps { get; private set; }
        public float SmoothedFrameMilliseconds { get; private set; }

        private float deltaSeconds;

        private void Awake()
        {
            ResolveReferences();
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

            float currentDelta = Time.unscaledDeltaTime;
            if (deltaSeconds <= 0f)
            {
                deltaSeconds = currentDelta;
            }
            else
            {
                deltaSeconds = Mathf.Lerp(deltaSeconds, currentDelta, Mathf.Clamp01(smoothing));
            }

            SmoothedFrameMilliseconds = deltaSeconds * 1000f;
            SmoothedFps = deltaSeconds > 0.0001f ? 1f / deltaSeconds : 0f;
        }

        public void ResolveReferences()
        {
            if (modeManager == null)
            {
                modeManager = AkgfUnityObjectFinder.FindFirst<AkgfGestureSystemModeManager>();
            }

            if (staticRecognizer == null)
            {
                staticRecognizer = AkgfUnityObjectFinder.FindFirst<AkgfGestureRecognizer>();
            }

            if (sequenceRecognizer == null)
            {
                sequenceRecognizer = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureRecognizer>();
            }

            if (multiUserManager == null)
            {
                multiUserManager = AkgfUnityObjectFinder.FindFirst<AkgfMultiUserGestureManager>();
            }

            if (identityTracker == null)
            {
                identityTracker = AkgfUnityObjectFinder.FindFirst<AkgfUserIdentityTracker>();
            }
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "AKGF Performance");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label($"Mode: {(modeManager != null ? modeManager.trackingMode.ToString() : "Unknown")}");
            GUILayout.Label($"FPS: {SmoothedFps:0.0}  Frame: {SmoothedFrameMilliseconds:0.00} ms");

            if (staticRecognizer != null)
            {
                GUILayout.Label($"Static: {staticRecognizer.LastProcessingMilliseconds:0.000} ms  Body: {staticRecognizer.HasBodyThisFrame}");
            }

            if (sequenceRecognizer != null)
            {
                GUILayout.Label($"Sequence: {sequenceRecognizer.LastProcessingMilliseconds:0.000} ms  Buffer: {sequenceRecognizer.BufferedFrameCount}");
            }

            if (multiUserManager != null)
            {
                GUILayout.Label($"MultiUser: {multiUserManager.LastProcessingMilliseconds:0.000} ms  Visible: {multiUserManager.VisibleBodyCount}  Active: {multiUserManager.ActiveUserCount}");
            }

            if (identityTracker != null)
            {
                GUILayout.Label($"Stable users: {identityTracker.StableUserCount}");
            }

            GUILayout.Label("Toggle: F8");
            GUI.DragWindow();
        }
    }
}
