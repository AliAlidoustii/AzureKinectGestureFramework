using System;
using System.Collections.Generic;
using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

public sealed class AkgfControlPanelWindow : EditorWindow
{
    private AkgfGestureSystemModeManager modeManager;
    private AkgfGestureRecognizer staticRecognizer;
    private AkgfSequenceGestureRecognizer sequenceRecognizer;
    private AkgfGestureCoordinator coordinator;
    private AkgfGestureSettingsDatabase settingsDatabase;
    private AkgfGestureGroupController groupController;
    private AkgfGestureDatabase gestureDatabase;
    private AkgfSequenceGestureDatabase sequenceDatabase;
    private AkgfGestureRecorder staticRecorder;
    private AkgfSequenceGestureRecorder sequenceRecorder;
    private AkgfCalibrationRecorder calibrationRecorder;
    private AkgfRecordingPanel recordingPanel;
    private AkgfGestureConflictVisualizer conflictVisualizer;
    private AkgfPerformanceProfiler performanceProfiler;
    private AkgfGestureSystemApi api;
    private AkgfMultiUserGestureManager multiUserManager;

    private UnityEngine.Object sourceCandidate;
    private Vector2 scroll;
    private bool showQuickSetup = true;
    private bool showSource = true;
    private bool showRecognition = true;
    private bool showCoordinator = true;
    private bool showSettingsDb = true;
    private bool showGroups = true;
    private bool showRecording = true;
    private bool showDebug = true;
    private bool showRuntimeStatus = true;
    private bool showMultiUser = false;
    private string newSettingsGestureName = "CrossArms";
    private AkgfGestureKind newSettingsKind = AkgfGestureKind.StaticPose;

    [MenuItem("Tools/Azure Kinect Gesture Framework/AKGF Control Panel", false, 5)]
    public static void Open()
    {
        GetWindow<AkgfControlPanelWindow>("AKGF Control Panel");
    }

    private void OnEnable()
    {
        ResolveReferences();
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        ResolveReferences();
        Repaint();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (modeManager == null && GUILayout.Button("Create Gesture System In This Scene", GUILayout.Height(28)))
        {
            GameObject root = AkgfGestureFrameworkMenu.CreateGestureSystemObject();
            Undo.RegisterCreatedObjectUndo(root, "Create Azure Kinect Gesture System");
            Selection.activeGameObject = root;
            ResolveReferences();
        }

        if (modeManager == null)
        {
            EditorGUILayout.HelpBox("No AKGF gesture system was found in the open scene. Create it first, then use this panel to edit everything in one place.", MessageType.Warning);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawQuickSetupSection();
        DrawSourceSection();
        DrawRecognitionSection();
        DrawCoordinatorSection();
        DrawSettingsDatabaseSection();
        DrawGroupsSection();
        DrawRecordingSection();
        DrawDebugSection();
        DrawMultiUserSection();
        DrawRuntimeStatusSection();
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Find", EditorStyles.toolbarButton, GUILayout.Width(55)))
            {
                ResolveReferences();
            }

            if (GUILayout.Button("Select Root", EditorStyles.toolbarButton, GUILayout.Width(85)) && modeManager != null)
            {
                Selection.activeGameObject = modeManager.gameObject;
            }

            if (GUILayout.Button("Reconnect API", EditorStyles.toolbarButton, GUILayout.Width(95)))
            {
                api?.Reconnect();
                coordinator?.Reconnect();
                staticRecognizer?.ResolveReferences();
                sequenceRecognizer?.ResolveReferences();
                multiUserManager?.ResolveReferences();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(modeManager != null ? modeManager.gameObject.name : "No AKGF System");
        }
    }

    private void DrawQuickSetupSection()
    {
        showQuickSetup = EditorGUILayout.Foldout(showQuickSetup, "Quick Setup / Debug Presets", true);
        if (!showQuickSetup)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.HelpBox("Use these buttons while testing. They make recognition permissive, enable event output, and reduce cooldowns so a candidate can become a final result.", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("SingleUser Debug Mode", GUILayout.Height(28)))
            {
                ApplySingleUserDebugMode();
            }

            if (GUILayout.Button("Force Candidate As Result", GUILayout.Height(28)))
            {
                ApplyForceCandidateResultMode();
            }

            if (GUILayout.Button("Normal SingleUser Defaults", GUILayout.Height(28)))
            {
                ApplySingleUserNormalDefaults();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Import Recorded Gestures Into Settings"))
            {
                ImportRecordedGesturesIntoSettings();
            }

            if (GUILayout.Button("Save Gesture Settings"))
            {
                SaveSettingsDatabase();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSourceSection()
    {
        showSource = EditorGUILayout.Foldout(showSource, "Kinect / Skeleton Source", true);
        if (!showSource)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        sourceCandidate = EditorGUILayout.ObjectField("Source Object", sourceCandidate, typeof(UnityEngine.Object), true);
        EditorGUILayout.HelpBox("Drag AKGF_KinectTrackerHandler here. It can feed both SingleUser and MultiUser. Then click Apply.", MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selected Object"))
            {
                sourceCandidate = Selection.activeObject;
            }

            if (GUILayout.Button("Apply Source To SingleUser"))
            {
                ApplySourceToSingleUser(FindSkeletonSourceBehaviour(sourceCandidate));
            }

            if (GUILayout.Button("Apply Source To MultiUser"))
            {
                ApplySourceToMultiUser(FindMultiSkeletonSourceBehaviour(sourceCandidate));
            }
        }

        EditorGUILayout.Space(4);
        DrawObjectLine("Static recognizer source", staticRecognizer != null ? staticRecognizer.skeletonSourceBehaviour : null);
        DrawObjectLine("Sequence recognizer source", sequenceRecognizer != null ? sequenceRecognizer.skeletonSourceBehaviour : null);
        DrawObjectLine("Static recorder source", staticRecorder != null ? staticRecorder.skeletonSourceBehaviour : null);
        DrawObjectLine("Sequence recorder source", sequenceRecorder != null ? sequenceRecorder.skeletonSourceBehaviour : null);
        DrawObjectLine("Calibration source", calibrationRecorder != null ? calibrationRecorder.skeletonSourceBehaviour : null);
        DrawObjectLine("MultiUser source", multiUserManager != null ? multiUserManager.multiSkeletonSourceBehaviour : null);
        EditorGUILayout.EndVertical();
    }

    private void DrawRecognitionSection()
    {
        showRecognition = EditorGUILayout.Foldout(showRecognition, "SingleUser Recognition", true);
        if (!showRecognition)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        DrawModeManagerMini();

        if (staticRecognizer == null)
        {
            EditorGUILayout.HelpBox("No AkgfGestureRecognizer found.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Static Pose Recognizer", EditorStyles.boldLabel);
            Undo.RecordObject(staticRecognizer, "Edit AKGF Static Recognizer");
            staticRecognizer.minimumSimilarity = EditorGUILayout.Slider("Minimum Similarity", staticRecognizer.minimumSimilarity, 0f, 1f);
            staticRecognizer.requiredStableSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Stable / Hold Seconds", staticRecognizer.requiredStableSeconds));
            staticRecognizer.sameGestureCooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Same Gesture Cooldown", staticRecognizer.sameGestureCooldownSeconds));
            staticRecognizer.recognizeEveryFrame = EditorGUILayout.Toggle("Recognize Every Frame", staticRecognizer.recognizeEveryFrame);
            staticRecognizer.autoLoadDatabaseOnStart = EditorGUILayout.Toggle("Auto Load Database", staticRecognizer.autoLoadDatabaseOnStart);
            staticRecognizer.fireUnityEventDirectly = EditorGUILayout.Toggle("Direct UnityEvent", staticRecognizer.fireUnityEventDirectly);
            EditorUtility.SetDirty(staticRecognizer);
        }

        EditorGUILayout.Space(8);
        if (sequenceRecognizer == null)
        {
            EditorGUILayout.HelpBox("No AkgfSequenceGestureRecognizer found.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Sequence Gesture Recognizer", EditorStyles.boldLabel);
            Undo.RecordObject(sequenceRecognizer, "Edit AKGF Sequence Recognizer");
            sequenceRecognizer.minimumSimilarity = EditorGUILayout.Slider("Minimum Similarity", sequenceRecognizer.minimumSimilarity, 0f, 1f);
            sequenceRecognizer.recognitionWindowSeconds = Mathf.Max(0.05f, EditorGUILayout.FloatField("Recognition Window Seconds", sequenceRecognizer.recognitionWindowSeconds));
            sequenceRecognizer.samplesPerSecond = Mathf.Max(1f, EditorGUILayout.FloatField("Samples Per Second", sequenceRecognizer.samplesPerSecond));
            sequenceRecognizer.recognitionsPerSecond = Mathf.Max(1f, EditorGUILayout.FloatField("Recognitions Per Second", sequenceRecognizer.recognitionsPerSecond));
            sequenceRecognizer.minimumWindowFrames = Mathf.Max(2, EditorGUILayout.IntField("Minimum Window Frames", sequenceRecognizer.minimumWindowFrames));
            sequenceRecognizer.requiredConsecutiveMatches = Mathf.Clamp(EditorGUILayout.IntField("Required Consecutive Matches", sequenceRecognizer.requiredConsecutiveMatches), 1, 10);
            sequenceRecognizer.sameGestureCooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Same Gesture Cooldown", sequenceRecognizer.sameGestureCooldownSeconds));
            sequenceRecognizer.autoLoadDatabaseOnStart = EditorGUILayout.Toggle("Auto Load Database", sequenceRecognizer.autoLoadDatabaseOnStart);
            sequenceRecognizer.fireUnityEventDirectly = EditorGUILayout.Toggle("Direct UnityEvent", sequenceRecognizer.fireUnityEventDirectly);
            EditorUtility.SetDirty(sequenceRecognizer);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCoordinatorSection()
    {
        showCoordinator = EditorGUILayout.Foldout(showCoordinator, "Coordinator / Result Output", true);
        if (!showCoordinator)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        if (coordinator == null)
        {
            EditorGUILayout.HelpBox("No AkgfGestureCoordinator found. Candidates can appear without final API events if the coordinator is missing or not connected.", MessageType.Warning);
        }
        else
        {
            Undo.RecordObject(coordinator, "Edit AKGF Coordinator");
            coordinator.staticPoseRecognizer = (AkgfGestureRecognizer)EditorGUILayout.ObjectField("Static Recognizer", coordinator.staticPoseRecognizer, typeof(AkgfGestureRecognizer), true);
            coordinator.sequenceRecognizer = (AkgfSequenceGestureRecognizer)EditorGUILayout.ObjectField("Sequence Recognizer", coordinator.sequenceRecognizer, typeof(AkgfSequenceGestureRecognizer), true);
            coordinator.gestureSettingsDatabase = (AkgfGestureSettingsDatabase)EditorGUILayout.ObjectField("Settings Database", coordinator.gestureSettingsDatabase, typeof(AkgfGestureSettingsDatabase), true);
            coordinator.groupController = (AkgfGestureGroupController)EditorGUILayout.ObjectField("Group Controller", coordinator.groupController, typeof(AkgfGestureGroupController), true);
            coordinator.sequenceHasPriority = EditorGUILayout.Toggle("Sequence Has Priority", coordinator.sequenceHasPriority);
            coordinator.sequenceBlocksStaticSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Sequence Blocks Static Seconds", coordinator.sequenceBlocksStaticSeconds));
            coordinator.globalCooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Global Cooldown Seconds", coordinator.globalCooldownSeconds));
            coordinator.sameGestureCooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Same Gesture Cooldown Seconds", coordinator.sameGestureCooldownSeconds));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Normal Candidate Acceptance", EditorStyles.boldLabel);
            coordinator.acceptCurrentCandidateDirectly = EditorGUILayout.Toggle("Accept Current Candidate Directly", coordinator.acceptCurrentCandidateDirectly);
            coordinator.directStaticMinimumSimilarity = EditorGUILayout.Slider("Direct Static Min Similarity", coordinator.directStaticMinimumSimilarity, 0f, 1f);
            coordinator.directSequenceMinimumSimilarity = EditorGUILayout.Slider("Direct Sequence Min Similarity", coordinator.directSequenceMinimumSimilarity, 0f, 1f);
            coordinator.usePerGestureThresholdForDirectCandidates = EditorGUILayout.Toggle("Use Per-Gesture Threshold", coordinator.usePerGestureThresholdForDirectCandidates);
            coordinator.useRecognizerThresholdForDirectCandidates = EditorGUILayout.Toggle("Use Recognizer Threshold", coordinator.useRecognizerThresholdForDirectCandidates);
            EditorGUILayout.Space(4);
            coordinator.debugForceEmitBestCandidateAsResult = EditorGUILayout.Toggle("DEBUG: Force Candidate As Result", coordinator.debugForceEmitBestCandidateAsResult);
            coordinator.debugForceEmitIntervalSeconds = Mathf.Max(0.01f, EditorGUILayout.FloatField("Debug Force Emit Interval", coordinator.debugForceEmitIntervalSeconds));
            coordinator.emitDetectedPhase = EditorGUILayout.Toggle("Emit Detected", coordinator.emitDetectedPhase);
            coordinator.emitEnterPhase = EditorGUILayout.Toggle("Emit Enter", coordinator.emitEnterPhase);
            coordinator.emitStayPhase = EditorGUILayout.Toggle("Emit Stay", coordinator.emitStayPhase);
            coordinator.emitExitPhase = EditorGUILayout.Toggle("Emit Exit", coordinator.emitExitPhase);
            coordinator.emitConfirmedPhase = EditorGUILayout.Toggle("Emit Confirmed", coordinator.emitConfirmedPhase);
            coordinator.exitAfterMissingSeconds = Mathf.Max(0.01f, EditorGUILayout.FloatField("Exit After Missing Seconds", coordinator.exitAfterMissingSeconds));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reconnect Coordinator"))
                {
                    coordinator.Reconnect();
                }

                if (GUILayout.Button("Select Coordinator"))
                {
                    Selection.activeGameObject = coordinator.gameObject;
                }
            }

            EditorUtility.SetDirty(coordinator);
        }

        if (api != null)
        {
            EditorGUILayout.Space(6);
            Undo.RecordObject(api, "Edit AKGF API");
            api.modeManager = (AkgfGestureSystemModeManager)EditorGUILayout.ObjectField("API Mode Manager", api.modeManager, typeof(AkgfGestureSystemModeManager), true);
            api.singleUserCoordinator = (AkgfGestureCoordinator)EditorGUILayout.ObjectField("API SingleUser Coordinator", api.singleUserCoordinator, typeof(AkgfGestureCoordinator), true);
            api.multiUserManager = (AkgfMultiUserGestureManager)EditorGUILayout.ObjectField("API MultiUser Manager", api.multiUserManager, typeof(AkgfMultiUserGestureManager), true);
            api.autoFindReferences = EditorGUILayout.Toggle("API Auto Find References", api.autoFindReferences);
            if (GUILayout.Button("Reconnect API"))
            {
                api.Reconnect();
            }
            EditorUtility.SetDirty(api);
        }
        else
        {
            EditorGUILayout.HelpBox("No AkgfGestureSystemApi found. Your Console receiver listens here, so add it to the root if it is missing.", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSettingsDatabaseSection()
    {
        showSettingsDb = EditorGUILayout.Foldout(showSettingsDb, "Gesture Settings Database / Per-Gesture Tuning", true);
        if (!showSettingsDb)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        if (settingsDatabase == null)
        {
            EditorGUILayout.HelpBox("No AkgfGestureSettingsDatabase found.", MessageType.Warning);
            if (GUILayout.Button("Add Settings Database To Root") && modeManager != null)
            {
                settingsDatabase = Undo.AddComponent<AkgfGestureSettingsDatabase>(modeManager.gameObject);
            }
            EditorGUILayout.EndVertical();
            return;
        }

        Undo.RecordObject(settingsDatabase, "Edit AKGF Settings Database");
        settingsDatabase.defaultStaticMinimumSimilarity = EditorGUILayout.Slider("Default Static Similarity", settingsDatabase.defaultStaticMinimumSimilarity, 0f, 1f);
        settingsDatabase.defaultSequenceMinimumSimilarity = EditorGUILayout.Slider("Default Sequence Similarity", settingsDatabase.defaultSequenceMinimumSimilarity, 0f, 1f);
        settingsDatabase.defaultStaticStableSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Default Static Stable Seconds", settingsDatabase.defaultStaticStableSeconds));
        settingsDatabase.defaultStaticCooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Default Static Cooldown Seconds", settingsDatabase.defaultStaticCooldownSeconds));
        settingsDatabase.defaultSequenceCooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Default Sequence Cooldown Seconds", settingsDatabase.defaultSequenceCooldownSeconds));
        settingsDatabase.defaultMinimumTrackingQuality = EditorGUILayout.Slider("Default Minimum Tracking Quality", settingsDatabase.defaultMinimumTrackingQuality, 0f, 1f);
        settingsDatabase.defaultGroupName = EditorGUILayout.TextField("Default Group Name", settingsDatabase.defaultGroupName);
        settingsDatabase.loadOnAwake = EditorGUILayout.Toggle("Load On Awake", settingsDatabase.loadOnAwake);
        settingsDatabase.loadFromResources = EditorGUILayout.Toggle("Load From Resources", settingsDatabase.loadFromResources);
        settingsDatabase.loadFromPersistentData = EditorGUILayout.Toggle("Load From Persistent Data", settingsDatabase.loadFromPersistentData);

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            newSettingsGestureName = EditorGUILayout.TextField("Gesture", newSettingsGestureName);
            newSettingsKind = (AkgfGestureKind)EditorGUILayout.EnumPopup(newSettingsKind, GUILayout.Width(120));
            if (GUILayout.Button("Add/Replace", GUILayout.Width(95)))
            {
                settingsDatabase.AddOrReplace(settingsDatabase.CreateRuntimeDefault(newSettingsGestureName, newSettingsKind));
                EditorUtility.SetDirty(settingsDatabase);
            }
        }

        IReadOnlyList<AkgfGestureSettings> items = settingsDatabase.Settings;
        EditorGUILayout.LabelField("Per-Gesture Settings", EditorStyles.boldLabel);
        for (int i = 0; i < items.Count; i++)
        {
            AkgfGestureSettings item = items[i];
            if (item == null)
            {
                continue;
            }

            EditorGUILayout.BeginVertical("box");
            using (new EditorGUILayout.HorizontalScope())
            {
                item.enabled = EditorGUILayout.Toggle(item.enabled, GUILayout.Width(18));
                item.gestureName = EditorGUILayout.TextField(item.gestureName);
                item.gestureKind = (AkgfGestureKind)EditorGUILayout.EnumPopup(item.gestureKind, GUILayout.Width(120));
            }

            item.groupName = EditorGUILayout.TextField("Group", item.groupName);
            item.minimumSimilarity = EditorGUILayout.Slider("Min Similarity", item.minimumSimilarity, 0f, 1f);
            item.requiredStableSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Stable / Hold Seconds", item.requiredStableSeconds));
            item.cooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Cooldown Seconds", item.cooldownSeconds));
            item.priority = EditorGUILayout.IntField("Priority", item.priority);
            item.mirrorMode = (AkgfMirrorMode)EditorGUILayout.EnumPopup("Mirror Mode", item.mirrorMode);
            item.minimumTrackingQuality = EditorGUILayout.Slider("Min Tracking Quality", item.minimumTrackingQuality, 0f, 1f);
            item.qualityPenaltyStrength = EditorGUILayout.Slider("Quality Penalty", item.qualityPenaltyStrength, 0f, 1f);
            using (new EditorGUILayout.HorizontalScope())
            {
                item.fireOnEnter = GUILayout.Toggle(item.fireOnEnter, "Enter");
                item.fireOnStay = GUILayout.Toggle(item.fireOnStay, "Stay");
                item.fireOnExit = GUILayout.Toggle(item.fireOnExit, "Exit");
                item.fireOnConfirmed = GUILayout.Toggle(item.fireOnConfirmed, "Confirmed");
            }
            EditorGUILayout.EndVertical();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load Settings"))
            {
                settingsDatabase.LoadAll();
            }

            if (GUILayout.Button("Save Settings"))
            {
                SaveSettingsDatabase();
            }
        }

        EditorUtility.SetDirty(settingsDatabase);
        EditorGUILayout.EndVertical();
    }

    private void DrawGroupsSection()
    {
        showGroups = EditorGUILayout.Foldout(showGroups, "Gesture Groups", true);
        if (!showGroups)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        if (groupController == null)
        {
            EditorGUILayout.HelpBox("No AkgfGestureGroupController found.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        Undo.RecordObject(groupController, "Edit AKGF Groups");
        groupController.allowUngroupedGestures = EditorGUILayout.Toggle("Allow Ungrouped Gestures", groupController.allowUngroupedGestures);
        groupController.defaultStateForUnknownGroups = EditorGUILayout.Toggle("Unknown Groups Active By Default", groupController.defaultStateForUnknownGroups);

        if (groupController.groups == null)
        {
            groupController.groups = new List<AkgfGestureGroupState>();
        }

        for (int i = 0; i < groupController.groups.Count; i++)
        {
            AkgfGestureGroupState state = groupController.groups[i];
            if (state == null)
            {
                continue;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                state.active = EditorGUILayout.Toggle(state.active, GUILayout.Width(18));
                state.groupName = EditorGUILayout.TextField(state.groupName);
                if (GUILayout.Button("Only", GUILayout.Width(55)))
                {
                    groupController.ActivateOnly(state.groupName);
                }
                if (GUILayout.Button("X", GUILayout.Width(28)))
                {
                    groupController.groups.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
            }
        }

        if (GUILayout.Button("Add Default Group"))
        {
            groupController.SetGroupActive("Default", true);
        }

        EditorUtility.SetDirty(groupController);
        EditorGUILayout.EndVertical();
    }

    private void DrawRecordingSection()
    {
        showRecording = EditorGUILayout.Foldout(showRecording, "Recording / Hotkeys", true);
        if (!showRecording)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        if (staticRecorder != null)
        {
            Undo.RecordObject(staticRecorder, "Edit AKGF Static Recorder");
            EditorGUILayout.LabelField("Static Pose Recorder", EditorStyles.boldLabel);
            staticRecorder.gestureName = EditorGUILayout.TextField("Gesture Name", staticRecorder.gestureName);
            staticRecorder.recordDurationSeconds = Mathf.Max(0.05f, EditorGUILayout.FloatField("Record Duration", staticRecorder.recordDurationSeconds));
            staticRecorder.samplesPerSecond = Mathf.Max(1f, EditorGUILayout.FloatField("Samples Per Second", staticRecorder.samplesPerSecond));
            staticRecorder.recordKey = (KeyCode)EditorGUILayout.EnumPopup("Record Hotkey", staticRecorder.recordKey);
            staticRecorder.recordWithKeyboard = EditorGUILayout.Toggle("Use Hotkey", staticRecorder.recordWithKeyboard);
            EditorUtility.SetDirty(staticRecorder);
        }

        if (sequenceRecorder != null)
        {
            EditorGUILayout.Space(6);
            Undo.RecordObject(sequenceRecorder, "Edit AKGF Sequence Recorder");
            EditorGUILayout.LabelField("Sequence Recorder", EditorStyles.boldLabel);
            sequenceRecorder.gestureName = EditorGUILayout.TextField("Gesture Name", sequenceRecorder.gestureName);
            sequenceRecorder.recordDurationSeconds = Mathf.Max(0.05f, EditorGUILayout.FloatField("Timed Record Duration", sequenceRecorder.recordDurationSeconds));
            sequenceRecorder.samplesPerSecond = Mathf.Max(1f, EditorGUILayout.FloatField("Samples Per Second", sequenceRecorder.samplesPerSecond));
            sequenceRecorder.recordKey = (KeyCode)EditorGUILayout.EnumPopup("Record Hotkey", sequenceRecorder.recordKey);
            sequenceRecorder.recordWithKeyboard = EditorGUILayout.Toggle("Use Hotkey", sequenceRecorder.recordWithKeyboard);
            sequenceRecorder.manualStopMode = EditorGUILayout.Toggle("Hotkey Toggles Start/Stop", sequenceRecorder.manualStopMode);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || sequenceRecorder.IsRecording))
            {
                if (GUILayout.Button("Start Sequence Recording"))
                {
                    sequenceRecorder.StartManualRecording(sequenceRecorder.gestureName);
                }
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying || !sequenceRecorder.IsRecording))
            {
                if (GUILayout.Button("Stop & Save Sequence Recording"))
                {
                    sequenceRecorder.StopRecordingAndSave();
                }
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying || sequenceRecorder.IsRecording))
            {
                if (GUILayout.Button("Record Timed Sequence"))
                {
                    sequenceRecorder.StartRecording(sequenceRecorder.gestureName);
                }
            }

            if (sequenceRecorder.IsRecording)
            {
                EditorGUILayout.HelpBox($"Recording sequence ({(sequenceRecorder.IsManualRecording ? "manual" : "timed")})... Time: {sequenceRecorder.RecordingElapsedSeconds:0.00}s, Frames: {sequenceRecorder.CurrentFrameCount}", MessageType.Info);
            }

            if (!string.IsNullOrWhiteSpace(sequenceRecorder.LastSavedPath))
            {
                EditorGUILayout.HelpBox("Last saved: " + sequenceRecorder.LastSavedPath, MessageType.None);
            }

            if (!string.IsNullOrWhiteSpace(sequenceRecorder.LastError))
            {
                EditorGUILayout.HelpBox(sequenceRecorder.LastError, MessageType.Warning);
            }

            EditorUtility.SetDirty(sequenceRecorder);
        }

        if (calibrationRecorder != null)
        {
            EditorGUILayout.Space(6);
            Undo.RecordObject(calibrationRecorder, "Edit AKGF Calibration Recorder");
            EditorGUILayout.LabelField("Calibration Recorder", EditorStyles.boldLabel);
            calibrationRecorder.profileName = EditorGUILayout.TextField("Profile Name", calibrationRecorder.profileName);
            calibrationRecorder.recordDurationSeconds = Mathf.Max(0.05f, EditorGUILayout.FloatField("Record Duration", calibrationRecorder.recordDurationSeconds));
            calibrationRecorder.samplesPerSecond = Mathf.Max(1f, EditorGUILayout.FloatField("Samples Per Second", calibrationRecorder.samplesPerSecond));
            calibrationRecorder.recordHotkey = (KeyCode)EditorGUILayout.EnumPopup("Calibration Hotkey", calibrationRecorder.recordHotkey);
            calibrationRecorder.saveWhenFinished = EditorGUILayout.Toggle("Save When Finished", calibrationRecorder.saveWhenFinished);
            EditorUtility.SetDirty(calibrationRecorder);
        }

        if (recordingPanel != null)
        {
            EditorGUILayout.Space(6);
            Undo.RecordObject(recordingPanel, "Edit AKGF Recording Panel");
            recordingPanel.showPanel = EditorGUILayout.Toggle("Show Recording Panel", recordingPanel.showPanel);
            recordingPanel.toggleKey = (KeyCode)EditorGUILayout.EnumPopup("Panel Toggle Key", recordingPanel.toggleKey);
            EditorUtility.SetDirty(recordingPanel);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawDebugSection()
    {
        showDebug = EditorGUILayout.Foldout(showDebug, "Debug Overlays / Profiler", true);
        if (!showDebug)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        if (conflictVisualizer != null)
        {
            Undo.RecordObject(conflictVisualizer, "Edit AKGF Conflict Visualizer");
            conflictVisualizer.showOverlay = EditorGUILayout.Toggle("Show Conflict Visualizer", conflictVisualizer.showOverlay);
            conflictVisualizer.toggleKey = (KeyCode)EditorGUILayout.EnumPopup("Conflict Toggle Key", conflictVisualizer.toggleKey);
            EditorUtility.SetDirty(conflictVisualizer);
        }

        if (performanceProfiler != null)
        {
            Undo.RecordObject(performanceProfiler, "Edit AKGF Performance Profiler");
            performanceProfiler.showOverlay = EditorGUILayout.Toggle("Show Performance Profiler", performanceProfiler.showOverlay);
            performanceProfiler.toggleKey = (KeyCode)EditorGUILayout.EnumPopup("Profiler Toggle Key", performanceProfiler.toggleKey);
            EditorUtility.SetDirty(performanceProfiler);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Show All Debug Panels"))
            {
                SetDebugPanels(true);
            }

            if (GUILayout.Button("Hide All Debug Panels"))
            {
                SetDebugPanels(false);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawMultiUserSection()
    {
        showMultiUser = EditorGUILayout.Foldout(showMultiUser, "MultiUser Settings", true);
        if (!showMultiUser)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        if (multiUserManager == null)
        {
            EditorGUILayout.HelpBox("No AkgfMultiUserGestureManager found.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        Undo.RecordObject(multiUserManager, "Edit AKGF MultiUser Manager");
        multiUserManager.multiSkeletonSourceBehaviour = (MonoBehaviour)EditorGUILayout.ObjectField("Multi Skeleton Source", multiUserManager.multiSkeletonSourceBehaviour, typeof(MonoBehaviour), true);
        multiUserManager.enableStaticPoseRecognition = EditorGUILayout.Toggle("Enable Static Recognition", multiUserManager.enableStaticPoseRecognition);
        multiUserManager.enableSequenceRecognition = EditorGUILayout.Toggle("Enable Sequence Recognition", multiUserManager.enableSequenceRecognition);
        multiUserManager.defaultStaticMinimumSimilarity = EditorGUILayout.Slider("Default Static Similarity", multiUserManager.defaultStaticMinimumSimilarity, 0f, 1f);
        multiUserManager.defaultSequenceMinimumSimilarity = EditorGUILayout.Slider("Default Sequence Similarity", multiUserManager.defaultSequenceMinimumSimilarity, 0f, 1f);
        multiUserManager.defaultStaticStableSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Default Static Stable Seconds", multiUserManager.defaultStaticStableSeconds));
        multiUserManager.defaultStaticCooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Default Static Cooldown", multiUserManager.defaultStaticCooldownSeconds));
        multiUserManager.defaultSequenceCooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Default Sequence Cooldown", multiUserManager.defaultSequenceCooldownSeconds));
        multiUserManager.sequenceHasPriority = EditorGUILayout.Toggle("Sequence Has Priority", multiUserManager.sequenceHasPriority);
        multiUserManager.globalCooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Global Cooldown", multiUserManager.globalCooldownSeconds));
        multiUserManager.sameGestureCooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Same Gesture Cooldown", multiUserManager.sameGestureCooldownSeconds));
        multiUserManager.useStableUserIds = EditorGUILayout.Toggle("Use Stable User IDs", multiUserManager.useStableUserIds);
        multiUserManager.autoCalibrateNewUsers = EditorGUILayout.Toggle("Auto Calibrate New Users", multiUserManager.autoCalibrateNewUsers);
        multiUserManager.requireCalibrationBeforeRecognition = EditorGUILayout.Toggle("Require Calibration Before Recognition", multiUserManager.requireCalibrationBeforeRecognition);
        multiUserManager.calibrationSeconds = Mathf.Max(0.1f, EditorGUILayout.FloatField("Calibration Seconds", multiUserManager.calibrationSeconds));
        multiUserManager.lostUserTimeoutSeconds = Mathf.Max(0.1f, EditorGUILayout.FloatField("Lost User Timeout", multiUserManager.lostUserTimeoutSeconds));
        multiUserManager.emitDetectedPhase = EditorGUILayout.Toggle("Emit Detected", multiUserManager.emitDetectedPhase);
        multiUserManager.emitEnterPhase = EditorGUILayout.Toggle("Emit Enter", multiUserManager.emitEnterPhase);
        multiUserManager.emitStayPhase = EditorGUILayout.Toggle("Emit Stay", multiUserManager.emitStayPhase);
        multiUserManager.emitExitPhase = EditorGUILayout.Toggle("Emit Exit", multiUserManager.emitExitPhase);
        multiUserManager.emitConfirmedPhase = EditorGUILayout.Toggle("Emit Confirmed", multiUserManager.emitConfirmedPhase);

        if (GUILayout.Button("Clear MultiUser Runtime Users"))
        {
            multiUserManager.ClearUsers();
        }

        EditorUtility.SetDirty(multiUserManager);
        EditorGUILayout.EndVertical();
    }

    private void DrawRuntimeStatusSection()
    {
        showRuntimeStatus = EditorGUILayout.Foldout(showRuntimeStatus, "Runtime Status", true);
        if (!showRuntimeStatus)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Mode", modeManager != null ? modeManager.trackingMode.ToString() : "Unknown");
        EditorGUILayout.LabelField("Static Candidate", Format(staticRecognizer != null ? staticRecognizer.LastMatch : AkgfGestureMatchResult.None));
        EditorGUILayout.LabelField("Sequence Candidate", Format(sequenceRecognizer != null ? sequenceRecognizer.LastMatch : AkgfGestureMatchResult.None));
        EditorGUILayout.LabelField("Final Output", Format(coordinator != null ? coordinator.LastOutput : AkgfGestureMatchResult.None));
        if (api != null && api.LastGesture != null)
        {
            EditorGUILayout.LabelField("API Last Gesture", $"{api.LastGesture.gestureName} {api.LastGesture.phase} {api.LastGesture.confidence:0.00}");
        }
        else
        {
            EditorGUILayout.LabelField("API Last Gesture", "None");
        }

        if (multiUserManager != null)
        {
            EditorGUILayout.LabelField("MultiUser", $"Visible: {multiUserManager.VisibleBodyCount}  Active: {multiUserManager.ActiveUserCount}");
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawModeManagerMini()
    {
        if (modeManager == null)
        {
            return;
        }

        Undo.RecordObject(modeManager, "Edit AKGF Mode");
        AkgfTrackingMode newMode = (AkgfTrackingMode)EditorGUILayout.EnumPopup("Tracking Mode", modeManager.trackingMode);
        if (newMode != modeManager.trackingMode)
        {
            modeManager.trackingMode = newMode;
            modeManager.ApplyMode();
        }
        modeManager.applyOnStart = EditorGUILayout.Toggle("Apply Mode On Start", modeManager.applyOnStart);
        modeManager.applyInEditorOnValidate = EditorGUILayout.Toggle("Apply Mode In Editor", modeManager.applyInEditorOnValidate);
        EditorUtility.SetDirty(modeManager);
    }

    private void ResolveReferences()
    {
        modeManager = FindSceneObject<AkgfGestureSystemModeManager>();
        staticRecognizer = FindSceneObject<AkgfGestureRecognizer>();
        sequenceRecognizer = FindSceneObject<AkgfSequenceGestureRecognizer>();
        coordinator = FindSceneObject<AkgfGestureCoordinator>();
        settingsDatabase = FindSceneObject<AkgfGestureSettingsDatabase>();
        groupController = FindSceneObject<AkgfGestureGroupController>();
        gestureDatabase = FindSceneObject<AkgfGestureDatabase>();
        sequenceDatabase = FindSceneObject<AkgfSequenceGestureDatabase>();
        staticRecorder = FindSceneObject<AkgfGestureRecorder>();
        sequenceRecorder = FindSceneObject<AkgfSequenceGestureRecorder>();
        calibrationRecorder = FindSceneObject<AkgfCalibrationRecorder>();
        recordingPanel = FindSceneObject<AkgfRecordingPanel>();
        conflictVisualizer = FindSceneObject<AkgfGestureConflictVisualizer>();
        performanceProfiler = FindSceneObject<AkgfPerformanceProfiler>();
        api = FindSceneObject<AkgfGestureSystemApi>();
        multiUserManager = FindSceneObject<AkgfMultiUserGestureManager>();
    }

    private static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        T[] all = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < all.Length; i++)
        {
            T item = all[i];
            if (item == null || EditorUtility.IsPersistent(item))
            {
                continue;
            }

            Component component = item as Component;
            if (component != null && component.gameObject.scene.IsValid())
            {
                return item;
            }

            GameObject go = item as GameObject;
            if (go != null && go.scene.IsValid())
            {
                return item;
            }
        }

        return null;
    }

    private static MonoBehaviour FindSkeletonSourceBehaviour(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return null;
        }

        MonoBehaviour mb = obj as MonoBehaviour;
        if (mb != null && mb is IAkgfSkeletonSource)
        {
            return mb;
        }

        GameObject go = obj as GameObject;
        if (go == null && obj is Component component)
        {
            go = component.gameObject;
        }

        if (go == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IAkgfSkeletonSource)
            {
                return behaviours[i];
            }
        }

        return null;
    }

    private static MonoBehaviour FindMultiSkeletonSourceBehaviour(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return null;
        }

        MonoBehaviour mb = obj as MonoBehaviour;
        if (mb != null && mb is IAkgfMultiSkeletonSource)
        {
            return mb;
        }

        GameObject go = obj as GameObject;
        if (go == null && obj is Component component)
        {
            go = component.gameObject;
        }

        if (go == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IAkgfMultiSkeletonSource)
            {
                return behaviours[i];
            }
        }

        return null;
    }

    private void ApplySourceToSingleUser(MonoBehaviour source)
    {
        if (source == null)
        {
            EditorUtility.DisplayDialog("AKGF", "The selected object does not contain a component that implements IAkgfSkeletonSource.", "OK");
            return;
        }

        Undo.RecordObjects(new UnityEngine.Object[] { staticRecognizer, sequenceRecognizer, staticRecorder, sequenceRecorder, calibrationRecorder }, "Assign AKGF SingleUser Source");
        if (staticRecognizer != null) staticRecognizer.skeletonSourceBehaviour = source;
        if (sequenceRecognizer != null) sequenceRecognizer.skeletonSourceBehaviour = source;
        if (staticRecorder != null) staticRecorder.skeletonSourceBehaviour = source;
        if (sequenceRecorder != null) sequenceRecorder.skeletonSourceBehaviour = source;
        if (calibrationRecorder != null) calibrationRecorder.skeletonSourceBehaviour = source;
        MarkDirty(staticRecognizer, sequenceRecognizer, staticRecorder, sequenceRecorder, calibrationRecorder);
    }

    private void ApplySourceToMultiUser(MonoBehaviour source)
    {
        if (source == null)
        {
            EditorUtility.DisplayDialog("AKGF", "The selected object does not contain a component that implements IAkgfMultiSkeletonSource.", "OK");
            return;
        }

        Undo.RecordObject(multiUserManager, "Assign AKGF MultiUser Source");
        if (multiUserManager != null)
        {
            multiUserManager.multiSkeletonSourceBehaviour = source;
            EditorUtility.SetDirty(multiUserManager);
        }
    }

    private void ApplySingleUserDebugMode()
    {
        if (modeManager != null)
        {
            Undo.RecordObject(modeManager, "AKGF Debug Mode");
            modeManager.trackingMode = AkgfTrackingMode.SingleUser;
            modeManager.ApplyMode();
            EditorUtility.SetDirty(modeManager);
        }

        if (staticRecognizer != null)
        {
            Undo.RecordObject(staticRecognizer, "AKGF Debug Mode");
            staticRecognizer.minimumSimilarity = 0.50f;
            staticRecognizer.requiredStableSeconds = 0f;
            staticRecognizer.sameGestureCooldownSeconds = 0f;
            staticRecognizer.recognizeEveryFrame = true;
            staticRecognizer.fireUnityEventDirectly = false;
            EditorUtility.SetDirty(staticRecognizer);
        }

        if (sequenceRecognizer != null)
        {
            Undo.RecordObject(sequenceRecognizer, "AKGF Debug Mode");
            sequenceRecognizer.minimumSimilarity = 0.50f;
            sequenceRecognizer.requiredConsecutiveMatches = 1;
            sequenceRecognizer.sameGestureCooldownSeconds = 0f;
            sequenceRecognizer.recognitionsPerSecond = 15f;
            sequenceRecognizer.fireUnityEventDirectly = false;
            EditorUtility.SetDirty(sequenceRecognizer);
        }

        if (coordinator != null)
        {
            Undo.RecordObject(coordinator, "AKGF Debug Mode");
            coordinator.globalCooldownSeconds = 0f;
            coordinator.sameGestureCooldownSeconds = 0.50f;
            coordinator.sequenceBlocksStaticSeconds = 0f;
            coordinator.acceptCurrentCandidateDirectly = true;
            coordinator.directStaticMinimumSimilarity = 0.25f;
            coordinator.directSequenceMinimumSimilarity = 0.35f;
            coordinator.usePerGestureThresholdForDirectCandidates = false;
            coordinator.useRecognizerThresholdForDirectCandidates = false;
            coordinator.debugForceEmitBestCandidateAsResult = false;
            coordinator.emitDetectedPhase = false;
            coordinator.emitEnterPhase = true;
            coordinator.emitStayPhase = false;
            coordinator.emitExitPhase = false;
            coordinator.emitConfirmedPhase = false;
            coordinator.Reconnect();
            EditorUtility.SetDirty(coordinator);
        }

        if (settingsDatabase != null)
        {
            Undo.RecordObject(settingsDatabase, "AKGF Debug Mode");
            settingsDatabase.defaultStaticMinimumSimilarity = 0.50f;
            settingsDatabase.defaultSequenceMinimumSimilarity = 0.50f;
            settingsDatabase.defaultStaticStableSeconds = 0f;
            settingsDatabase.defaultStaticCooldownSeconds = 0f;
            settingsDatabase.defaultSequenceCooldownSeconds = 0f;
            settingsDatabase.defaultMinimumTrackingQuality = 0f;
            settingsDatabase.defaultGroupName = "Default";
            EditorUtility.SetDirty(settingsDatabase);
        }

        if (groupController != null)
        {
            Undo.RecordObject(groupController, "AKGF Debug Mode");
            groupController.allowUngroupedGestures = true;
            groupController.defaultStateForUnknownGroups = true;
            groupController.SetGroupActive("Default", true);
            EditorUtility.SetDirty(groupController);
        }

        if (api != null)
        {
            api.Reconnect();
        }
    }


    private void ApplyForceCandidateResultMode()
    {
        ApplySingleUserDebugMode();

        if (coordinator != null)
        {
            Undo.RecordObject(coordinator, "AKGF Force Candidate Result Mode");
            coordinator.debugForceEmitBestCandidateAsResult = true;
            coordinator.debugForceEmitIntervalSeconds = 0.20f;
            coordinator.globalCooldownSeconds = 0f;
            coordinator.sameGestureCooldownSeconds = 0f;
            coordinator.emitDetectedPhase = true;
            coordinator.emitEnterPhase = true;
            coordinator.emitStayPhase = true;
            coordinator.emitExitPhase = false;
            coordinator.emitConfirmedPhase = false;
            coordinator.Reconnect();
            EditorUtility.SetDirty(coordinator);
        }

        if (api != null)
        {
            api.Reconnect();
        }
    }

    private void ApplySingleUserNormalDefaults()
    {
        if (staticRecognizer != null)
        {
            Undo.RecordObject(staticRecognizer, "AKGF Normal Defaults");
            staticRecognizer.minimumSimilarity = 0.82f;
            staticRecognizer.requiredStableSeconds = 0.20f;
            staticRecognizer.sameGestureCooldownSeconds = 0.75f;
            staticRecognizer.recognizeEveryFrame = false;
            staticRecognizer.fireUnityEventDirectly = false;
            EditorUtility.SetDirty(staticRecognizer);
        }

        if (sequenceRecognizer != null)
        {
            Undo.RecordObject(sequenceRecognizer, "AKGF Normal Defaults");
            sequenceRecognizer.minimumSimilarity = 0.72f;
            sequenceRecognizer.requiredConsecutiveMatches = 2;
            sequenceRecognizer.sameGestureCooldownSeconds = 1f;
            sequenceRecognizer.recognitionsPerSecond = 10f;
            sequenceRecognizer.fireUnityEventDirectly = false;
            EditorUtility.SetDirty(sequenceRecognizer);
        }

        if (coordinator != null)
        {
            Undo.RecordObject(coordinator, "AKGF Normal Defaults");
            coordinator.globalCooldownSeconds = 0.08f;
            coordinator.sameGestureCooldownSeconds = 0.75f;
            coordinator.sequenceBlocksStaticSeconds = 0.60f;
            coordinator.acceptCurrentCandidateDirectly = true;
            coordinator.directStaticMinimumSimilarity = 0.45f;
            coordinator.directSequenceMinimumSimilarity = 0.55f;
            coordinator.usePerGestureThresholdForDirectCandidates = false;
            coordinator.useRecognizerThresholdForDirectCandidates = false;
            coordinator.emitDetectedPhase = false;
            coordinator.emitEnterPhase = true;
            coordinator.emitStayPhase = false;
            coordinator.emitExitPhase = true;
            coordinator.emitConfirmedPhase = false;
            coordinator.debugForceEmitBestCandidateAsResult = false;
            coordinator.Reconnect();
            EditorUtility.SetDirty(coordinator);
        }
    }

    private void ImportRecordedGesturesIntoSettings()
    {
        if (settingsDatabase == null)
        {
            EditorUtility.DisplayDialog("AKGF", "No AkgfGestureSettingsDatabase found.", "OK");
            return;
        }

        gestureDatabase?.LoadAll();
        sequenceDatabase?.LoadAll();

        if (gestureDatabase != null)
        {
            foreach (AkgfGestureData gesture in gestureDatabase.Gestures)
            {
                if (gesture != null && !string.IsNullOrWhiteSpace(gesture.gestureName))
                {
                    settingsDatabase.AddOrReplace(settingsDatabase.CreateRuntimeDefault(gesture.gestureName, AkgfGestureKind.StaticPose));
                }
            }
        }

        if (sequenceDatabase != null)
        {
            foreach (AkgfSequenceGestureData gesture in sequenceDatabase.Gestures)
            {
                if (gesture != null && !string.IsNullOrWhiteSpace(gesture.gestureName))
                {
                    settingsDatabase.AddOrReplace(settingsDatabase.CreateRuntimeDefault(gesture.gestureName, AkgfGestureKind.Sequence));
                }
            }
        }

        EditorUtility.SetDirty(settingsDatabase);
    }

    private void SaveSettingsDatabase()
    {
        if (settingsDatabase == null)
        {
            return;
        }

        string path = settingsDatabase.SaveAll();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("AKGF", "Saved gesture settings to:\n" + path, "OK");
    }

    private void SetDebugPanels(bool value)
    {
        if (recordingPanel != null)
        {
            Undo.RecordObject(recordingPanel, "Toggle AKGF Debug Panels");
            recordingPanel.showPanel = value;
            EditorUtility.SetDirty(recordingPanel);
        }

        if (conflictVisualizer != null)
        {
            Undo.RecordObject(conflictVisualizer, "Toggle AKGF Debug Panels");
            conflictVisualizer.showOverlay = value;
            EditorUtility.SetDirty(conflictVisualizer);
        }

        if (performanceProfiler != null)
        {
            Undo.RecordObject(performanceProfiler, "Toggle AKGF Debug Panels");
            performanceProfiler.showOverlay = value;
            EditorUtility.SetDirty(performanceProfiler);
        }
    }

    private static void MarkDirty(params UnityEngine.Object[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                EditorUtility.SetDirty(objects[i]);
            }
        }
    }

    private static void DrawObjectLine(string label, UnityEngine.Object obj)
    {
        EditorGUILayout.ObjectField(label, obj, typeof(UnityEngine.Object), true);
    }

    private static string Format(AkgfGestureMatchResult match)
    {
        if (!match.isValid)
        {
            return "None";
        }

        return $"{match.gestureName} {match.gestureKind} {match.phase} {match.similarity:0.00}";
    }
}
