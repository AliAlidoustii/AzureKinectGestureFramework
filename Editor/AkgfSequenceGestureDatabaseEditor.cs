using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AkgfSequenceGestureDatabase))]
public sealed class AkgfSequenceGestureDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AkgfSequenceGestureDatabase database = (AkgfSequenceGestureDatabase)target;
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Sequence Database Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Load / Reload Sequence Gestures"))
        {
            database.LoadAll();
            EditorUtility.SetDirty(database);
        }

        if (GUILayout.Button("Open Editor Sequence Gesture Folder"))
        {
            string path = database.editorAssetGestureFolder;
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }

            EditorUtility.RevealInFinder(path);
        }

        if (GUILayout.Button("Open Persistent Sequence Gesture Folder"))
        {
            string path = database.GetPersistentGestureFolderPath();
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }

            EditorUtility.RevealInFinder(path);
        }
    }
}
