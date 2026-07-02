using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public sealed class AkgfGestureRecorder : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Any MonoBehaviour that implements IAkgfSkeletonSource.")]
        public MonoBehaviour skeletonSourceBehaviour;
        public AkgfGestureDatabase gestureDatabase;
        public AkgfCalibrationDatabase calibrationDatabase;

        [Header("Recording")]
        public string gestureName = "CrossArms";
        public float recordDurationSeconds = 1.5f;
        public float samplesPerSecond = 15f;
        public KeyCode recordKey = KeyCode.R;
        public bool recordWithKeyboard = true;
        public bool replaceExistingGesture = true;
        public AkgfPoseNormalizerSettings normalizerSettings = new AkgfPoseNormalizerSettings();

        public bool IsRecording { get; private set; }
        public float RecordingProgress01 { get; private set; }
        public int CurrentSampleCount => samples != null ? samples.Count : 0;
        public string LastSavedPath { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;

        public event Action<AkgfGestureData> GestureSaved;

        private IAkgfSkeletonSource source;
        private readonly AkgfPoseNormalizer normalizer = new AkgfPoseNormalizer();
        private readonly List<AkgfNormalizedPose> samples = new List<AkgfNormalizedPose>();
        private float recordingStartedAt;
        private float nextSampleTime;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (recordWithKeyboard && Input.GetKeyDown(recordKey))
            {
                StartRecording(gestureName);
            }

            if (IsRecording)
            {
                TickRecording();
            }
        }

        public void ResolveReferences()
        {
            source = skeletonSourceBehaviour as IAkgfSkeletonSource;

            if (gestureDatabase == null)
            {
                gestureDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureDatabase>();
            }

            if (calibrationDatabase == null)
            {
                calibrationDatabase = AkgfUnityObjectFinder.FindFirst<AkgfCalibrationDatabase>();
            }
        }

        public void StartRecording(string newGestureName)
        {
            LastError = string.Empty;

            if (string.IsNullOrWhiteSpace(newGestureName))
            {
                LastError = "Gesture name is empty.";
                Debug.LogWarning(LastError, this);
                return;
            }

            if (source == null)
            {
                ResolveReferences();
            }

            if (source == null)
            {
                LastError = "No skeleton source assigned. Assign a component that implements IAkgfSkeletonSource.";
                Debug.LogWarning(LastError, this);
                return;
            }

            gestureName = newGestureName.Trim();
            samples.Clear();
            RecordingProgress01 = 0f;
            recordingStartedAt = Time.time;
            nextSampleTime = Time.time;
            IsRecording = true;
        }

        public void CancelRecording()
        {
            IsRecording = false;
            RecordingProgress01 = 0f;
            samples.Clear();
        }

        private void TickRecording()
        {
            float elapsed = Time.time - recordingStartedAt;
            RecordingProgress01 = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, recordDurationSeconds));

            if (Time.time >= nextSampleTime)
            {
                TryCaptureSample();
                float interval = 1f / Mathf.Max(1f, samplesPerSecond);
                nextSampleTime = Time.time + interval;
            }

            if (elapsed >= recordDurationSeconds)
            {
                FinishRecordingAndSave();
            }
        }

        private bool TryCaptureSample()
        {
            if (source == null || !source.TryGetBody(out AkgfTrackedBody body) || body == null || !body.IsTracked)
            {
                return false;
            }

            if (!normalizer.TryNormalize(body, normalizerSettings, out AkgfNormalizedPose pose))
            {
                return false;
            }

            pose = calibrationDatabase != null ? calibrationDatabase.ApplyToPose(pose, true) : pose;
            samples.Add(pose.Clone());
            return true;
        }

        private void FinishRecordingAndSave()
        {
            IsRecording = false;

            if (samples.Count == 0)
            {
                LastError = "Recording finished, but no valid skeleton samples were captured.";
                Debug.LogWarning(LastError, this);
                return;
            }

            if (gestureDatabase == null)
            {
                ResolveReferences();
            }

            if (gestureDatabase == null)
            {
                LastError = "No AkgfGestureDatabase found. Add one to the scene.";
                Debug.LogWarning(LastError, this);
                return;
            }

            AkgfGestureData gesture = new AkgfGestureData
            {
                gestureName = gestureName.Trim(),
                notes = $"Recorded in Unity. Samples: {samples.Count}",
                samples = new List<AkgfNormalizedPose>(samples)
            };

            try
            {
                LastSavedPath = gestureDatabase.SaveGesture(gesture, true);
                GestureSaved?.Invoke(gesture);
                Debug.Log($"Saved gesture '{gesture.gestureName}' with {gesture.samples.Count} samples to: {LastSavedPath}", this);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogError($"Could not save gesture '{gestureName}': {e.Message}", this);
            }
            finally
            {
                samples.Clear();
                RecordingProgress01 = 0f;
            }
        }
    }
}
