using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AzureKinectGestureFramework
{
    public sealed class AkgfSequenceGestureDatabase : MonoBehaviour
    {
        [Header("Loading")]
        public bool loadFromResources = true;
        public string resourcesFolder = "SequenceGestures";
        public bool loadFromPersistentData = true;
        public string persistentFolder = "AzureKinectGestureFramework/SequenceGestures";
        public bool loadOnAwake = true;

        [Header("Editor Recording Save Path")]
        public string editorAssetGestureFolder = "Assets/AzureKinectGestureFramework/Resources/SequenceGestures";

        [SerializeField] private List<AkgfSequenceGestureData> gestures = new List<AkgfSequenceGestureData>();

        public IReadOnlyList<AkgfSequenceGestureData> Gestures => gestures;
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
            gestures = new List<AkgfSequenceGestureData>();

            if (loadFromResources)
            {
                LoadFromResources();
            }

            if (loadFromPersistentData)
            {
                LoadFromFolder(GetPersistentGestureFolderPath());
            }

            RemoveDuplicateNamesKeepingLast();
        }

        public AkgfSequenceGestureData GetGesture(string gestureName)
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

        public void AddOrReplace(AkgfSequenceGestureData gesture)
        {
            if (gesture == null)
            {
                return;
            }

            gesture.EnsureValid();
            if (gestures == null)
            {
                gestures = new List<AkgfSequenceGestureData>();
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

        public string SaveGesture(AkgfSequenceGestureData gesture, bool alsoAddToLoadedDatabase = true)
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
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return;
            }

            string[] files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    TryLoadJson(File.ReadAllText(files[i]), Path.GetFileNameWithoutExtension(files[i]));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not read sequence gesture file '{files[i]}': {e.Message}", this);
                }
            }
        }

        private void TryLoadJson(string json, string sourceName)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                AkgfSequenceGestureData gesture = JsonUtility.FromJson<AkgfSequenceGestureData>(json);
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
                Debug.LogWarning($"Could not parse sequence gesture '{sourceName}': {e.Message}", this);
            }
        }

        private void RemoveDuplicateNamesKeepingLast()
        {
            if (gestures == null || gestures.Count <= 1)
            {
                return;
            }

            Dictionary<string, AkgfSequenceGestureData> map = new Dictionary<string, AkgfSequenceGestureData>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < gestures.Count; i++)
            {
                AkgfSequenceGestureData g = gestures[i];
                if (g == null || string.IsNullOrWhiteSpace(g.gestureName))
                {
                    continue;
                }

                map[g.gestureName] = g;
            }

            gestures = new List<AkgfSequenceGestureData>(map.Values);
        }
    }
}
