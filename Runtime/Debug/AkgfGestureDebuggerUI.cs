using UnityEngine;

namespace AzureKinectGestureFramework
{
    public sealed class AkgfGestureDebuggerUI : MonoBehaviour
    {
        public AkgfGestureRecognizer recognizer;
        public AkgfSequenceGestureRecognizer sequenceRecognizer;
        public AkgfGestureCoordinator coordinator;
        public AkgfGestureRecorder recorder;
        public AkgfSequenceGestureRecorder sequenceRecorder;
        public AkgfCalibrationRecorder calibrationRecorder;
        public AkgfCalibrationDatabase calibrationDatabase;
        public AkgfGestureSettingsDatabase settingsDatabase;
        public AkgfGestureGroupController groupController;
        public AkgfGestureDatabase database;
        public AkgfSequenceGestureDatabase sequenceDatabase;
        public bool show = true;
        public Vector2 position = new Vector2(12, 12);
        public Vector2 size = new Vector2(620, 520);

        private GUIStyle labelStyle;

        private void Awake()
        {
            AutoFind();
        }

        public void AutoFind()
        {
            if (recognizer == null) recognizer = AkgfUnityObjectFinder.FindFirst<AkgfGestureRecognizer>();
            if (sequenceRecognizer == null) sequenceRecognizer = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureRecognizer>();
            if (coordinator == null) coordinator = AkgfUnityObjectFinder.FindFirst<AkgfGestureCoordinator>();
            if (recorder == null) recorder = AkgfUnityObjectFinder.FindFirst<AkgfGestureRecorder>();
            if (sequenceRecorder == null) sequenceRecorder = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureRecorder>();
            if (calibrationRecorder == null) calibrationRecorder = AkgfUnityObjectFinder.FindFirst<AkgfCalibrationRecorder>();
            if (calibrationDatabase == null) calibrationDatabase = AkgfUnityObjectFinder.FindFirst<AkgfCalibrationDatabase>();
            if (settingsDatabase == null) settingsDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureSettingsDatabase>();
            if (groupController == null) groupController = AkgfUnityObjectFinder.FindFirst<AkgfGestureGroupController>();
            if (database == null) database = AkgfUnityObjectFinder.FindFirst<AkgfGestureDatabase>();
            if (sequenceDatabase == null) sequenceDatabase = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureDatabase>();
        }

        private void OnGUI()
        {
            if (!show)
            {
                return;
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            }

            GUILayout.BeginArea(new Rect(position.x, position.y, size.x, size.y), GUI.skin.box);
            GUILayout.Label("Azure Kinect Gesture Framework - Live Debug", labelStyle);
            GUILayout.Label($"Static gestures: {(database != null ? database.Count : 0)} | Sequence gestures: {(sequenceDatabase != null ? sequenceDatabase.Count : 0)} | Settings: {(settingsDatabase != null ? settingsDatabase.Settings.Count : 0)}", labelStyle);

            DrawCalibration();
            DrawGroups();
            DrawStaticRecognizer();
            DrawSequenceRecognizer();
            DrawCoordinator();
            DrawRecorders();

            GUILayout.EndArea();
        }

        private void DrawCalibration()
        {
            if (calibrationDatabase == null)
            {
                GUILayout.Label("Calibration: missing", labelStyle);
                return;
            }

            string profile = calibrationDatabase.HasUsableProfile ? calibrationDatabase.activeProfile.profileName : "none";
            GUILayout.Label($"Calibration: {profile} | strength={calibrationDatabase.calibrationStrength:0.00} | recognition={(calibrationDatabase.applyCalibrationToRecognition ? "on" : "off")}", labelStyle);
        }

        private void DrawGroups()
        {
            if (groupController == null || groupController.groups == null)
            {
                GUILayout.Label("Gesture groups: none", labelStyle);
                return;
            }

            string text = "Groups: ";
            for (int i = 0; i < groupController.groups.Count; i++)
            {
                if (groupController.groups[i] == null) continue;
                text += $"{groupController.groups[i].groupName}={(groupController.groups[i].active ? "on" : "off")} ";
            }
            GUILayout.Label(text, labelStyle);
        }

        private void DrawStaticRecognizer()
        {
            if (recognizer == null)
            {
                GUILayout.Label("Static recognizer: missing", labelStyle);
                return;
            }

            AkgfGestureMatchResult match = recognizer.LastMatch;
            GUILayout.Label($"Static body: {(recognizer.HasBodyThisFrame ? "yes" : "no")} | pose: {(recognizer.HasNormalizedPoseThisFrame ? "yes" : "no")} | body quality={recognizer.LastTrackingQuality:0.00}", labelStyle);
            GUILayout.Label(match.isValid
                ? $"Static best: {match.gestureName} | {AkgfGestureMatcher.FormatSimilarityPercent(match.similarity)} | q={match.trackingQuality:0.00} | mirrored={match.wasMirrored} | group={match.groupName}"
                : "Static best: none", labelStyle);
        }

        private void DrawSequenceRecognizer()
        {
            if (sequenceRecognizer == null)
            {
                GUILayout.Label("Sequence recognizer: missing", labelStyle);
                return;
            }

            AkgfGestureMatchResult match = sequenceRecognizer.LastMatch;
            GUILayout.Label($"Sequence buffer: {sequenceRecognizer.BufferedFrameCount} frames | ready: {(sequenceRecognizer.HasEnoughBufferedFrames ? "yes" : "no")} | body quality={sequenceRecognizer.LastTrackingQuality:0.00}", labelStyle);
            GUILayout.Label(match.isValid
                ? $"Sequence best: {match.gestureName} | {AkgfGestureMatcher.FormatSimilarityPercent(match.similarity)} | q={match.trackingQuality:0.00} | mirrored={match.wasMirrored} | group={match.groupName}"
                : "Sequence best: none", labelStyle);
        }

        private void DrawCoordinator()
        {
            if (coordinator == null)
            {
                GUILayout.Label("Coordinator: missing", labelStyle);
                return;
            }

            AkgfGestureMatchResult output = coordinator.LastOutput;
            GUILayout.Label(output.isValid
                ? $"Output: {output.gestureName} [{output.gestureKind}/{output.phase}] | {AkgfGestureMatcher.FormatSimilarityPercent(output.similarity)} | active={coordinator.ActiveGestureName}"
                : "Output: none yet", labelStyle);
        }

        private void DrawRecorders()
        {
            if (calibrationRecorder != null)
            {
                GUILayout.Label(calibrationRecorder.IsRecording
                    ? $"Calibration recording... {calibrationRecorder.RecordingProgress01 * 100f:0}%"
                    : $"Calibration recorder: press {calibrationRecorder.recordHotkey} for neutral profile.", labelStyle);
            }

            if (recorder != null)
            {
                GUILayout.Label(recorder.IsRecording
                    ? $"Static recording '{recorder.gestureName}'... {recorder.RecordingProgress01 * 100f:0}% | Samples: {recorder.CurrentSampleCount}"
                    : $"Static recorder: press {recorder.recordKey} for '{recorder.gestureName}'.", labelStyle);

                if (!string.IsNullOrWhiteSpace(recorder.LastError))
                {
                    GUILayout.Label($"Static recorder error: {recorder.LastError}", labelStyle);
                }
            }

            if (sequenceRecorder != null)
            {
                GUILayout.Label(sequenceRecorder.IsRecording
                    ? $"Sequence recording '{sequenceRecorder.gestureName}' ({(sequenceRecorder.IsManualRecording ? "manual" : "timed")})... {sequenceRecorder.RecordingElapsedSeconds:0.00}s | Frames: {sequenceRecorder.CurrentFrameCount}"
                    : $"Sequence recorder: press {sequenceRecorder.recordKey} for '{sequenceRecorder.gestureName}'{(sequenceRecorder.manualStopMode ? " start/stop" : " timed") }.", labelStyle);

                if (!string.IsNullOrWhiteSpace(sequenceRecorder.LastError))
                {
                    GUILayout.Label($"Sequence recorder error: {sequenceRecorder.LastError}", labelStyle);
                }
            }
        }
    }
}
