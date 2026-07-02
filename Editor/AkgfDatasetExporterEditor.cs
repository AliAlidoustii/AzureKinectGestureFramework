using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AkgfDatasetExporter))]
public sealed class AkgfDatasetExporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AkgfDatasetExporter exporter = (AkgfDatasetExporter)target;
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Dataset Export", EditorStyles.boldLabel);

        if (GUILayout.Button("Export Static Gestures CSV"))
        {
            string path = exporter.ExportStaticGesturesCsv();
            EditorUtility.RevealInFinder(path);
        }

        if (GUILayout.Button("Export Sequence Gestures CSV"))
        {
            string path = exporter.ExportSequenceGesturesCsv();
            EditorUtility.RevealInFinder(path);
        }

        if (GUILayout.Button("Export Dataset Summary JSON"))
        {
            string path = exporter.ExportSummaryJson();
            EditorUtility.RevealInFinder(path);
        }
    }
}
