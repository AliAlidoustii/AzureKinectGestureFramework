using System.Collections.Generic;
using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

public sealed class AkgfGestureTuningWindow : EditorWindow
{
    private AkgfGestureSettingsDatabase settingsDatabase;
    private AkgfGestureDatabase staticDatabase;
    private AkgfSequenceGestureDatabase sequenceDatabase;
    private Vector2 scroll;
    private string newGestureName = "NewGesture";
    private AkgfGestureKind newGestureKind = AkgfGestureKind.StaticPose;

    [MenuItem("Tools/Azure Kinect Gesture Framework/Gesture Tuning Window", false, 40)]
    public static void Open()
    {
        GetWindow<AkgfGestureTuningWindow>("AKGF Tuning");
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void OnGUI()
    {
        DrawToolbar();
        GUILayout.Space(6);

        if (settingsDatabase == null)
        {
            EditorGUILayout.HelpBox("No AkgfGestureSettingsDatabase found in the open scene. Create the gesture system first.", MessageType.Warning);
            if (GUILayout.Button("Find Again"))
            {
                ResolveReferences();
            }
            return;
        }

        DrawAddSection();
        GUILayout.Space(8);
        DrawKnownGestureImportSection();
        GUILayout.Space(8);
        DrawSettingsList();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Find", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ResolveReferences();
            }
            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                settingsDatabase?.LoadAll();
            }
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                SaveSettings();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(settingsDatabase != null ? settingsDatabase.name : "No settings database");
        }
    }

    private void DrawAddSection()
    {
        EditorGUILayout.LabelField("Add / Replace Gesture Settings", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            newGestureName = EditorGUILayout.TextField("Gesture", newGestureName);
            newGestureKind = (AkgfGestureKind)EditorGUILayout.EnumPopup(newGestureKind, GUILayout.Width(110));
            if (GUILayout.Button("Add", GUILayout.Width(70)))
            {
                settingsDatabase.AddOrReplace(settingsDatabase.CreateRuntimeDefault(newGestureName, newGestureKind));
                EditorUtility.SetDirty(settingsDatabase);
            }
        }
    }

    private void DrawKnownGestureImportSection()
    {
        EditorGUILayout.LabelField("Create Settings From Recorded Gestures", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Import Static Gestures"))
            {
                ImportStaticGestures();
            }
            if (GUILayout.Button("Import Sequence Gestures"))
            {
                ImportSequenceGestures();
            }
        }
    }

    private void DrawSettingsList()
    {
        IReadOnlyList<AkgfGestureSettings> settings = settingsDatabase.Settings;
        EditorGUILayout.LabelField($"Settings ({settings.Count})", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        for (int i = 0; i < settings.Count; i++)
        {
            AkgfGestureSettings item = settings[i];
            if (item == null)
            {
                continue;
            }

            EditorGUILayout.BeginVertical("box");
            using (new EditorGUILayout.HorizontalScope())
            {
                item.enabled = EditorGUILayout.Toggle(item.enabled, GUILayout.Width(18));
                item.gestureName = EditorGUILayout.TextField(item.gestureName);
                item.gestureKind = (AkgfGestureKind)EditorGUILayout.EnumPopup(item.gestureKind, GUILayout.Width(110));
            }

            item.groupName = EditorGUILayout.TextField("Group", item.groupName);
            item.minimumSimilarity = EditorGUILayout.Slider("Min Similarity", item.minimumSimilarity, 0f, 1f);
            item.requiredStableSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Hold / Stable Seconds", item.requiredStableSeconds));
            item.cooldownSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Cooldown Seconds", item.cooldownSeconds));
            item.priority = EditorGUILayout.IntField("Priority", item.priority);
            item.mirrorMode = (AkgfMirrorMode)EditorGUILayout.EnumPopup("Mirror Mode", item.mirrorMode);
            item.minimumTrackingQuality = EditorGUILayout.Slider("Min Tracking Quality", item.minimumTrackingQuality, 0f, 1f);
            item.qualityPenaltyStrength = EditorGUILayout.Slider("Quality Penalty", item.qualityPenaltyStrength, 0f, 1f);

            EditorGUILayout.LabelField("Event Phases", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                item.fireOnEnter = GUILayout.Toggle(item.fireOnEnter, "Enter");
                item.fireOnStay = GUILayout.Toggle(item.fireOnStay, "Stay");
                item.fireOnExit = GUILayout.Toggle(item.fireOnExit, "Exit");
                item.fireOnConfirmed = GUILayout.Toggle(item.fireOnConfirmed, "Confirmed");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                {
                    RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(settingsDatabase);
        }
    }

    private void ResolveReferences()
    {
        settingsDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureSettingsDatabase>();
        staticDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureDatabase>();
        sequenceDatabase = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureDatabase>();
    }

    private void SaveSettings()
    {
        if (settingsDatabase == null)
        {
            return;
        }

        string path = settingsDatabase.SaveAll();
        EditorUtility.DisplayDialog("AKGF", "Saved gesture settings to:\n" + path, "OK");
        AssetDatabase.Refresh();
    }

    private void ImportStaticGestures()
    {
        if (staticDatabase == null)
        {
            staticDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureDatabase>();
        }
        staticDatabase?.LoadAll();
        if (staticDatabase == null)
        {
            return;
        }
        foreach (AkgfGestureData gesture in staticDatabase.Gestures)
        {
            if (gesture != null && !string.IsNullOrWhiteSpace(gesture.gestureName))
            {
                settingsDatabase.AddOrReplace(settingsDatabase.CreateRuntimeDefault(gesture.gestureName, AkgfGestureKind.StaticPose));
            }
        }
        EditorUtility.SetDirty(settingsDatabase);
    }

    private void ImportSequenceGestures()
    {
        if (sequenceDatabase == null)
        {
            sequenceDatabase = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureDatabase>();
        }
        sequenceDatabase?.LoadAll();
        if (sequenceDatabase == null)
        {
            return;
        }
        foreach (AkgfSequenceGestureData gesture in sequenceDatabase.Gestures)
        {
            if (gesture != null && !string.IsNullOrWhiteSpace(gesture.gestureName))
            {
                settingsDatabase.AddOrReplace(settingsDatabase.CreateRuntimeDefault(gesture.gestureName, AkgfGestureKind.Sequence));
            }
        }
        EditorUtility.SetDirty(settingsDatabase);
    }

    private void RemoveAt(int index)
    {
        // Settings is exposed as IReadOnlyList, but the objects are owned by the scene component.
        SerializedObject serialized = new SerializedObject(settingsDatabase);
        SerializedProperty list = serialized.FindProperty("settings");
        if (list != null && index >= 0 && index < list.arraySize)
        {
            list.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(settingsDatabase);
        }
    }
}
