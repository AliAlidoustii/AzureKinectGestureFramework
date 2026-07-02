using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public sealed class AkgfGestureReplay : MonoBehaviour
    {
        [Header("Sources")]
        public AkgfGestureDatabase staticGestureDatabase;
        public AkgfSequenceGestureDatabase sequenceGestureDatabase;
        public string gestureName = "Wave";
        public AkgfGestureKind gestureKind = AkgfGestureKind.Sequence;
        public int sampleIndex = 0;

        [Header("Playback")]
        public bool playOnStart = false;
        public bool loop = true;
        public float playbackSpeed = 1f;

        public AkgfNormalizedPose CurrentPose { get; private set; }
        public bool IsPlaying { get; private set; }
        public float NormalizedTime01 { get; private set; }

        private AkgfPoseSequence activeSequence;
        private float startedAt;

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        private void Update()
        {
            if (IsPlaying)
            {
                TickPlayback();
            }
        }

        public void Play()
        {
            ResolveDatabases();
            activeSequence = BuildSequenceFromSelectedGesture();
            if (activeSequence == null || !activeSequence.IsValid)
            {
                Debug.LogWarning($"Could not replay gesture '{gestureName}'. No valid sample found.", this);
                return;
            }

            startedAt = Time.time;
            IsPlaying = true;
        }

        public void Stop()
        {
            IsPlaying = false;
            NormalizedTime01 = 0f;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        private void TickPlayback()
        {
            if (activeSequence == null || !activeSequence.IsValid)
            {
                Stop();
                return;
            }

            float duration = Mathf.Max(0.001f, activeSequence.durationSeconds);
            float elapsed = (Time.time - startedAt) * Mathf.Max(0.0001f, playbackSpeed);
            if (loop)
            {
                elapsed = elapsed % duration;
            }
            else if (elapsed >= duration)
            {
                elapsed = duration;
                IsPlaying = false;
            }

            NormalizedTime01 = Mathf.Clamp01(elapsed / duration);
            CurrentPose = SampleSequence(activeSequence, NormalizedTime01);
        }

        private void ResolveDatabases()
        {
            if (staticGestureDatabase == null)
            {
                staticGestureDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureDatabase>();
            }

            if (sequenceGestureDatabase == null)
            {
                sequenceGestureDatabase = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureDatabase>();
            }
        }

        private AkgfPoseSequence BuildSequenceFromSelectedGesture()
        {
            if (gestureKind == AkgfGestureKind.Sequence && sequenceGestureDatabase != null)
            {
                AkgfSequenceGestureData sequenceGesture = sequenceGestureDatabase.GetGesture(gestureName);
                if (sequenceGesture != null && sequenceGesture.samples != null && sequenceGesture.samples.Count > 0)
                {
                    int index = Mathf.Clamp(sampleIndex, 0, sequenceGesture.samples.Count - 1);
                    return sequenceGesture.samples[index];
                }
            }

            if (staticGestureDatabase != null)
            {
                AkgfGestureData staticGesture = staticGestureDatabase.GetGesture(gestureName);
                if (staticGesture != null && staticGesture.samples != null && staticGesture.samples.Count > 0)
                {
                    int index = Mathf.Clamp(sampleIndex, 0, staticGesture.samples.Count - 1);
                    AkgfPoseSequence sequence = new AkgfPoseSequence
                    {
                        durationSeconds = 1f,
                        sampleRate = 1f,
                        frames = new List<AkgfNormalizedPose> { staticGesture.samples[index].Clone() }
                    };
                    sequence.EnsureValid();
                    return sequence;
                }
            }

            return null;
        }

        private static AkgfNormalizedPose SampleSequence(AkgfPoseSequence sequence, float t)
        {
            if (sequence == null || !sequence.IsValid || sequence.frames.Count == 0)
            {
                return null;
            }

            if (sequence.frames.Count == 1)
            {
                return sequence.frames[0].Clone();
            }

            float position = Mathf.Clamp01(t) * (sequence.frames.Count - 1);
            int left = Mathf.FloorToInt(position);
            int right = Mathf.Min(left + 1, sequence.frames.Count - 1);
            float blend = position - left;

            AkgfPoseSequence temp = new AkgfPoseSequence
            {
                frames = new List<AkgfNormalizedPose> { sequence.frames[left], sequence.frames[right] },
                durationSeconds = 1f,
                sampleRate = 2f
            };
            List<AkgfNormalizedPose> frames = AkgfSequenceGestureMatcher.ResampleFrames(temp, 2);
            if (frames == null || frames.Count < 2)
            {
                return sequence.frames[left].Clone();
            }

            AkgfNormalizedPose result = new AkgfNormalizedPose();
            result.EnsureArrays();
            for (int i = 0; i < result.values.Length; i++)
            {
                result.values[i] = Mathf.Lerp(frames[0].values[i], frames[1].values[i], blend);
            }

            for (int i = 0; i < result.weights.Length; i++)
            {
                result.weights[i] = Mathf.Lerp(frames[0].weights[i], frames[1].weights[i], blend);
            }

            return result;
        }
    }
}
