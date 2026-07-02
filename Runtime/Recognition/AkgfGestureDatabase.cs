using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AzureKinectGestureFramework
{
    public sealed class AkgfGestureDatabase : MonoBehaviour
    {
        [Header("Loading")]
        public bool loadFromResources = true;
        public string resourcesFolder = "Gestures";
        public bool loadFromPersistentData = true;
        public string persistentFolder = "AzureKinectGestureFramework/Gestures";
        public bool loadOnAwake = true;

        [Header("Editor Recording Save Path")]
        public string editorAssetGestureFolder = "Assets/AzureKinectGestureFramework/Resources/Gestures";

        [SerializeField] private List<AkgfGestureData> gestures = new List<AkgfGestureData>();

        public string LastLoadSummary { get; private set; } = "not loaded yet";

        public IReadOnlyList<AkgfGestureData> Gestures => gestures;
        public int Count => gestures != null ? gestures.Count : 0;

        private void Awake()
        {
            if (loadOnAwake)
            {
                LoadAll();
            }
        }

        public void LoadAll()
        {
            gestures = new List<AkgfGestureData>();
            int resourcesBefore = gestures.Count;

            if (loadFromResources)
            {
                LoadFromResources();
            }

            int resourcesCount = gestures.Count - resourcesBefore;
            int persistentBefore = gestures.Count;

            if (loadFromPersistentData)
            {
                LoadFromFolder(GetPersistentGestureFolderPath());
            }

            int persistentCount = gestures.Count - persistentBefore;

            // Important MultiUser fix:
            // In the Unity Editor users often delete/import AKGF packages while their recordings live in
            // a different Assets folder, or Unity has not re-imported Resources yet. SingleUser may still
            // work from an already assigned database while MultiUser reports staticDB=0.
            // This fallback searches real JSON files and only accepts JSON that looks like an AKGF static pose.
            int fallbackBefore = gestures.Count;
#if UNITY_EDITOR
            if (gestures.Count == 0)
            {
                LoadFromFolderRecursive(Application.dataPath);
            }
#endif
            if (gestures.Count == 0)
            {
                LoadFromFolderRecursive(Path.Combine(Application.persistentDataPath, "AzureKinectGestureFramework"));
            }

            int fallbackCount = gestures.Count - fallbackBefore;
            RemoveDuplicateNamesKeepingLast();
            LastLoadSummary = $"static poses loaded={gestures.Count}, resources+={resourcesCount}, persistent+={persistentCount}, fallback+={fallbackCount}";
        }

        public AkgfGestureData GetGesture(string gestureName)
        {
            if (string.IsNullOrWhiteSpace(gestureName) || gestures == null)
            {
                return null;
            }

            for (int i = 0; i < gestures.Count; i++)
            {
                if (gestures[i] != null && string.Equals(gestures[i].gestureName, gestureName, StringComparison.OrdinalIgnoreCase))
                {
                    return gestures[i];
                }
            }

            return null;
        }

        public void AddOrReplace(AkgfGestureData gesture)
        {
            if (gesture == null)
            {
                return;
            }

            gesture.EnsureValid();
            if (gestures == null)
            {
                gestures = new List<AkgfGestureData>();
            }

            for (int i = 0; i < gestures.Count; i++)
            {
                if (gestures[i] != null && string.Equals(gestures[i].gestureName, gesture.gestureName, StringComparison.OrdinalIgnoreCase))
                {
                    gestures[i] = gesture;
                    return;
                }
            }

            gestures.Add(gesture);
        }

        public string SaveGesture(AkgfGestureData gesture, bool alsoAddToLoadedDatabase = true)
        {
            if (gesture == null)
            {
                throw new ArgumentNullException(nameof(gesture));
            }

            gesture.EnsureValid();
            if (string.IsNullOrWhiteSpace(gesture.createdUtc))
            {
                gesture.createdUtc = DateTime.UtcNow.ToString("o");
            }

            gesture.modifiedUtc = DateTime.UtcNow.ToString("o");
            string json = JsonUtility.ToJson(gesture, true);
            string fileName = AkgfMath.SanitizeFileName(gesture.gestureName) + ".json";
            string folder = GetWritableGestureFolderPath();
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, fileName);
            File.WriteAllText(path, json);

#if UNITY_EDITOR
            if (path.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase) || path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.Refresh();
            }
#endif

            if (alsoAddToLoadedDatabase)
            {
                AddOrReplace(gesture);
            }

            return path;
        }

        public string GetPersistentGestureFolderPath()
        {
            return Path.Combine(Application.persistentDataPath, persistentFolder);
        }

        public string GetWritableGestureFolderPath()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(editorAssetGestureFolder))
            {
                return editorAssetGestureFolder;
            }
#endif
            return GetPersistentGestureFolderPath();
        }

        private void LoadFromResources()
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(resourcesFolder);
            for (int i = 0; i < assets.Length; i++)
            {
                TryLoadJson(assets[i].text, assets[i].name);
            }
        }

        private void LoadFromFolder(string folder)
        {
            LoadFromFolderInternal(folder, SearchOption.TopDirectoryOnly);
        }

        private void LoadFromFolderRecursive(string folder)
        {
            LoadFromFolderInternal(folder, SearchOption.AllDirectories);
        }

        private void LoadFromFolderInternal(string folder, SearchOption searchOption)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return;
            }

            string[] files = Directory.GetFiles(folder, "*.json", searchOption);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    TryLoadJson(File.ReadAllText(files[i]), Path.GetFileNameWithoutExtension(files[i]));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not read gesture file '{files[i]}': {e.Message}", this);
                }
            }
        }

        private void TryLoadJson(string json, string sourceName)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            if (!LooksLikeStaticGestureJson(json))
            {
                return;
            }

            try
            {
                AkgfGestureData gesture = JsonUtility.FromJson<AkgfGestureData>(json);
                if (gesture == null)
                {
                    return;
                }

                gesture.EnsureValid();
                if (gesture.IsUsable)
                {
                    AddOrReplace(gesture);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not parse gesture '{sourceName}': {e.Message}", this);
            }
        }

        private static bool LooksLikeStaticGestureJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            // Static pose JSON has gestureName + samples. Sequence JSON has frames/keyframes and should not be loaded here.
            return json.IndexOf("\"gestureName\"", StringComparison.OrdinalIgnoreCase) >= 0
                && json.IndexOf("\"samples\"", StringComparison.OrdinalIgnoreCase) >= 0
                && json.IndexOf("\"frames\"", StringComparison.OrdinalIgnoreCase) < 0
                && json.IndexOf("\"sequence\"", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private void RemoveDuplicateNamesKeepingLast()
        {
            if (gestures == null || gestures.Count <= 1)
            {
                return;
            }

            Dictionary<string, AkgfGestureData> map = new Dictionary<string, AkgfGestureData>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < gestures.Count; i++)
            {
                AkgfGestureData g = gestures[i];
                if (g == null || string.IsNullOrWhiteSpace(g.gestureName))
                {
                    continue;
                }

                map[g.gestureName] = g;
            }

            gestures = new List<AkgfGestureData>(map.Values);
        }
    }
}
