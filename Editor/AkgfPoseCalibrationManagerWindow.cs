using System;
using System.Collections.Generic;
using System.IO;
using AzureKinectGestureFramework;
using UnityEditor;
using UnityEngine;

public sealed class AkgfPoseCalibrationManagerWindow : EditorWindow
{
    private enum Tab
    {
        StaticPoses,
        SequenceGestures,
        Calibrations
    }

    private enum SourceLocation
    {
        ProjectResources,
        PersistentData
    }

    private sealed class ManagedFile
    {
        public string name;
        public string path;
        public string displayPath;
        public string modifiedUtc;
        public int sampleCount;
        public bool usable;
        public SourceLocation location;
        public string notes;
        public string error;
    }

    private Tab currentTab = Tab.StaticPoses;
    private Vector2 scroll;
    private string search = string.Empty;
    private bool showProjectResources = true;
    private bool showPersistentData = true;
    private bool confirmDeletes = true;

    private AkgfGestureDatabase staticDatabase;
    private AkgfSequenceGestureDatabase sequenceDatabase;
    private AkgfCalibrationDatabase calibrationDatabase;
    private AkgfGestureSettingsDatabase settingsDatabase;

    private readonly List<ManagedFile> staticFiles = new List<ManagedFile>();
    private readonly List<ManagedFile> sequenceFiles = new List<ManagedFile>();
    private readonly List<ManagedFile> calibrationFiles = new List<ManagedFile>();

    [MenuItem("Tools/Azure Kinect Gesture Framework/Pose & Calibration Manager", false, 6)]
    public static void Open()
    {
        GetWindow<AkgfPoseCalibrationManagerWindow>("AKGF Pose Manager");
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshLists();
    }

    private void OnFocus()
    {
        RefreshLists();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawReferenceStatus();
        DrawTabs();
        DrawFilters();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (currentTab)
        {
            case Tab.StaticPoses:
                DrawStaticPoseSection();
                break;
            case Tab.SequenceGestures:
                DrawSequenceGestureSection();
                break;
            case Tab.Calibrations:
                DrawCalibrationSection();
                break;
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RefreshLists();
            }

            if (GUILayout.Button("Find Scene DBs", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                ResolveReferences();
                RefreshLists();
            }

            if (GUILayout.Button("Reload Databases", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                ReloadRuntimeDatabases();
            }

            GUILayout.FlexibleSpace();
            search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField, GUILayout.Width(220));
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45)))
            {
                search = string.Empty;
                GUI.FocusControl(null);
            }
        }
    }

    private void DrawReferenceStatus()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Scene References", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            staticDatabase = (AkgfGestureDatabase)EditorGUILayout.ObjectField("Static DB", staticDatabase, typeof(AkgfGestureDatabase), true);
            sequenceDatabase = (AkgfSequenceGestureDatabase)EditorGUILayout.ObjectField("Sequence DB", sequenceDatabase, typeof(AkgfSequenceGestureDatabase), true);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            calibrationDatabase = (AkgfCalibrationDatabase)EditorGUILayout.ObjectField("Calibration DB", calibrationDatabase, typeof(AkgfCalibrationDatabase), true);
            settingsDatabase = (AkgfGestureSettingsDatabase)EditorGUILayout.ObjectField("Settings DB", settingsDatabase, typeof(AkgfGestureSettingsDatabase), true);
        }

        EditorGUILayout.HelpBox("This window manages JSON files saved by AKGF. Use Reload Databases after deleting or adding files so the running scene uses the latest static poses, sequence gestures, and calibration profile.", MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private void DrawTabs()
    {
        currentTab = (Tab)GUILayout.Toolbar((int)currentTab, new[]
        {
            "Static Poses",
            "Sequence Gestures",
            "Calibrations"
        });
    }

    private void DrawFilters()
    {
        EditorGUILayout.BeginVertical("box");
        using (new EditorGUILayout.HorizontalScope())
        {
            showProjectResources = EditorGUILayout.ToggleLeft("Project Resources", showProjectResources, GUILayout.Width(150));
            showPersistentData = EditorGUILayout.ToggleLeft("Persistent Data", showPersistentData, GUILayout.Width(140));
            confirmDeletes = EditorGUILayout.ToggleLeft("Confirm Deletes", confirmDeletes, GUILayout.Width(130));
            GUILayout.FlexibleSpace();
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawStaticPoseSection()
    {
        DrawFolderActions("Static Pose Folders", GetStaticAssetFolder(), GetStaticPersistentFolder());
        DrawFileList(staticFiles, "No static pose JSON files were found.", DrawStaticPoseRow);
    }

    private void DrawSequenceGestureSection()
    {
        DrawFolderActions("Sequence Gesture Folders", GetSequenceAssetFolder(), GetSequencePersistentFolder());
        DrawFileList(sequenceFiles, "No sequence gesture JSON files were found.", DrawSequenceGestureRow);
    }

    private void DrawCalibrationSection()
    {
        DrawFolderActions("Calibration Profile Folders", GetCalibrationAssetFolder(), GetCalibrationPersistentFolder());

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Active Calibration", EditorStyles.boldLabel);
        if (calibrationDatabase == null)
        {
            EditorGUILayout.HelpBox("No AkgfCalibrationDatabase found. Create/assign an AKGF gesture system first if you want to load a profile into the scene.", MessageType.Warning);
        }
        else if (calibrationDatabase.activeProfile == null)
        {
            EditorGUILayout.HelpBox("No active calibration profile is currently loaded.", MessageType.None);
        }
        else
        {
            EditorGUILayout.LabelField("Profile", calibrationDatabase.activeProfile.profileName);
            EditorGUILayout.LabelField("Samples", calibrationDatabase.activeProfile.sampleCount.ToString());
            EditorGUILayout.LabelField("Usable", calibrationDatabase.activeProfile.IsUsable ? "Yes" : "No");
            if (GUILayout.Button("Save Current Active Calibration To Default File"))
            {
                SaveActiveCalibration();
            }
        }
        EditorGUILayout.EndVertical();

        DrawFileList(calibrationFiles, "No calibration profile JSON files were found.", DrawCalibrationRow);
    }

    private void DrawFolderActions(string title, string assetFolder, string persistentFolder)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Project Resources", ShortenPath(assetFolder));
            if (GUILayout.Button("Create", GUILayout.Width(60)))
            {
                CreateFolder(assetFolder);
            }
            if (GUILayout.Button("Open", GUILayout.Width(60)))
            {
                RevealPath(assetFolder);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Persistent Data", ShortenPath(persistentFolder));
            if (GUILayout.Button("Create", GUILayout.Width(60)))
            {
                CreateFolder(persistentFolder);
            }
            if (GUILayout.Button("Open", GUILayout.Width(60)))
            {
                RevealPath(persistentFolder);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private delegate void RowDrawer(ManagedFile file);

    private void DrawFileList(List<ManagedFile> files, string emptyMessage, RowDrawer rowDrawer)
    {
        List<ManagedFile> visible = Filter(files);
        if (visible.Count == 0)
        {
            EditorGUILayout.HelpBox(emptyMessage, MessageType.None);
            return;
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"Files ({visible.Count})", EditorStyles.boldLabel);
        for (int i = 0; i < visible.Count; i++)
        {
            rowDrawer(visible[i]);
            if (i < visible.Count - 1)
            {
                EditorGUILayout.Space(3);
                DrawThinLine();
                EditorGUILayout.Space(3);
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawStaticPoseRow(ManagedFile file)
    {
        DrawCommonFileHeader(file, "Static Pose");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load / Reload DB"))
            {
                LoadStaticPoseFile(file);
            }
            if (GUILayout.Button("Add To Settings"))
            {
                AddGestureToSettings(file.name, AkgfGestureKind.StaticPose);
            }
            DrawCommonFileButtons(file);
        }
    }

    private void DrawSequenceGestureRow(ManagedFile file)
    {
        DrawCommonFileHeader(file, "Sequence Gesture");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load / Reload DB"))
            {
                LoadSequenceGestureFile(file);
            }
            if (GUILayout.Button("Add To Settings"))
            {
                AddGestureToSettings(file.name, AkgfGestureKind.Sequence);
            }
            DrawCommonFileButtons(file);
        }
    }

    private void DrawCalibrationRow(ManagedFile file)
    {
        DrawCommonFileHeader(file, "Calibration");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load As Active"))
            {
                LoadCalibrationAsActive(file, false);
            }
            if (GUILayout.Button("Load + Save As Default"))
            {
                LoadCalibrationAsActive(file, true);
            }
            DrawCommonFileButtons(file);
        }
    }

    private void DrawCommonFileHeader(ManagedFile file, string typeLabel)
    {
        EditorGUILayout.BeginVertical("box");
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(file.name, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(typeLabel, EditorStyles.miniLabel, GUILayout.Width(110));
            GUILayout.Label(file.location == SourceLocation.ProjectResources ? "Project" : "Persistent", EditorStyles.miniLabel, GUILayout.Width(75));
            GUILayout.Label(file.usable ? "Usable" : "Not usable", file.usable ? EditorStyles.miniLabel : EditorStyles.boldLabel, GUILayout.Width(80));
        }

        EditorGUILayout.LabelField("File", file.displayPath);
        EditorGUILayout.LabelField("Modified", file.modifiedUtc);
        EditorGUILayout.LabelField("Samples", file.sampleCount.ToString());
        if (!string.IsNullOrWhiteSpace(file.notes))
        {
            EditorGUILayout.LabelField("Notes", file.notes);
        }
        if (!string.IsNullOrWhiteSpace(file.error))
        {
            EditorGUILayout.HelpBox(file.error, MessageType.Warning);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawCommonFileButtons(ManagedFile file)
    {
        if (GUILayout.Button("Select", GUILayout.Width(70)))
        {
            SelectFile(file.path);
        }
        if (GUILayout.Button("Reveal", GUILayout.Width(70)))
        {
            RevealPath(file.path);
        }
        if (GUILayout.Button("Open JSON", GUILayout.Width(90)))
        {
            OpenFile(file.path);
        }
        GUI.backgroundColor = new Color(1f, 0.65f, 0.65f);
        if (GUILayout.Button("Delete", GUILayout.Width(70)))
        {
            DeleteFile(file);
        }
        GUI.backgroundColor = Color.white;
    }

    private void ResolveReferences()
    {
        staticDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureDatabase>();
        sequenceDatabase = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureDatabase>();
        calibrationDatabase = AkgfUnityObjectFinder.FindFirst<AkgfCalibrationDatabase>();
        settingsDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureSettingsDatabase>();
    }

    private void RefreshLists()
    {
        staticFiles.Clear();
        sequenceFiles.Clear();
        calibrationFiles.Clear();

        AddStaticFiles(GetStaticAssetFolder(), SourceLocation.ProjectResources);
        AddStaticFiles(GetStaticPersistentFolder(), SourceLocation.PersistentData);
        AddSequenceFiles(GetSequenceAssetFolder(), SourceLocation.ProjectResources);
        AddSequenceFiles(GetSequencePersistentFolder(), SourceLocation.PersistentData);
        AddCalibrationFiles(GetCalibrationAssetFolder(), SourceLocation.ProjectResources);
        AddCalibrationFiles(GetCalibrationPersistentFolder(), SourceLocation.PersistentData);

        SortByName(staticFiles);
        SortByName(sequenceFiles);
        SortByName(calibrationFiles);

        Repaint();
    }

    private void AddStaticFiles(string folder, SourceLocation location)
    {
        foreach (string file in EnumerateJsonFiles(folder))
        {
            ManagedFile managed = CreateBaseFile(file, location);
            try
            {
                AkgfGestureData data = JsonUtility.FromJson<AkgfGestureData>(File.ReadAllText(GetAbsolutePath(file)));
                if (data != null)
                {
                    data.EnsureValid();
                    managed.name = data.gestureName;
                    managed.sampleCount = data.samples != null ? data.samples.Count : 0;
                    managed.usable = data.IsUsable;
                    managed.notes = data.notes;
                    managed.modifiedUtc = FirstNonEmpty(data.modifiedUtc, managed.modifiedUtc);
                }
            }
            catch (Exception e)
            {
                managed.error = e.Message;
                managed.usable = false;
            }
            staticFiles.Add(managed);
        }
    }

    private void AddSequenceFiles(string folder, SourceLocation location)
    {
        foreach (string file in EnumerateJsonFiles(folder))
        {
            ManagedFile managed = CreateBaseFile(file, location);
            try
            {
                AkgfSequenceGestureData data = JsonUtility.FromJson<AkgfSequenceGestureData>(File.ReadAllText(GetAbsolutePath(file)));
                if (data != null)
                {
                    data.EnsureValid();
                    managed.name = data.gestureName;
                    managed.sampleCount = data.samples != null ? data.samples.Count : 0;
                    managed.usable = data.IsUsable;
                    managed.notes = data.notes;
                    managed.modifiedUtc = FirstNonEmpty(data.modifiedUtc, managed.modifiedUtc);
                }
            }
            catch (Exception e)
            {
                managed.error = e.Message;
                managed.usable = false;
            }
            sequenceFiles.Add(managed);
        }
    }

    private void AddCalibrationFiles(string folder, SourceLocation location)
    {
        foreach (string file in EnumerateJsonFiles(folder))
        {
            ManagedFile managed = CreateBaseFile(file, location);
            try
            {
                AkgfCalibrationProfile data = JsonUtility.FromJson<AkgfCalibrationProfile>(File.ReadAllText(GetAbsolutePath(file)));
                if (data != null)
                {
                    data.EnsureValid();
                    managed.name = data.profileName;
                    managed.sampleCount = data.sampleCount;
                    managed.usable = data.IsUsable;
                    managed.notes = data.notes;
                    managed.modifiedUtc = FirstNonEmpty(data.modifiedUtc, managed.modifiedUtc);
                }
            }
            catch (Exception e)
            {
                managed.error = e.Message;
                managed.usable = false;
            }
            calibrationFiles.Add(managed);
        }
    }

    private ManagedFile CreateBaseFile(string path, SourceLocation location)
    {
        string absolute = GetAbsolutePath(path);
        return new ManagedFile
        {
            name = Path.GetFileNameWithoutExtension(path),
            path = path,
            displayPath = path,
            location = location,
            modifiedUtc = File.Exists(absolute) ? File.GetLastWriteTimeUtc(absolute).ToString("u") : string.Empty,
            sampleCount = 0,
            usable = false
        };
    }

    private IEnumerable<string> EnumerateJsonFiles(string folder)
    {
        string absoluteFolder = GetAbsolutePath(folder);
        if (string.IsNullOrWhiteSpace(absoluteFolder) || !Directory.Exists(absoluteFolder))
        {
            yield break;
        }

        string[] files = Directory.GetFiles(absoluteFolder, "*.json", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            yield return ToDisplayPath(files[i]);
        }
    }

    private List<ManagedFile> Filter(List<ManagedFile> source)
    {
        List<ManagedFile> result = new List<ManagedFile>();
        for (int i = 0; i < source.Count; i++)
        {
            ManagedFile file = source[i];
            if (file.location == SourceLocation.ProjectResources && !showProjectResources)
            {
                continue;
            }
            if (file.location == SourceLocation.PersistentData && !showPersistentData)
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                bool match = ContainsIgnoreCase(file.name, s) || ContainsIgnoreCase(file.path, s) || ContainsIgnoreCase(file.notes, s);
                if (!match)
                {
                    continue;
                }
            }
            result.Add(file);
        }
        return result;
    }

    private void LoadStaticPoseFile(ManagedFile file)
    {
        if (staticDatabase == null)
        {
            EditorUtility.DisplayDialog("AKGF", "No AkgfGestureDatabase was found in the scene.", "OK");
            return;
        }

        try
        {
            AkgfGestureData data = JsonUtility.FromJson<AkgfGestureData>(File.ReadAllText(GetAbsolutePath(file.path)));
            if (data == null)
            {
                throw new InvalidOperationException("Could not parse static pose JSON.");
            }
            data.EnsureValid();
            Undo.RecordObject(staticDatabase, "Load Static Pose");
            staticDatabase.LoadAll();
            staticDatabase.AddOrReplace(data);
            EditorUtility.SetDirty(staticDatabase);
            AssetDatabase.Refresh();
            Debug.Log($"[AKGF] Loaded static pose '{data.gestureName}'.", staticDatabase);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("AKGF Load Error", e.Message, "OK");
        }
    }

    private void LoadSequenceGestureFile(ManagedFile file)
    {
        if (sequenceDatabase == null)
        {
            EditorUtility.DisplayDialog("AKGF", "No AkgfSequenceGestureDatabase was found in the scene.", "OK");
            return;
        }

        try
        {
            AkgfSequenceGestureData data = JsonUtility.FromJson<AkgfSequenceGestureData>(File.ReadAllText(GetAbsolutePath(file.path)));
            if (data == null)
            {
                throw new InvalidOperationException("Could not parse sequence gesture JSON.");
            }
            data.EnsureValid();
            Undo.RecordObject(sequenceDatabase, "Load Sequence Gesture");
            sequenceDatabase.LoadAll();
            sequenceDatabase.AddOrReplace(data);
            EditorUtility.SetDirty(sequenceDatabase);
            AssetDatabase.Refresh();
            Debug.Log($"[AKGF] Loaded sequence gesture '{data.gestureName}'.", sequenceDatabase);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("AKGF Load Error", e.Message, "OK");
        }
    }

    private void LoadCalibrationAsActive(ManagedFile file, bool saveAsDefault)
    {
        if (calibrationDatabase == null)
        {
            EditorUtility.DisplayDialog("AKGF", "No AkgfCalibrationDatabase was found in the scene.", "OK");
            return;
        }

        try
        {
            AkgfCalibrationProfile profile = JsonUtility.FromJson<AkgfCalibrationProfile>(File.ReadAllText(GetAbsolutePath(file.path)));
            if (profile == null)
            {
                throw new InvalidOperationException("Could not parse calibration profile JSON.");
            }
            profile.EnsureValid();
            Undo.RecordObject(calibrationDatabase, "Load Active Calibration");
            calibrationDatabase.activeProfile = profile;
            EditorUtility.SetDirty(calibrationDatabase);
            if (saveAsDefault)
            {
                calibrationDatabase.SaveActiveProfile();
                AssetDatabase.Refresh();
            }
            Debug.Log($"[AKGF] Loaded calibration profile '{profile.profileName}' as active.", calibrationDatabase);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("AKGF Calibration Load Error", e.Message, "OK");
        }
    }

    private void SaveActiveCalibration()
    {
        if (calibrationDatabase == null || calibrationDatabase.activeProfile == null)
        {
            EditorUtility.DisplayDialog("AKGF", "No active calibration profile to save.", "OK");
            return;
        }

        try
        {
            string path = calibrationDatabase.SaveActiveProfile();
            AssetDatabase.Refresh();
            RefreshLists();
            Debug.Log($"[AKGF] Saved active calibration to '{path}'.", calibrationDatabase);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("AKGF Calibration Save Error", e.Message, "OK");
        }
    }

    private void AddGestureToSettings(string gestureName, AkgfGestureKind kind)
    {
        if (settingsDatabase == null)
        {
            EditorUtility.DisplayDialog("AKGF", "No AkgfGestureSettingsDatabase was found in the scene.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(gestureName))
        {
            return;
        }

        Undo.RecordObject(settingsDatabase, "Add AKGF Gesture Settings");
        AkgfGestureSettings settings = settingsDatabase.GetSettings(gestureName, kind);
        settings.enabled = true;
        settings.minimumSimilarity = Mathf.Clamp01(settings.minimumSimilarity <= 0f ? 0.55f : settings.minimumSimilarity);
        settings.cooldownSeconds = Mathf.Max(0.5f, settings.cooldownSeconds);
        settings.fireOnEnter = true;
        settings.fireOnStay = false;
        settings.fireOnConfirmed = false;
        settingsDatabase.AddOrReplace(settings);
        EditorUtility.SetDirty(settingsDatabase);
        settingsDatabase.SaveAll();
        AssetDatabase.Refresh();
        Debug.Log($"[AKGF] Added/updated settings for '{gestureName}' ({kind}).", settingsDatabase);
    }

    private void ReloadRuntimeDatabases()
    {
        if (staticDatabase != null)
        {
            Undo.RecordObject(staticDatabase, "Reload Static Gestures");
            staticDatabase.LoadAll();
            EditorUtility.SetDirty(staticDatabase);
        }

        if (sequenceDatabase != null)
        {
            Undo.RecordObject(sequenceDatabase, "Reload Sequence Gestures");
            sequenceDatabase.LoadAll();
            EditorUtility.SetDirty(sequenceDatabase);
        }

        if (calibrationDatabase != null)
        {
            Undo.RecordObject(calibrationDatabase, "Reload Calibration");
            calibrationDatabase.LoadActiveProfile();
            EditorUtility.SetDirty(calibrationDatabase);
        }

        AssetDatabase.Refresh();
        RefreshLists();
        Debug.Log("[AKGF] Reloaded gesture and calibration databases.");
    }

    private void DeleteFile(ManagedFile file)
    {
        string absolute = GetAbsolutePath(file.path);
        if (!File.Exists(absolute))
        {
            RefreshLists();
            return;
        }

        if (confirmDeletes)
        {
            bool ok = EditorUtility.DisplayDialog(
                "Delete AKGF File?",
                $"Delete this file?\n\n{file.name}\n{file.path}\n\nThis cannot be undone from this window.",
                "Delete",
                "Cancel");
            if (!ok)
            {
                return;
            }
        }

        try
        {
            string assetPath = ToAssetPathIfInsideProject(absolute);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            else
            {
                File.Delete(absolute);
            }
            AssetDatabase.Refresh();
            ReloadRuntimeDatabases();
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("AKGF Delete Error", e.Message, "OK");
        }
    }

    private void SelectFile(string path)
    {
        string absolute = GetAbsolutePath(path);
        string assetPath = ToAssetPathIfInsideProject(absolute);
        if (!string.IsNullOrWhiteSpace(assetPath))
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                return;
            }
        }
        RevealPath(path);
    }

    private void OpenFile(string path)
    {
        string absolute = GetAbsolutePath(path);
        if (!File.Exists(absolute))
        {
            EditorUtility.DisplayDialog("AKGF", "File does not exist anymore.", "OK");
            return;
        }

        string assetPath = ToAssetPathIfInsideProject(absolute);
        if (!string.IsNullOrWhiteSpace(assetPath))
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset);
                return;
            }
        }

        Application.OpenURL(new Uri(absolute).AbsoluteUri);
    }

    private void RevealPath(string path)
    {
        string absolute = GetAbsolutePath(path);
        if (File.Exists(absolute) || Directory.Exists(absolute))
        {
            EditorUtility.RevealInFinder(absolute);
            return;
        }

        string folder = Directory.Exists(absolute) ? absolute : Path.GetDirectoryName(absolute);
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            EditorUtility.RevealInFinder(folder);
        }
        else
        {
            EditorUtility.DisplayDialog("AKGF", "Path does not exist yet. Use Create first if this is a folder.", "OK");
        }
    }

    private void CreateFolder(string folder)
    {
        string absolute = GetAbsolutePath(folder);
        if (string.IsNullOrWhiteSpace(absolute))
        {
            return;
        }
        Directory.CreateDirectory(absolute);
        AssetDatabase.Refresh();
        RefreshLists();
        RevealPath(folder);
    }

    private string GetStaticAssetFolder()
    {
        return staticDatabase != null && !string.IsNullOrWhiteSpace(staticDatabase.editorAssetGestureFolder)
            ? staticDatabase.editorAssetGestureFolder
            : "Assets/AzureKinectGestureFramework/Resources/Gestures";
    }

    private string GetStaticPersistentFolder()
    {
        return staticDatabase != null
            ? staticDatabase.GetPersistentGestureFolderPath()
            : Path.Combine(Application.persistentDataPath, "AzureKinectGestureFramework/Gestures");
    }

    private string GetSequenceAssetFolder()
    {
        return sequenceDatabase != null && !string.IsNullOrWhiteSpace(sequenceDatabase.editorAssetGestureFolder)
            ? sequenceDatabase.editorAssetGestureFolder
            : "Assets/AzureKinectGestureFramework/Resources/SequenceGestures";
    }

    private string GetSequencePersistentFolder()
    {
        return sequenceDatabase != null
            ? sequenceDatabase.GetPersistentGestureFolderPath()
            : Path.Combine(Application.persistentDataPath, "AzureKinectGestureFramework/SequenceGestures");
    }

    private string GetCalibrationAssetFolder()
    {
        if (calibrationDatabase != null && !string.IsNullOrWhiteSpace(calibrationDatabase.editorAssetProfileFile))
        {
            string folder = Path.GetDirectoryName(calibrationDatabase.editorAssetProfileFile);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                return folder.Replace('\\', '/');
            }
        }
        return "Assets/AzureKinectGestureFramework/Resources/CalibrationProfiles";
    }

    private string GetCalibrationPersistentFolder()
    {
        string file = calibrationDatabase != null
            ? calibrationDatabase.GetPersistentProfilePath()
            : Path.Combine(Application.persistentDataPath, "AzureKinectGestureFramework/CalibrationProfiles/active_user.json");
        string folder = Path.GetDirectoryName(file);
        return string.IsNullOrWhiteSpace(folder) ? Application.persistentDataPath : folder;
    }

    private string GetAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        path = path.Replace('\\', '/');
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase))
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, path).Replace('\\', '/');
        }

        return Path.GetFullPath(path).Replace('\\', '/');
    }

    private string ToDisplayPath(string absolutePath)
    {
        string assetPath = ToAssetPathIfInsideProject(absolutePath);
        return !string.IsNullOrWhiteSpace(assetPath) ? assetPath : absolutePath.Replace('\\', '/');
    }

    private string ToAssetPathIfInsideProject(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return string.Empty;
        }

        string normalized = Path.GetFullPath(absolutePath).Replace('\\', '/');
        string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        if (normalized.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets" + normalized.Substring(dataPath.Length).Replace('\\', '/');
        }

        return string.Empty;
    }

    private string ShortenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "<empty>";
        }

        string normalized = path.Replace('\\', '/');
        if (normalized.Length <= 95)
        {
            return normalized;
        }
        return "..." + normalized.Substring(normalized.Length - 92);
    }

    private static string FirstNonEmpty(string a, string b)
    {
        return !string.IsNullOrWhiteSpace(a) ? a : b;
    }

    private static bool ContainsIgnoreCase(string value, string searchValue)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SortByName(List<ManagedFile> files)
    {
        files.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
    }

    private static void DrawThinLine()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.25f, 0.25f, 0.25f, 0.35f));
    }
}
