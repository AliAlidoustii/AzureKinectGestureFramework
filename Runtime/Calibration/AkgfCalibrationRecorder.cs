using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public sealed class AkgfCalibrationRecorder : MonoBehaviour
    {
        [Header("References")]
        public MonoBehaviour skeletonSourceBehaviour;
        public AkgfCalibrationDatabase calibrationDatabase;

        [Header("Recording")]
        public string profileName = "DefaultUser";
        public AkgfPoseNormalizerSettings normalizerSettings = new AkgfPoseNormalizerSettings();
        public KeyCode recordHotkey = KeyCode.C;
        public float recordDurationSeconds = 2.5f;
        public float samplesPerSecond = 15f;
        public bool saveWhenFinished = true;

        public bool IsRecording { get; private set; }
        public float RecordingProgress01 { get; private set; }
        public string LastSavedPath { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;

        private IAkgfSkeletonSource source;
        private readonly AkgfPoseNormalizer normalizer = new AkgfPoseNormalizer();
        private readonly List<AkgfNormalizedPose> frames = new List<AkgfNormalizedPose>();
        private float startedAt;
        private float nextSampleTime;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (!IsRecording && recordHotkey != KeyCode.None && Input.GetKeyDown(recordHotkey))
            {
                StartCalibration();
            }

            if (IsRecording)
            {
                TickRecording();
            }
        }

        public void ResolveReferences()
        {
            source = skeletonSourceBehaviour as IAkgfSkeletonSource;
            if (calibrationDatabase == null)
            {
                calibrationDatabase = AkgfUnityObjectFinder.FindFirst<AkgfCalibrationDatabase>();
            }
        }

        public void StartCalibration()
        {
            LastError = string.Empty;
            if (source == null)
            {
                ResolveReferences();
            }

            if (source == null)
            {
                LastError = "No skeleton source assigned for calibration.";
                Debug.LogWarning(LastError, this);
                return;
            }

            frames.Clear();
            RecordingProgress01 = 0f;
            startedAt = Time.time;
            nextSampleTime = Time.time;
            IsRecording = true;
        }

        public void CancelCalibration()
        {
            IsRecording = false;
            RecordingProgress01 = 0f;
            frames.Clear();
        }

        private void TickRecording()
        {
            float elapsed = Time.time - startedAt;
            RecordingProgress01 = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, recordDurationSeconds));

            if (Time.time >= nextSampleTime)
            {
                TryCaptureFrame();
                nextSampleTime = Time.time + 1f / Mathf.Max(1f, samplesPerSecond);
            }

            if (elapsed >= recordDurationSeconds)
            {
                FinishCalibration();
            }
        }

        private bool TryCaptureFrame()
        {
            if (source == null || !source.TryGetBody(out AkgfTrackedBody body) || body == null || !body.IsTracked)
            {
                return false;
            }

            if (!normalizer.TryNormalize(body, normalizerSettings, out AkgfNormalizedPose pose))
            {
                return false;
            }

            frames.Add(pose.Clone());
            return true;
        }

        private void FinishCalibration()
        {
            IsRecording = false;
            RecordingProgress01 = 0f;

            if (frames.Count < 4)
            {
                LastError = "Calibration finished, but too few valid body frames were captured.";
                Debug.LogWarning(LastError, this);
                frames.Clear();
                return;
            }

            AkgfCalibrationProfile profile = AkgfCalibrationProfile.FromSamples(profileName, frames);
            frames.Clear();
            if (profile == null || !profile.IsUsable)
            {
                LastError = "Could not build a valid calibration profile from the captured frames.";
                Debug.LogWarning(LastError, this);
                return;
            }

            if (calibrationDatabase == null)
            {
                ResolveReferences();
            }

            if (calibrationDatabase == null)
            {
                LastError = "No AkgfCalibrationDatabase found. Add one to the scene.";
                Debug.LogWarning(LastError, this);
                return;
            }

            calibrationDatabase.activeProfile = profile;

            if (saveWhenFinished)
            {
                try
                {
                    LastSavedPath = calibrationDatabase.SaveActiveProfile();
                    Debug.Log($"Saved calibration profile '{profile.profileName}' to: {LastSavedPath}", this);
                }
                catch (Exception e)
                {
                    LastError = e.Message;
                    Debug.LogError($"Could not save calibration profile: {e.Message}", this);
                }
            }
        }
    }
}
