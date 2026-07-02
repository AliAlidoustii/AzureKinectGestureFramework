using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AkgfGestureDatabase))]
public sealed class AkgfGestureDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AkgfGestureDatabase database = (AkgfGestureDatabase)target;
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Database Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Load / Reload Gestures"))
        {
            database.LoadAll();
            EditorUtility.SetDirty(database);
        }

        if (GUILayout.Button("Open Editor Gesture Folder"))
        {
            string path = database.editorAssetGestureFolder;
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }

            EditorUtility.RevealInFinder(path);
        }

        if (GUILayout.Button("Open Persistent Gesture Folder"))
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
