#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AkgfConfigurationSnapshotWindow : EditorWindow
{
    private bool includeSceneComponents = true;
    private bool includeAkgfAssets = true;
    private bool includeGameObjectActiveState = true;
    private bool createMissingComponents = false;

    [MenuItem("Tools/Azure Kinect Gesture Framework/Configuration Snapshot")]
    public static void Open()
    {
        GetWindow<AkgfConfigurationSnapshotWindow>("AKGF Config Snapshot");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AKGF Configuration Snapshot", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "This tool saves AKGF Inspector/component settings into one JSON file. " +
            "Use it to backup or transfer settings after creating the AKGF system in another Unity project.",
            MessageType.Info
        );

        includeSceneComponents = EditorGUILayout.ToggleLeft("Save scene AKGF components", includeSceneComponents);
        includeAkgfAssets = EditorGUILayout.ToggleLeft("Save AKGF ScriptableObject/assets", includeAkgfAssets);
        includeGameObjectActiveState = EditorGUILayout.ToggleLeft("Save GameObject active state", includeGameObjectActiveState);
        createMissingComponents = EditorGUILayout.ToggleLeft("Create missing components on load", createMissingComponents);

        EditorGUILayout.Space();

        if (GUILayout.Button("Save AKGF Configuration To JSON", GUILayout.Height(35)))
        {
            SaveSnapshot();
        }

        if (GUILayout.Button("Load AKGF Configuration From JSON", GUILayout.Height(35)))
        {
            LoadSnapshot();
        }

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Recommended workflow:\n" +
            "1. Create the AKGF Gesture System in the new scene.\n" +
            "2. Assign the Kinect source object if needed.\n" +
            "3. Load this JSON snapshot.\n" +
            "4. Check references such as Source Object and AkgfGestureSystemApi.\n\n" +
            "This saves settings, not recorded gesture JSON files. For poses/sequences/calibrations, copy the Resources folders or use the Pose & Calibration Manager.",
            MessageType.Warning
        );
    }

    private void SaveSnapshot()
    {
        string path = EditorUtility.SaveFilePanel(
            "Save AKGF Configuration",
            Application.dataPath,
            "AKGF_Config_Snapshot.json",
            "json"
        );

        if (string.IsNullOrEmpty(path))
            return;

        var snapshot = new AkgfConfigurationSnapshot
        {
            createdUtc = DateTime.UtcNow.ToString("o"),
            unityVersion = Application.unityVersion,
            scenePath = SceneManager.GetActiveScene().path,
            sceneName = SceneManager.GetActiveScene().name
        };

        if (includeSceneComponents)
            CaptureScene(snapshot);

        if (includeAkgfAssets)
            CaptureAssets(snapshot);

        string json = JsonUtility.ToJson(snapshot, true);
        File.WriteAllText(path, json);

        AssetDatabase.Refresh();

        Debug.Log($"[AKGF CONFIG] Saved configuration snapshot to: {path}");
    }

    private void LoadSnapshot()
    {
        string path = EditorUtility.OpenFilePanel(
            "Load AKGF Configuration",
            Application.dataPath,
            "json"
        );

        if (string.IsNullOrEmpty(path))
            return;

        string json = File.ReadAllText(path);
        var snapshot = JsonUtility.FromJson<AkgfConfigurationSnapshot>(json);

        if (snapshot == null)
        {
            Debug.LogError("[AKGF CONFIG] Could not read snapshot JSON.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        int restoredComponents = 0;
        int missingComponents = 0;
        int restoredAssets = 0;

        if (includeSceneComponents)
        {
            foreach (var goSnapshot in snapshot.gameObjects)
            {
                GameObject go = FindGameObjectByPath(goSnapshot.path);

                if (go == null)
                    continue;

                if (includeGameObjectActiveState)
                {
                    Undo.RecordObject(go, "Restore AKGF GameObject Active State");
                    go.SetActive(goSnapshot.activeSelf);
                    EditorUtility.SetDirty(go);
                }
            }

            foreach (var componentSnapshot in snapshot.components)
            {
                GameObject go = FindGameObjectByPath(componentSnapshot.gameObjectPath);

                if (go == null)
                {
                    missingComponents++;
                    Debug.LogWarning($"[AKGF CONFIG] Missing GameObject: {componentSnapshot.gameObjectPath}");
                    continue;
                }

                Type type = ResolveType(componentSnapshot.componentType);

                if (type == null)
                {
                    missingComponents++;
                    Debug.LogWarning($"[AKGF CONFIG] Missing component type: {componentSnapshot.componentType}");
                    continue;
                }

                Component component = GetComponentByTypeAndIndex(go, type, componentSnapshot.componentIndex);

                if (component == null && createMissingComponents && typeof(Component).IsAssignableFrom(type))
                {
                    component = Undo.AddComponent(go, type);
                }

                if (component == null)
                {
                    missingComponents++;
                    Debug.LogWarning(
                        $"[AKGF CONFIG] Missing component on {componentSnapshot.gameObjectPath}: {type.Name}"
                    );
                    continue;
                }

                Undo.RecordObject(component, "Restore AKGF Component Configuration");
                EditorJsonUtility.FromJsonOverwrite(componentSnapshot.json, component);
                EditorUtility.SetDirty(component);

                restoredComponents++;
            }
        }

        if (includeAkgfAssets)
        {
            foreach (var assetSnapshot in snapshot.assets)
            {
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetSnapshot.assetPath);

                if (asset == null)
                {
                    Debug.LogWarning($"[AKGF CONFIG] Missing asset: {assetSnapshot.assetPath}");
                    continue;
                }

                Undo.RecordObject(asset, "Restore AKGF Asset Configuration");
                EditorJsonUtility.FromJsonOverwrite(assetSnapshot.json, asset);
                EditorUtility.SetDirty(asset);

                restoredAssets++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[AKGF CONFIG] Loaded snapshot.\n" +
            $"Restored components: {restoredComponents}\n" +
            $"Restored assets: {restoredAssets}\n" +
            $"Missing components/objects: {missingComponents}"
        );
    }

    private static void CaptureScene(AkgfConfigurationSnapshot snapshot)
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        foreach (var root in roots)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                Component[] components = go.GetComponents<Component>();

                bool hasAkgfComponent = components.Any(c => c != null && IsAkgfType(c.GetType()));

                if (!hasAkgfComponent)
                    continue;

                snapshot.gameObjects.Add(new AkgfGameObjectSnapshot
                {
                    path = GetHierarchyPath(go),
                    activeSelf = go.activeSelf
                });

                foreach (Component component in components)
                {
                    if (component == null)
                        continue;

                    Type type = component.GetType();

                    if (!IsAkgfType(type))
                        continue;

                    string componentJson = EditorJsonUtility.ToJson(component, true);

                    snapshot.components.Add(new AkgfComponentSnapshot
                    {
                        gameObjectPath = GetHierarchyPath(go),
                        componentType = type.AssemblyQualifiedName,
                        componentTypeName = type.FullName,
                        componentIndex = GetComponentIndex(component),
                        json = componentJson
                    });
                }
            }
        }
    }

    private static void CaptureAssets(AkgfConfigurationSnapshot snapshot)
    {
        string[] searchFolders =
        {
            "Assets/AzureKinectGestureFramework"
        };

        foreach (string guid in AssetDatabase.FindAssets("t:Object", searchFolders))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

            if (asset == null)
                continue;

            Type type = asset.GetType();

            if (!IsAkgfType(type))
                continue;

            if (asset is MonoScript)
                continue;

            snapshot.assets.Add(new AkgfAssetSnapshot
            {
                assetPath = assetPath,
                objectName = asset.name,
                objectType = type.AssemblyQualifiedName,
                json = EditorJsonUtility.ToJson(asset, true)
            });
        }
    }

    private static bool IsAkgfType(Type type)
    {
        if (type == null)
            return false;

        string fullName = type.FullName ?? "";
        string name = type.Name ?? "";

        return fullName.Contains("AzureKinectGestureFramework") ||
               name.StartsWith("Akgf", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("AKGF");
    }

    private static string GetHierarchyPath(GameObject go)
    {
        var parts = new List<string>();
        Transform current = go.transform;

        while (current != null)
        {
            parts.Add($"{current.name}[{current.GetSiblingIndex()}]");
            current = current.parent;
        }

        parts.Reverse();
        return "/" + string.Join("/", parts);
    }

    private static GameObject FindGameObjectByPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        string[] rawParts = path.Trim('/').Split('/');

        if (rawParts.Length == 0)
            return null;

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        Transform current = null;

        for (int i = 0; i < rawParts.Length; i++)
        {
            ParsePathPart(rawParts[i], out string expectedName, out int expectedSiblingIndex);

            if (i == 0)
            {
                current = roots
                    .Select(r => r.transform)
                    .FirstOrDefault(t => t.name == expectedName && t.GetSiblingIndex() == expectedSiblingIndex);

                if (current == null)
                {
                    current = roots
                        .Select(r => r.transform)
                        .FirstOrDefault(t => t.name == expectedName);
                }
            }
            else
            {
                Transform found = null;

                for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                {
                    Transform child = current.GetChild(childIndex);

                    if (child.name == expectedName && child.GetSiblingIndex() == expectedSiblingIndex)
                    {
                        found = child;
                        break;
                    }
                }

                if (found == null)
                {
                    for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                    {
                        Transform child = current.GetChild(childIndex);

                        if (child.name == expectedName)
                        {
                            found = child;
                            break;
                        }
                    }
                }

                current = found;
            }

            if (current == null)
                return null;
        }

        return current.gameObject;
    }

    private static void ParsePathPart(string part, out string name, out int siblingIndex)
    {
        siblingIndex = -1;
        name = part;

        int open = part.LastIndexOf('[');
        int close = part.LastIndexOf(']');

        if (open >= 0 && close > open)
        {
            name = part.Substring(0, open);
            string number = part.Substring(open + 1, close - open - 1);
            int.TryParse(number, out siblingIndex);
        }
    }

    private static int GetComponentIndex(Component component)
    {
        Component[] components = component.gameObject.GetComponents(component.GetType());

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == component)
                return i;
        }

        return 0;
    }

    private static Component GetComponentByTypeAndIndex(GameObject go, Type type, int index)
    {
        Component[] components = go.GetComponents(type);

        if (components == null || components.Length == 0)
            return null;

        if (index >= 0 && index < components.Length)
            return components[index];

        return components[0];
    }

    private static Type ResolveType(string assemblyQualifiedName)
    {
        if (string.IsNullOrEmpty(assemblyQualifiedName))
            return null;

        Type type = Type.GetType(assemblyQualifiedName);

        if (type != null)
            return type;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(assemblyQualifiedName);

            if (type != null)
                return type;
        }

        string typeNameOnly = assemblyQualifiedName.Split(',')[0];

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeNameOnly);

            if (type != null)
                return type;
        }

        return null;
    }

    [Serializable]
    private sealed class AkgfConfigurationSnapshot
    {
        public string createdUtc;
        public string unityVersion;
        public string scenePath;
        public string sceneName;

        public List<AkgfGameObjectSnapshot> gameObjects = new();
        public List<AkgfComponentSnapshot> components = new();
        public List<AkgfAssetSnapshot> assets = new();
    }

    [Serializable]
    private sealed class AkgfGameObjectSnapshot
    {
        public string path;
        public bool activeSelf;
    }

    [Serializable]
    private sealed class AkgfComponentSnapshot
    {
        public string gameObjectPath;
        public string componentType;
        public string componentTypeName;
        public int componentIndex;
        public string json;
    }

    [Serializable]
    private sealed class AkgfAssetSnapshot
    {
        public string assetPath;
        public string objectName;
        public string objectType;
        public string json;
    }
}
#endif
