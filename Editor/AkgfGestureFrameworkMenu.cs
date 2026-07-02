using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

public static class AkgfGestureFrameworkMenu
{
    [MenuItem("GameObject/Azure Kinect Gesture Framework/Create Gesture System", false, 10)]
    public static void CreateGestureSystem(MenuCommand menuCommand)
    {
        GameObject root = CreateGestureSystemObject();
        Undo.RegisterCreatedObjectUndo(root, "Create Azure Kinect Gesture System");
        GameObjectUtility.SetParentAndAlign(root, menuCommand.context as GameObject);
        Selection.activeObject = root;
    }

    public static GameObject CreateGestureSystemObject()
    {
        GameObject root = new GameObject("AzureKinectGestureSystem");

        AkgfGestureDatabase database = root.AddComponent<AkgfGestureDatabase>();
        AkgfSequenceGestureDatabase sequenceDatabase = root.AddComponent<AkgfSequenceGestureDatabase>();
        AkgfGestureSettingsDatabase settingsDatabase = root.AddComponent<AkgfGestureSettingsDatabase>();
        AkgfGestureGroupController groupController = root.AddComponent<AkgfGestureGroupController>();
        AkgfCalibrationDatabase calibrationDatabase = root.AddComponent<AkgfCalibrationDatabase>();
        AkgfDatasetExporter exporter = root.AddComponent<AkgfDatasetExporter>();
        AkgfGestureReplay replay = root.AddComponent<AkgfGestureReplay>();

        GameObject singleRoot = new GameObject("SingleUserSystem");
        GameObject multiRoot = new GameObject("MultiUserSystem");
        singleRoot.transform.SetParent(root.transform, false);
        multiRoot.transform.SetParent(root.transform, false);

        AkgfGestureSystemModeManager modeManager = root.AddComponent<AkgfGestureSystemModeManager>();
        modeManager.trackingMode = AkgfTrackingMode.SingleUser;
        modeManager.singleUserSystemRoot = singleRoot;
        modeManager.multiUserSystemRoot = multiRoot;

        BuildSingleUserSystem(singleRoot, database, sequenceDatabase, settingsDatabase, groupController, calibrationDatabase);
        BuildMultiUserSystem(multiRoot, database, sequenceDatabase, settingsDatabase, groupController);

        exporter.staticGestureDatabase = database;
        exporter.sequenceGestureDatabase = sequenceDatabase;
        replay.staticGestureDatabase = database;
        replay.sequenceGestureDatabase = sequenceDatabase;

        AkgfGestureCoordinator coordinator = singleRoot.GetComponent<AkgfGestureCoordinator>();
        AkgfGestureRecognizer recognizer = singleRoot.GetComponent<AkgfGestureRecognizer>();
        AkgfSequenceGestureRecognizer sequenceRecognizer = singleRoot.GetComponent<AkgfSequenceGestureRecognizer>();
        AkgfGestureRecorder recorder = singleRoot.GetComponent<AkgfGestureRecorder>();
        AkgfSequenceGestureRecorder sequenceRecorder = singleRoot.GetComponent<AkgfSequenceGestureRecorder>();
        AkgfCalibrationRecorder calibrationRecorder = singleRoot.GetComponent<AkgfCalibrationRecorder>();
        AkgfMultiUserGestureManager multiManager = multiRoot.GetComponent<AkgfMultiUserGestureManager>();

        AkgfGestureSystemApi api = root.AddComponent<AkgfGestureSystemApi>();
        api.modeManager = modeManager;
        api.singleUserCoordinator = coordinator;
        api.multiUserManager = multiManager;

        AkgfGestureEventLogger logger = root.AddComponent<AkgfGestureEventLogger>();
        logger.gestureSystemApi = api;
        logger.singleUserCoordinator = coordinator;
        logger.multiUserManager = multiManager;
        logger.enabled = false;

        AkgfPerformanceProfiler profiler = root.AddComponent<AkgfPerformanceProfiler>();
        profiler.modeManager = modeManager;
        profiler.staticRecognizer = recognizer;
        profiler.sequenceRecognizer = sequenceRecognizer;
        profiler.multiUserManager = multiManager;
        profiler.identityTracker = multiRoot.GetComponent<AkgfUserIdentityTracker>();
        profiler.showOverlay = false;

        AkgfRecordingPanel recordingPanel = root.AddComponent<AkgfRecordingPanel>();
        recordingPanel.staticRecorder = recorder;
        recordingPanel.sequenceRecorder = sequenceRecorder;
        recordingPanel.calibrationRecorder = calibrationRecorder;
        recordingPanel.showPanel = false;

        AkgfGestureConflictVisualizer conflictVisualizer = root.AddComponent<AkgfGestureConflictVisualizer>();
        conflictVisualizer.staticRecognizer = recognizer;
        conflictVisualizer.sequenceRecognizer = sequenceRecognizer;
        conflictVisualizer.coordinator = coordinator;
        conflictVisualizer.multiUserManager = multiManager;
        conflictVisualizer.api = api;
        conflictVisualizer.showOverlay = false;

        AkgfMultiUserLiveDebugUI multiUserLiveDebug = root.AddComponent<AkgfMultiUserLiveDebugUI>();
        multiUserLiveDebug.multiUserManager = multiManager;
        multiUserLiveDebug.api = api;
        multiUserLiveDebug.showOverlay = false;

        modeManager.ApplyMode();
        return root;
    }

    private static void BuildSingleUserSystem(
        GameObject root,
        AkgfGestureDatabase database,
        AkgfSequenceGestureDatabase sequenceDatabase,
        AkgfGestureSettingsDatabase settingsDatabase,
        AkgfGestureGroupController groupController,
        AkgfCalibrationDatabase calibrationDatabase)
    {
        AkgfGestureRecognizer recognizer = root.AddComponent<AkgfGestureRecognizer>();
        AkgfSequenceGestureRecognizer sequenceRecognizer = root.AddComponent<AkgfSequenceGestureRecognizer>();
        AkgfGestureRecorder recorder = root.AddComponent<AkgfGestureRecorder>();
        AkgfSequenceGestureRecorder sequenceRecorder = root.AddComponent<AkgfSequenceGestureRecorder>();
        AkgfCalibrationRecorder calibrationRecorder = root.AddComponent<AkgfCalibrationRecorder>();
        AkgfGestureCoordinator coordinator = root.AddComponent<AkgfGestureCoordinator>();
        AkgfGestureEventRouter router = root.AddComponent<AkgfGestureEventRouter>();
        AkgfGestureDebuggerUI debugger = root.AddComponent<AkgfGestureDebuggerUI>();

        recognizer.gestureDatabase = database;
        recognizer.gestureSettingsDatabase = settingsDatabase;
        recognizer.groupController = groupController;
        recognizer.calibrationDatabase = calibrationDatabase;
        recognizer.fireUnityEventDirectly = false;

        sequenceRecognizer.sequenceGestureDatabase = sequenceDatabase;
        sequenceRecognizer.gestureSettingsDatabase = settingsDatabase;
        sequenceRecognizer.groupController = groupController;
        sequenceRecognizer.calibrationDatabase = calibrationDatabase;
        sequenceRecognizer.fireUnityEventDirectly = false;

        recorder.gestureDatabase = database;
        recorder.calibrationDatabase = calibrationDatabase;
        sequenceRecorder.sequenceGestureDatabase = sequenceDatabase;
        sequenceRecorder.calibrationDatabase = calibrationDatabase;
        calibrationRecorder.calibrationDatabase = calibrationDatabase;

        coordinator.staticPoseRecognizer = recognizer;
        coordinator.sequenceRecognizer = sequenceRecognizer;
        coordinator.gestureSettingsDatabase = settingsDatabase;
        coordinator.groupController = groupController;
        coordinator.sequenceHasPriority = true;

        router.coordinator = coordinator;
        router.recognizer = null;
        router.sequenceRecognizer = null;

        debugger.database = database;
        debugger.sequenceDatabase = sequenceDatabase;
        debugger.settingsDatabase = settingsDatabase;
        debugger.groupController = groupController;
        debugger.calibrationDatabase = calibrationDatabase;
        debugger.recognizer = recognizer;
        debugger.sequenceRecognizer = sequenceRecognizer;
        debugger.coordinator = coordinator;
        debugger.recorder = recorder;
        debugger.sequenceRecorder = sequenceRecorder;
        debugger.calibrationRecorder = calibrationRecorder;
    }

    private static void BuildMultiUserSystem(
        GameObject root,
        AkgfGestureDatabase database,
        AkgfSequenceGestureDatabase sequenceDatabase,
        AkgfGestureSettingsDatabase settingsDatabase,
        AkgfGestureGroupController groupController)
    {
        AkgfUserIdentityTracker identityTracker = root.AddComponent<AkgfUserIdentityTracker>();
        AkgfMultiUserGestureManager manager = root.AddComponent<AkgfMultiUserGestureManager>();
        AkgfMultiUserGestureEventRouter router = root.AddComponent<AkgfMultiUserGestureEventRouter>();

        manager.gestureDatabase = database;
        manager.sequenceGestureDatabase = sequenceDatabase;
        manager.gestureSettingsDatabase = settingsDatabase;
        manager.groupController = groupController;
        manager.identityTracker = identityTracker;
        manager.useStableUserIds = true;
        manager.lostUserTimeoutSeconds = 2.5f;
        manager.autoCalibrateNewUsers = true;
        manager.requireCalibrationBeforeRecognition = false;
        manager.sequenceHasPriority = true;
        manager.defaultStaticMinimumSimilarity = 0.55f;
        manager.defaultSequenceMinimumSimilarity = 0.50f;
        manager.defaultStaticStableSeconds = 0f;
        manager.defaultStaticCooldownSeconds = 1.0f;
        manager.defaultSequenceCooldownSeconds = 1.0f;
        manager.requiredConsecutiveSequenceMatches = 1;
        manager.globalCooldownSeconds = 0.3f;
        manager.sameGestureCooldownSeconds = 1.0f;
        manager.usePerGestureThresholds = false;
        manager.usePerGestureCooldowns = false;
        manager.usePerGestureStableSeconds = false;
        manager.useExplicitGestureSettings = true;
        manager.useTrackingQualityFilter = false;
        manager.debugIgnoreQualityFilter = true;
        manager.holdLastDebugValues = true;
        manager.debugCandidateHoldSeconds = 0.45f;
        manager.emitDetectedPhase = false;
        manager.emitEnterPhase = true;
        manager.emitStayPhase = false;
        manager.emitExitPhase = false;
        manager.emitConfirmedPhase = false;

        router.multiUserManager = manager;
    }

    [MenuItem("Tools/Azure Kinect Gesture Framework/Add MultiUser Live Debug UI", false, 35)]
    public static void AddMultiUserLiveDebugUi()
    {
        AkgfGestureSystemApi api = AkgfUnityObjectFinder.FindFirst<AkgfGestureSystemApi>();
        AkgfMultiUserGestureManager multiManager = AkgfUnityObjectFinder.FindFirst<AkgfMultiUserGestureManager>();
        GameObject target = api != null ? api.gameObject : (multiManager != null ? multiManager.gameObject : new GameObject("AKGF_MultiUserLiveDebug"));
        AkgfMultiUserLiveDebugUI ui = target.GetComponent<AkgfMultiUserLiveDebugUI>();
        if (ui == null)
        {
            ui = Undo.AddComponent<AkgfMultiUserLiveDebugUI>(target);
        }
        ui.api = api;
        ui.multiUserManager = multiManager;
        ui.showOverlay = true;
        Selection.activeGameObject = target;
        EditorUtility.SetDirty(target);
    }

    [MenuItem("Tools/Azure Kinect Gesture Framework/Create Resource Folders", false, 100)]
    public static void CreateResourceFolders()
    {
        string[] folders =
        {
            "Assets/AzureKinectGestureFramework/Resources/Gestures",
            "Assets/AzureKinectGestureFramework/Resources/SequenceGestures",
            "Assets/AzureKinectGestureFramework/Resources/GestureSettings",
            "Assets/AzureKinectGestureFramework/Resources/CalibrationProfiles",
            "Assets/AzureKinectGestureFramework/Exports",
            "Assets/AzureKinectGestureFramework/Samples/Scenes"
        };

        for (int i = 0; i < folders.Length; i++)
        {
            if (!System.IO.Directory.Exists(folders[i]))
            {
                System.IO.Directory.CreateDirectory(folders[i]);
            }
        }

        AssetDatabase.Refresh();
    }
}

public static class AkgfAzureKinectStandaloneMenu
{
    [MenuItem("GameObject/Azure Kinect Gesture Framework/Create Standalone Azure Kinect Source", false, 11)]
    public static void CreateStandaloneAzureKinectSource(MenuCommand menuCommand)
    {
        System.Type sourceType = System.Type.GetType("AzureKinectGestureFramework.AkgfAzureKinectStandaloneSource, Assembly-CSharp");
        if (sourceType == null)
        {
            EditorUtility.DisplayDialog(
                "AKGF Standalone Source not compiled",
                "The standalone Azure Kinect source is included in the framework, but it is compiled only when you add the scripting define symbol:\n\nAKGF_MICROSOFT_AZURE_KINECT_STANDALONE\n\nAlso make sure Microsoft.Azure.Kinect.Sensor and Microsoft.Azure.Kinect.BodyTracking assemblies are present in the Unity project.",
                "OK");
            return;
        }

        GameObject sourceObject = new GameObject("AKGF_StandaloneAzureKinectSource");
        sourceObject.AddComponent(sourceType);
        Undo.RegisterCreatedObjectUndo(sourceObject, "Create AKGF Standalone Azure Kinect Source");
        GameObjectUtility.SetParentAndAlign(sourceObject, menuCommand.context as GameObject);
        Selection.activeObject = sourceObject;
    }
}
