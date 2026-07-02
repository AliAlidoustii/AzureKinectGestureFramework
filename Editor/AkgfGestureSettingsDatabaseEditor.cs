using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AkgfGestureSettingsDatabase))]
public sealed class AkgfGestureSettingsDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AkgfGestureSettingsDatabase database = (AkgfGestureSettingsDatabase)target;
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Gesture Settings Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Load / Reload Settings"))
        {
            database.LoadAll();
            EditorUtility.SetDirty(database);
        }

        if (GUILayout.Button("Save Settings"))
        {
            string path = database.SaveAll();
            EditorUtility.SetDirty(database);
            EditorGUILayout.HelpBox("Saved: " + path, MessageType.None);
        }
    }
}
