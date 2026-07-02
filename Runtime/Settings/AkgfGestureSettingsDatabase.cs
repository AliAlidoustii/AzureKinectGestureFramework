using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfGestureSettingsCollection
    {
        public List<AkgfGestureSettings> settings = new List<AkgfGestureSettings>();
    }

    public sealed class AkgfGestureSettingsDatabase : MonoBehaviour
    {
        [Header("Loading")]
        public bool loadFromResources = true;
        public string resourcesFolder = "GestureSettings";
        public bool loadFromPersistentData = true;
        public string persistentFile = "AzureKinectGestureFramework/GestureSettings/settings.json";
        public bool loadOnAwake = true;
        public bool caseSensitiveGestureNames = false;

        [Header("Defaults")]
        [Range(0f, 1f)] public float defaultStaticMinimumSimilarity = 0.60f;
        [Range(0f, 1f)] public float defaultSequenceMinimumSimilarity = 0.55f;
        public float defaultStaticStableSeconds = 0.20f;
        public float defaultStaticCooldownSeconds = 0.75f;
        public float defaultSequenceCooldownSeconds = 1.0f;
        [Range(0f, 1f)] public float defaultMinimumTrackingQuality = 0.25f;
        public string defaultGroupName = "Default";

        [Header("Editor Save Path")]
        public string editorAssetSettingsFile = "Assets/AzureKinectGestureFramework/Resources/GestureSettings/settings.json";

        [SerializeField] private List<AkgfGestureSettings> settings = new List<AkgfGestureSettings>();

        public IReadOnlyList<AkgfGestureSettings> Settings => settings;

        private void Awake()
        {
            if (loadOnAwake)
            {
                LoadAll();
            }
        }

        public void LoadAll()
        {
            if (settings == null)
            {
                settings = new List<AkgfGestureSettings>();
            }

            if (loadFromResources)
            {
                LoadFromResources();
            }

            if (loadFromPersistentData)
            {
                LoadFromFile(GetPersistentSettingsFilePath());
            }

            RemoveDuplicatesKeepingLast();
        }

        public bool TryGetExplicitSettings(string gestureName, AkgfGestureKind kind, out AkgfGestureSettings result)
        {
            result = null;
            if (settings == null)
            {
                return false;
            }

            for (int i = 0; i < settings.Count; i++)
            {
                AkgfGestureSettings item = settings[i];
                if (item == null)
                {
                    continue;
                }

                item.EnsureValid();
                if (item.Matches(gestureName, kind, caseSensitiveGestureNames))
                {
                    result = item;
                    return true;
                }
            }

            return false;
        }

        public AkgfGestureSettings GetSettings(string gestureName, AkgfGestureKind kind)
        {
            if (settings != null)
            {
                for (int i = 0; i < settings.Count; i++)
                {
                    AkgfGestureSettings item = settings[i];
                    if (item == null)
                    {
                        continue;
                    }

                    item.EnsureValid();
                    if (item.Matches(gestureName, kind, caseSensitiveGestureNames))
                    {
                        return item;
                    }
                }
            }

            return CreateRuntimeDefault(gestureName, kind);
        }

        public bool IsAllowed(string gestureName, AkgfGestureKind kind, AkgfGestureGroupController groupController)
        {
            AkgfGestureSettings item = GetSettings(gestureName, kind);
            if (item == null || !item.enabled)
            {
                return false;
            }

            return groupController == null || groupController.IsGroupActive(item.groupName);
        }

        public void AddOrReplace(AkgfGestureSettings item)
        {
            if (item == null)
            {
                return;
            }

            item.EnsureValid();
            if (settings == null)
            {
                settings = new List<AkgfGestureSettings>();
            }

            for (int i = 0; i < settings.Count; i++)
            {
                AkgfGestureSettings existing = settings[i];
                if (existing != null && existing.Matches(item.gestureName, item.gestureKind, caseSensitiveGestureNames))
                {
                    settings[i] = item;
                    return;
                }
            }

            settings.Add(item);
        }

        public string SaveAll()
        {
            if (settings == null)
            {
                settings = new List<AkgfGestureSettings>();
            }

            for (int i = 0; i < settings.Count; i++)
            {
                settings[i]?.EnsureValid();
            }

            AkgfGestureSettingsCollection collection = new AkgfGestureSettingsCollection { settings = settings };
            string json = JsonUtility.ToJson(collection, true);
            string path = GetWritableSettingsFilePath();
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(path, json);

#if UNITY_EDITOR
            if (path.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase) || path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.Refresh();
            }
#endif

            return path;
        }

        public AkgfGestureSettings CreateRuntimeDefault(string gestureName, AkgfGestureKind kind)
        {
            AkgfGestureSettings item = new AkgfGestureSettings
            {
                gestureName = string.IsNullOrWhiteSpace(gestureName) ? "Gesture" : gestureName,
                gestureKind = kind,
                enabled = true,
                groupName = string.IsNullOrWhiteSpace(defaultGroupName) ? "Default" : defaultGroupName,
                minimumSimilarity = kind == AkgfGestureKind.Sequence ? defaultSequenceMinimumSimilarity : defaultStaticMinimumSimilarity,
                requiredStableSeconds = defaultStaticStableSeconds,
                cooldownSeconds = kind == AkgfGestureKind.Sequence ? defaultSequenceCooldownSeconds : defaultStaticCooldownSeconds,
                minimumTrackingQuality = defaultMinimumTrackingQuality,
                priority = kind == AkgfGestureKind.Sequence ? 10 : 0,
                mirrorMode = AkgfMirrorMode.Strict
            };
            item.EnsureValid();
            return item;
        }

        public string GetPersistentSettingsFilePath()
        {
            return Path.Combine(Application.persistentDataPath, persistentFile);
        }

        public string GetWritableSettingsFilePath()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(editorAssetSettingsFile))
            {
                return editorAssetSettingsFile;
            }
#endif
            return GetPersistentSettingsFilePath();
        }

        private void LoadFromResources()
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(resourcesFolder);
            for (int i = 0; i < assets.Length; i++)
            {
                TryLoadCollection(assets[i].text, assets[i].name);
            }
        }

        private void LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            TryLoadCollection(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
        }

        private void TryLoadCollection(string json, string sourceName)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                AkgfGestureSettingsCollection collection = JsonUtility.FromJson<AkgfGestureSettingsCollection>(json);
                if (collection == null || collection.settings == null)
                {
                    return;
                }

                for (int i = 0; i < collection.settings.Count; i++)
                {
                    AddOrReplace(collection.settings[i]);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not parse gesture settings '{sourceName}': {e.Message}", this);
            }
        }

        private void RemoveDuplicatesKeepingLast()
        {
            if (settings == null || settings.Count <= 1)
            {
                return;
            }

            List<AkgfGestureSettings> unique = new List<AkgfGestureSettings>();
            for (int i = 0; i < settings.Count; i++)
            {
                AkgfGestureSettings item = settings[i];
                if (item == null)
                {
                    continue;
                }

                int existingIndex = -1;
                for (int j = 0; j < unique.Count; j++)
                {
                    if (unique[j] != null && unique[j].Matches(item.gestureName, item.gestureKind, caseSensitiveGestureNames))
                    {
                        existingIndex = j;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    unique[existingIndex] = item;
                }
                else
                {
                    unique.Add(item);
                }
            }

            settings = unique;
        }
    }
}
