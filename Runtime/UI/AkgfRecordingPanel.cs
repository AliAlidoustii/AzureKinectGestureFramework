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
        public Rect windowRect = new Rect(16, 240, 380, 260);
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
            GUI.enabled = sequenceRecorder != null && !sequenceRecorder.IsRecording;
            if (GUILayout.Button("Record Sequence"))
            {
                sequenceRecorder.StartRecording(sequenceGestureName);
            }
            GUI.enabled = true;
            DrawRecorderStatus(sequenceRecorder != null ? sequenceRecorder.IsRecording : false,
                sequenceRecorder != null ? sequenceRecorder.RecordingProgress01 : 0f,
                sequenceRecorder != null ? sequenceRecorder.CurrentFrameCount : 0,
                sequenceRecorder != null ? sequenceRecorder.LastError : "No sequence recorder found.");

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
