using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AkgfTransformSkeletonSource))]
public sealed class AkgfTransformSkeletonSourceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AkgfTransformSkeletonSource source = (AkgfTransformSkeletonSource)target;
        GUILayout.Space(8);

        if (GUILayout.Button("Auto Assign Joints From Root"))
        {
            Undo.RecordObject(source, "Auto Assign AKGF Joints");
            source.AutoAssignJointsFromRoot();
            EditorUtility.SetDirty(source);
        }

        EditorGUILayout.HelpBox(
            "For the Microsoft sample, assign the transform that contains the rendered skeleton body. " +
            "If the selected root has a child with 32 joint children, Auto Assign will use that child.",
            MessageType.Info);
    }
}
