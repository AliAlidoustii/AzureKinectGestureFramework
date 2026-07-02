using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AkgfGestureRecorder))]
public sealed class AkgfGestureRecorderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AkgfGestureRecorder recorder = (AkgfGestureRecorder)target;
        GUILayout.Space(10);

        EditorGUILayout.LabelField("Recording Controls", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(Application.isPlaying == false || recorder.IsRecording))
        {
            if (GUILayout.Button($"Record '{recorder.gestureName}'"))
            {
                recorder.StartRecording(recorder.gestureName);
            }
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying == false || !recorder.IsRecording))
        {
            if (GUILayout.Button("Cancel Recording"))
            {
                recorder.CancelRecording();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to record body poses.", MessageType.Info);
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
