using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AkgfGestureReplay))]
public sealed class AkgfGestureReplayEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AkgfGestureReplay replay = (AkgfGestureReplay)target;
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Replay Controls", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Play Replay"))
            {
                replay.Play();
            }

            if (GUILayout.Button("Stop Replay"))
            {
                replay.Stop();
            }
        }
    }
}
