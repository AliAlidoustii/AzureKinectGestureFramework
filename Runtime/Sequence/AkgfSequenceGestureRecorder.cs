using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public sealed class AkgfSequenceGestureRecorder : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Any MonoBehaviour that implements IAkgfSkeletonSource.")]
        public MonoBehaviour skeletonSourceBehaviour;
        public AkgfSequenceGestureDatabase sequenceGestureDatabase;
        public AkgfCalibrationDatabase calibrationDatabase;

        [Header("Recording")]
        public string gestureName = "Wave";
        [Tooltip("Used only for automatic/timed sequence recording. Manual recording ignores this duration and stops only when StopRecordingAndSave is called.")]
        public float recordDurationSeconds = 1.25f;
        public float samplesPerSecond = 15f;
        public KeyCode recordKey = KeyCode.T;
        public bool recordWithKeyboard = true;
        [Tooltip("When enabled, pressing the record key starts recording and pressing it again stops and saves. The UI buttons also use this manual mode.")]
        public bool manualStopMode = true;
        public AkgfPoseNormalizerSettings normalizerSettings = new AkgfPoseNormalizerSettings();

        public bool IsRecording { get; private set; }
        public bool IsManualRecording { get; private set; }
        public float RecordingProgress01 { get; private set; }
        public float RecordingElapsedSeconds { get; private set; }
        public int CurrentFrameCount => frames != null ? frames.Count : 0;
        public string LastSavedPath { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;

        public event Action<AkgfSequenceGestureData> GestureSaved;

        private IAkgfSkeletonSource source;
        private readonly AkgfPoseNormalizer normalizer = new AkgfPoseNormalizer();
        private readonly List<AkgfNormalizedPose> frames = new List<AkgfNormalizedPose>();
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
                if (manualStopMode)
                {
                    if (IsRecording)
                    {
                        StopRecordingAndSave();
                    }
                    else
                    {
                        StartManualRecording(gestureName);
                    }
                }
                else
                {
                    StartRecording(gestureName);
                }
            }

            if (IsRecording)
            {
                TickRecording();
            }
        }

        public void ResolveReferences()
        {
            source = skeletonSourceBehaviour as IAkgfSkeletonSource;

            if (sequenceGestureDatabase == null)
            {
                sequenceGestureDatabase = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureDatabase>();
            }

            if (calibrationDatabase == null)
            {
                calibrationDatabase = AkgfUnityObjectFinder.FindFirst<AkgfCalibrationDatabase>();
            }
        }

        /// <summary>
        /// Starts the old fixed-duration sequence recording. The recording automatically saves after recordDurationSeconds.
        /// </summary>
        public void StartRecording(string newGestureName)
        {
            StartRecordingInternal(newGestureName, false);
        }

        /// <summary>
        /// Starts manual sequence recording. Call StopRecordingAndSave() to finish and save it.
        /// </summary>
        public void StartManualRecording(string newGestureName)
        {
            StartRecordingInternal(newGestureName, true);
        }

        private void StartRecordingInternal(string newGestureName, bool manual)
        {
            LastError = string.Empty;

            if (IsRecording)
            {
                LastError = "Sequence recorder is already recording. Stop or cancel the current recording first.";
                Debug.LogWarning(LastError, this);
                return;
            }

            if (string.IsNullOrWhiteSpace(newGestureName))
            {
                LastError = "Sequence gesture name is empty.";
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
            frames.Clear();
            RecordingProgress01 = 0f;
            RecordingElapsedSeconds = 0f;
            recordingStartedAt = Time.time;
            nextSampleTime = Time.time;
            IsManualRecording = manual;
            IsRecording = true;
        }

        public void StopRecordingAndSave()
        {
            if (!IsRecording)
            {
                LastError = "Sequence recorder is not recording.";
                Debug.LogWarning(LastError, this);
                return;
            }

            // Try to capture the current frame at the exact stop moment, then save.
            TryCaptureFrame();
            FinishRecordingAndSave();
        }

        public void CancelRecording()
        {
            IsRecording = false;
            IsManualRecording = false;
            RecordingProgress01 = 0f;
            RecordingElapsedSeconds = 0f;
            frames.Clear();
        }

        private void TickRecording()
        {
            float elapsed = Time.time - recordingStartedAt;
            RecordingElapsedSeconds = elapsed;

            if (IsManualRecording)
            {
                // Manual recordings have no fixed end, but this value still gives a useful UI progress reference.
                RecordingProgress01 = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, recordDurationSeconds));
            }
            else
            {
                RecordingProgress01 = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, recordDurationSeconds));
            }

            if (Time.time >= nextSampleTime)
            {
                TryCaptureFrame();
                float interval = 1f / Mathf.Max(1f, samplesPerSecond);
                nextSampleTime = Time.time + interval;
            }

            if (!IsManualRecording && elapsed >= recordDurationSeconds)
            {
                FinishRecordingAndSave();
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

            pose = calibrationDatabase != null ? calibrationDatabase.ApplyToPose(pose, true) : pose;
            frames.Add(pose.Clone());
            return true;
        }

        private void FinishRecordingAndSave()
        {
            float durationSeconds = Mathf.Max(0.001f, Time.time - recordingStartedAt);
            if (!IsManualRecording)
            {
                durationSeconds = Mathf.Max(0.001f, recordDurationSeconds);
            }

            IsRecording = false;
            IsManualRecording = false;

            if (frames.Count < 2)
            {
                LastError = "Recording finished, but fewer than 2 valid skeleton frames were captured.";
                Debug.LogWarning(LastError, this);
                frames.Clear();
                RecordingProgress01 = 0f;
                RecordingElapsedSeconds = 0f;
                return;
            }

            if (sequenceGestureDatabase == null)
            {
                ResolveReferences();
            }

            if (sequenceGestureDatabase == null)
            {
                LastError = "No AkgfSequenceGestureDatabase found. Add one to the scene.";
                Debug.LogWarning(LastError, this);
                frames.Clear();
                RecordingProgress01 = 0f;
                RecordingElapsedSeconds = 0f;
                return;
            }

            AkgfPoseSequence sequence = new AkgfPoseSequence
            {
                durationSeconds = durationSeconds,
                sampleRate = samplesPerSecond,
                frames = new List<AkgfNormalizedPose>(frames)
            };
            sequence.EnsureValid();

            AkgfSequenceGestureData gesture = new AkgfSequenceGestureData
            {
                gestureName = gestureName.Trim(),
                notes = $"Recorded in Unity as sequence. Frames: {sequence.frameCount}, Duration: {durationSeconds:0.000}s, ManualStop: {durationSeconds != recordDurationSeconds}",
                samples = new List<AkgfPoseSequence> { sequence }
            };

            try
            {
                LastSavedPath = sequenceGestureDatabase.SaveGesture(gesture, true);
                GestureSaved?.Invoke(gesture);
                Debug.Log($"Saved sequence gesture '{gesture.gestureName}' with {sequence.frameCount} frames to: {LastSavedPath}", this);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogError($"Could not save sequence gesture '{gestureName}': {e.Message}", this);
            }
            finally
            {
                frames.Clear();
                RecordingProgress01 = 0f;
                RecordingElapsedSeconds = 0f;
            }
        }
    }
}
