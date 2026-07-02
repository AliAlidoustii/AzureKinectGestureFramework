using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AzureKinectGestureFramework
{
    public sealed class AkgfCalibrationDatabase : MonoBehaviour
    {
        [Header("Active Profile")]
        public AkgfCalibrationProfile activeProfile;
        [Range(0f, 1f)] public float calibrationStrength = 0.75f;
        public bool applyCalibrationToRecognition = true;
        public bool applyCalibrationToRecordings = true;

        [Header("Loading")]
        public bool loadFromResources = true;
        public string resourcesProfilePath = "CalibrationProfiles/active_user";
        public bool loadFromPersistentData = true;
        public string activeProfileFile = "AzureKinectGestureFramework/CalibrationProfiles/active_user.json";
        public bool loadOnAwake = true;

        [Header("Editor Save Path")]
        public string editorAssetProfileFile = "Assets/AzureKinectGestureFramework/Resources/CalibrationProfiles/active_user.json";

        public bool HasUsableProfile => activeProfile != null && activeProfile.IsUsable;

        private void Awake()
        {
            if (loadOnAwake)
            {
                LoadActiveProfile();
            }
        }

        public AkgfNormalizedPose ApplyToPose(AkgfNormalizedPose pose, bool forRecording)
        {
            if (pose == null)
            {
                return null;
            }

            if (!HasUsableProfile)
            {
                return pose;
            }

            bool shouldApply = forRecording ? applyCalibrationToRecordings : applyCalibrationToRecognition;
            return shouldApply ? AkgfCalibrationProcessor.Apply(pose, activeProfile, calibrationStrength) : pose;
        }

        public string SaveActiveProfile()
        {
            if (activeProfile == null)
            {
                throw new InvalidOperationException("No active calibration profile to save.");
            }

            activeProfile.EnsureValid();
            if (string.IsNullOrWhiteSpace(activeProfile.createdUtc))
            {
                activeProfile.createdUtc = DateTime.UtcNow.ToString("o");
            }

            activeProfile.modifiedUtc = DateTime.UtcNow.ToString("o");
            string json = JsonUtility.ToJson(activeProfile, true);
            string path = GetWritableProfilePath();
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

        public bool LoadActiveProfile()
        {
            if (loadFromResources && TryLoadFromResources())
            {
                return true;
            }

            if (!loadFromPersistentData)
            {
                return false;
            }

            string path = GetPersistentProfilePath();
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                AkgfCalibrationProfile profile = JsonUtility.FromJson<AkgfCalibrationProfile>(File.ReadAllText(path));
                if (profile == null)
                {
                    return false;
                }

                profile.EnsureValid();
                if (!profile.IsUsable)
                {
                    return false;
                }

                activeProfile = profile;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not load calibration profile '{path}': {e.Message}", this);
                return false;
            }
        }

        private bool TryLoadFromResources()
        {
            if (string.IsNullOrWhiteSpace(resourcesProfilePath))
            {
                return false;
            }

            TextAsset asset = Resources.Load<TextAsset>(resourcesProfilePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return false;
            }

            try
            {
                AkgfCalibrationProfile profile = JsonUtility.FromJson<AkgfCalibrationProfile>(asset.text);
                if (profile == null)
                {
                    return false;
                }

                profile.EnsureValid();
                if (!profile.IsUsable)
                {
                    return false;
                }

                activeProfile = profile;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not load calibration profile resource '{resourcesProfilePath}': {e.Message}", this);
                return false;
            }
        }

        public void ClearActiveProfile()
        {
            activeProfile = null;
        }

        public string GetPersistentProfilePath()
        {
            return Path.Combine(Application.persistentDataPath, activeProfileFile);
        }

        public string GetWritableProfilePath()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(editorAssetProfileFile))
            {
                return editorAssetProfileFile;
            }
#endif
            return GetPersistentProfilePath();
        }
    }
}
