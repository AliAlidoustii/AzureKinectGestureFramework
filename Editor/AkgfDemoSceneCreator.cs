using System.IO;
using AzureKinectGestureFramework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AkgfDemoSceneCreator
{
    private const string ScenePath = "Assets/AzureKinectGestureFramework/Samples/Scenes/AKGF_DemoScene.unity";

    [MenuItem("Tools/Azure Kinect Gesture Framework/Create Demo Scene", false, 20)]
    public static void CreateDemoScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject cameraObject = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
        Camera camera = cameraObject.GetComponent<Camera>();
        if (camera == null)
        {
            camera = cameraObject.AddComponent<Camera>();
        }
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 2.2f, -5.5f);
        cameraObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

        GameObject lightObject = GameObject.Find("Directional Light");
        if (lightObject == null)
        {
            lightObject = new GameObject("Directional Light");
            lightObject.AddComponent<Light>().type = LightType.Directional;
        }
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject system = AkgfGestureFrameworkMenu.CreateGestureSystemObject();
        system.name = "AzureKinectGestureSystem_Demo";

        AkgfDemoSimulatedMultiUserSource simulatedSource = system.AddComponent<AkgfDemoSimulatedMultiUserSource>();
        simulatedSource.simulateTwoUsers = true;

        WireDemoSource(system, simulatedSource);

        GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        targetObject.name = "Demo Action Target - reacts to gestures";
        targetObject.transform.position = new Vector3(0f, 1f, 0f);
        targetObject.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        AkgfDemoActionTarget target = targetObject.AddComponent<AkgfDemoActionTarget>();
        target.demoLight = light;

        GameObject listenerObject = new GameObject("Demo API Listener");
        AkgfDemoApiListener listener = listenerObject.AddComponent<AkgfDemoApiListener>();
        listener.api = system.GetComponent<AkgfGestureSystemApi>();
        listener.target = target;

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Demo Floor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(3f, 1f, 3f);

        GameObject label = new GameObject("Demo Instructions");
        TextMesh text = label.AddComponent<TextMesh>();
        text.text = "AKGF Demo Scene\nF7 Recording UI | F6 Conflict View | F8 Profiler\nUse simulated source to record sample gestures, or replace it with Azure Kinect.";
        text.fontSize = 48;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        label.transform.position = new Vector3(0f, 2.8f, 1.8f);
        label.transform.rotation = Quaternion.Euler(20f, 180f, 0f);
        label.transform.localScale = Vector3.one * 0.025f;

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Selection.activeGameObject = system;
        EditorUtility.DisplayDialog("AKGF Demo Scene", "Created and saved:\n" + ScenePath + "\n\nThe demo uses a synthetic skeleton source. Replace it with Azure Kinect for real tracking.", "OK");
    }

    private static void WireDemoSource(GameObject system, AkgfDemoSimulatedMultiUserSource source)
    {
        foreach (AkgfGestureRecognizer recognizer in system.GetComponentsInChildren<AkgfGestureRecognizer>(true))
        {
            recognizer.skeletonSourceBehaviour = source;
        }
        foreach (AkgfSequenceGestureRecognizer recognizer in system.GetComponentsInChildren<AkgfSequenceGestureRecognizer>(true))
        {
            recognizer.skeletonSourceBehaviour = source;
        }
        foreach (AkgfGestureRecorder recorder in system.GetComponentsInChildren<AkgfGestureRecorder>(true))
        {
            recorder.skeletonSourceBehaviour = source;
        }
        foreach (AkgfSequenceGestureRecorder recorder in system.GetComponentsInChildren<AkgfSequenceGestureRecorder>(true))
        {
            recorder.skeletonSourceBehaviour = source;
        }
        foreach (AkgfCalibrationRecorder recorder in system.GetComponentsInChildren<AkgfCalibrationRecorder>(true))
        {
            recorder.skeletonSourceBehaviour = source;
        }
        foreach (AkgfMultiUserGestureManager manager in system.GetComponentsInChildren<AkgfMultiUserGestureManager>(true))
        {
            manager.multiSkeletonSourceBehaviour = source;
        }
    }
}
