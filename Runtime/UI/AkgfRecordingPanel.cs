using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Small in-game IMGUI panel for recording gestures without removing the original hotkeys.
    /// Hotkeys still live on the recorder components: C / R / T by default.
    /// </summary>
    public sealed class AkgfRecordingPanel : MonoBehaviour
    {
        [Header("References")]
        public AkgfGestureRecorder staticRecorder;
        public AkgfSequenceGestureRecorder sequenceRecorder;
        public AkgfCalibrationRecorder calibrationRecorder;
        public bool autoFindReferences = true;

        [Header("UI")]
        public bool showPanel = true;
        public KeyCode toggleKey = KeyCode.F7;
        public Rect windowRect = new Rect(16, 240, 420, 310);
        public string staticGestureName = "CrossArms";
        public string sequenceGestureName = "Wave";
        public string calibrationProfileName = "DefaultUser";

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                showPanel = !showPanel;
            }

            if (autoFindReferences)
            {
                ResolveReferences();
            }
        }

        public void ResolveReferences()
        {
            if (staticRecorder == null)
            {
                staticRecorder = AkgfUnityObjectFinder.FindFirst<AkgfGestureRecorder>();
            }

            if (sequenceRecorder == null)
            {
                sequenceRecorder = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureRecorder>();
            }

            if (calibrationRecorder == null)
            {
                calibrationRecorder = AkgfUnityObjectFinder.FindFirst<AkgfCalibrationRecorder>();
            }
        }

        private void OnGUI()
        {
            if (!showPanel)
            {
                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "AKGF Recording");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Hotkeys still work: C = calibration, R = static, T = sequence");
            GUILayout.Space(4);

            GUILayout.Label("Static pose gesture");
            staticGestureName = GUILayout.TextField(staticGestureName ?? string.Empty);
            GUI.enabled = staticRecorder != null && !staticRecorder.IsRecording;
            if (GUILayout.Button("Record Static Pose"))
            {
                staticRecorder.StartRecording(staticGestureName);
            }
            GUI.enabled = true;
            DrawRecorderStatus(staticRecorder != null ? staticRecorder.IsRecording : false,
                staticRecorder != null ? staticRecorder.RecordingProgress01 : 0f,
                staticRecorder != null ? staticRecorder.CurrentSampleCount : 0,
                staticRecorder != null ? staticRecorder.LastError : "No static recorder found.");

            GUILayout.Space(6);
            GUILayout.Label("Movement sequence gesture");
            sequenceGestureName = GUILayout.TextField(sequenceGestureName ?? string.Empty);

            GUILayout.BeginHorizontal();
            GUI.enabled = sequenceRecorder != null && !sequenceRecorder.IsRecording;
            if (GUILayout.Button("Start Sequence Recording"))
            {
                sequenceRecorder.StartManualRecording(sequenceGestureName);
            }

            GUI.enabled = sequenceRecorder != null && sequenceRecorder.IsRecording;
            if (GUILayout.Button("Stop & Save Sequence"))
            {
                sequenceRecorder.StopRecordingAndSave();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUI.enabled = sequenceRecorder != null && !sequenceRecorder.IsRecording;
            if (GUILayout.Button("Record Timed Sequence"))
            {
                sequenceRecorder.StartRecording(sequenceGestureName);
            }
            GUI.enabled = true;

            DrawSequenceRecorderStatus(sequenceRecorder);

            GUILayout.Space(6);
            GUILayout.Label("Neutral calibration profile");
            calibrationProfileName = GUILayout.TextField(calibrationProfileName ?? string.Empty);
            GUI.enabled = calibrationRecorder != null && !calibrationRecorder.IsRecording;
            if (GUILayout.Button("Record Neutral Calibration"))
            {
                calibrationRecorder.profileName = string.IsNullOrWhiteSpace(calibrationProfileName) ? "DefaultUser" : calibrationProfileName.Trim();
                calibrationRecorder.StartCalibration();
            }
            GUI.enabled = true;
            DrawRecorderStatus(calibrationRecorder != null ? calibrationRecorder.IsRecording : false,
                calibrationRecorder != null ? calibrationRecorder.RecordingProgress01 : 0f,
                0,
                calibrationRecorder != null ? calibrationRecorder.LastError : "No calibration recorder found.");

            if (GUILayout.Button("Cancel Current Recording"))
            {
                staticRecorder?.CancelRecording();
                sequenceRecorder?.CancelRecording();
                calibrationRecorder?.CancelCalibration();
            }

            GUILayout.Label("Toggle panel: F7");
            GUI.DragWindow();
        }


        private static void DrawSequenceRecorderStatus(AkgfSequenceGestureRecorder sequenceRecorder)
        {
            if (sequenceRecorder == null)
            {
                GUILayout.Label("Status: No sequence recorder found.");
                return;
            }

            if (sequenceRecorder.IsRecording)
            {
                string mode = sequenceRecorder.IsManualRecording ? "manual" : "timed";
                GUILayout.Label($"Recording sequence ({mode})... Time: {sequenceRecorder.RecordingElapsedSeconds:0.00}s  Frames: {sequenceRecorder.CurrentFrameCount}");
            }
            else if (!string.IsNullOrWhiteSpace(sequenceRecorder.LastError))
            {
                GUILayout.Label("Status: " + sequenceRecorder.LastError);
            }
            else if (!string.IsNullOrWhiteSpace(sequenceRecorder.LastSavedPath))
            {
                GUILayout.Label("Last saved: " + sequenceRecorder.LastSavedPath);
            }
        }

        private static void DrawRecorderStatus(bool recording, float progress, int count, string error)
        {
            if (recording)
            {
                GUILayout.Label($"Recording... {Mathf.RoundToInt(progress * 100f)}%  Samples: {count}");
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                GUILayout.Label("Status: " + error);
            }
        }
    }
}
