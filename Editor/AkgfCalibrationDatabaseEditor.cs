using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AkgfCalibrationDatabase))]
public sealed class AkgfCalibrationDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AkgfCalibrationDatabase database = (AkgfCalibrationDatabase)target;
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Calibration Profile Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Load Active Profile"))
        {
            database.LoadActiveProfile();
            EditorUtility.SetDirty(database);
        }

        using (new EditorGUI.DisabledScope(database.activeProfile == null))
        {
            if (GUILayout.Button("Save Active Profile"))
            {
                database.SaveActiveProfile();
                EditorUtility.SetDirty(database);
            }
        }

        if (GUILayout.Button("Clear Active Profile"))
        {
            database.ClearActiveProfile();
            EditorUtility.SetDirty(database);
        }

        if (database.activeProfile != null)
        {
            EditorGUILayout.HelpBox($"Active profile: {database.activeProfile.profileName} | samples: {database.activeProfile.sampleCount}", MessageType.None);
        }
    }
}
