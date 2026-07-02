using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AkgfCalibrationRecorder))]
public sealed class AkgfCalibrationRecorderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AkgfCalibrationRecorder recorder = (AkgfCalibrationRecorder)target;
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Calibration Controls", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying || recorder.IsRecording))
        {
            if (GUILayout.Button($"Record Neutral Profile '{recorder.profileName}'"))
            {
                recorder.StartCalibration();
            }
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying || !recorder.IsRecording))
        {
            if (GUILayout.Button("Cancel Calibration"))
            {
                recorder.CancelCalibration();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode, stand naturally, then record calibration for 2-3 seconds.", MessageType.Info);
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
