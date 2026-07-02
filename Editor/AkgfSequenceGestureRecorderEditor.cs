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
            if (GUILayout.Button($"Record Sequence '{recorder.gestureName}'"))
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
