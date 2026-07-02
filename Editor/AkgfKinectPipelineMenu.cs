using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

public static class AkgfKinectPipelineMenu
{
    [MenuItem("GameObject/Azure Kinect Gesture Framework/Create Full AKGF Kinect Pipeline", false, 12)]
    public static void CreateFullKinectPipeline(MenuCommand menuCommand)
    {
        System.Type handlerType = System.Type.GetType("AzureKinectGestureFramework.AkgfTrackerHandler, Assembly-CSharp");
        if (handlerType == null)
        {
            EditorUtility.DisplayDialog(
                "AKGF Kinect pipeline not compiled",
                "The AKGF-owned TrackerHandler/SkeletalTrackingProvider pipeline is included, but it is compiled only when you add this scripting define symbol:\n\nAKGF_MICROSOFT_AZURE_KINECT_STANDALONE\n\nAlso make sure Microsoft.Azure.Kinect.Sensor and Microsoft.Azure.Kinect.BodyTracking assemblies plus native DLLs are present in the project.",
                "OK");
            return;
        }

        GameObject sourceObject = new GameObject("AKGF_KinectTrackerHandler");
        sourceObject.AddComponent(handlerType);
        Undo.RegisterCreatedObjectUndo(sourceObject, "Create AKGF Kinect Tracker Handler");
        GameObjectUtility.SetParentAndAlign(sourceObject, menuCommand.context as GameObject);
        Selection.activeObject = sourceObject;
    }
}
