using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AkgfSequenceGestureRecorder))]
public sealed class AkgfSequenceGestureRecorderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AkgfSequenceGestureRecorder recorder = (AkgfSequenceGestureRecorder)target;
        GUILayout.Space(10);

        EditorGUILayout.LabelField("Sequence Recording Controls", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(Application.isPlaying == false || recorder.IsRecording))
        {
            if (GUILayout.Button($"Start Manual Sequence '{recorder.gestureName}'"))
            {
                recorder.StartManualRecording(recorder.gestureName);
            }
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying == false || !recorder.IsRecording))
        {
            if (GUILayout.Button("Stop & Save Sequence Recording"))
            {
                recorder.StopRecordingAndSave();
            }
        }

        GUILayout.Space(4);

        using (new EditorGUI.DisabledScope(Application.isPlaying == false || recorder.IsRecording))
        {
            if (GUILayout.Button($"Record Timed Sequence '{recorder.gestureName}'"))
            {
                recorder.StartRecording(recorder.gestureName);
            }
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying == false || !recorder.IsRecording))
        {
            if (GUILayout.Button("Cancel Sequence Recording"))
            {
                recorder.CancelRecording();
            }
        }

        if (recorder.IsRecording)
        {
            string mode = recorder.IsManualRecording ? "manual" : "timed";
            EditorGUILayout.HelpBox($"Recording sequence ({mode})... Time: {recorder.RecordingElapsedSeconds:0.00}s, Frames: {recorder.CurrentFrameCount}", MessageType.Info);
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to record movement sequences.", MessageType.Info);
        }

        if (!string.IsNullOrWhiteSpace(recorder.LastSavedPath))
        {
            EditorGUILayout.HelpBox("Last saved: " + recorder.LastSavedPath, MessageType.None);
        }

        if (!string.IsNullOrWhiteSpace(recorder.LastError))
        {
            EditorGUILayout.HelpBox(recorder.LastError, MessageType.Warning);
        }
    }
}
