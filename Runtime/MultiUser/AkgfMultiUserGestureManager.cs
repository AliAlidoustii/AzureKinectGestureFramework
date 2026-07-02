using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// True simultaneous multi-user recognizer. One independent recognition state is kept per Azure Kinect body ID.
    /// This prevents sequence buffers, calibration, cooldowns and active gesture state from leaking between people.
    /// </summary>
    public sealed class AkgfMultiUserGestureManager : MonoBehaviour
    {
        private struct TimedPose
        {
            public float time;
            public AkgfNormalizedPose pose;
        }

        private sealed class UserState
        {
            public int bodyId;
            public float firstSeenTime;
            public float lastSeenTime;
            public Vector3 lastBodyPosition;
            public AkgfNormalizedPose lastPose;
            public float lastTrackingQuality;

            public readonly List<AkgfNormalizedPose> calibrationSamples = new List<AkgfNormalizedPose>();
            public AkgfCalibrationProfile calibrationProfile;
            public bool calibrationStarted;
            public bool calibrationFinished;

            public string stableStaticName = string.Empty;
            public float staticStableSinceTime;
            public string lastFiredStaticName = string.Empty;
            public float lastStaticFireTime = -9999f;

            public readonly List<TimedPose> sequenceBuffer = new List<TimedPose>(128);
            public float nextSequenceSampleTime;
            public float nextSequenceRecognitionTime;
            public string consecutiveSequenceName = string.Empty;
            public int consecutiveSequenceCount;
            public string lastFiredSequenceName = string.Empty;
            public float lastSequenceFireTime = -9999f;

            public bool hasPendingStatic;
            public bool hasPendingSequence;
            public AkgfGestureMatchResult pendingStatic;
            public AkgfGestureMatchResult pendingSequence;
            public float staticBlockedUntil = -9999f;
            public float lastAnyFireTime = -9999f;
            public readonly Dictionary<string, float> lastFireByGesture = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            public string activeGestureName = string.Empty;
            public AkgfGestureKind activeGestureKind = AkgfGestureKind.StaticPose;
            public AkgfGestureMatchResult activeMatch;
            public float lastActiveRefreshTime = -9999f;
        }

        [Header("Source")]
        [Tooltip("A MonoBehaviour that implements IAkgfMultiSkeletonSource.")]
        public MonoBehaviour multiSkeletonSourceBehaviour;

        [Header("Databases")]
        public AkgfGestureDatabase gestureDatabase;
        public AkgfSequenceGestureDatabase sequenceGestureDatabase;
        public AkgfGestureSettingsDatabase gestureSettingsDatabase;
        public AkgfGestureGroupController groupController;
        public AkgfUserIdentityTracker identityTracker;
        public bool autoLoadDatabasesOnStart = true;

        [Header("Recognition")]
        public bool enableStaticPoseRecognition = true;
        public bool enableSequenceRecognition = true;
        public AkgfPoseNormalizerSettings normalizerSettings = new AkgfPoseNormalizerSettings();
        public AkgfGestureMatcherSettings staticMatcherSettings = new AkgfGestureMatcherSettings();
        public AkgfSequenceGestureMatcherSettings sequenceMatcherSettings = new AkgfSequenceGestureMatcherSettings();
        public AkgfTrackingQualityFilter trackingQualityFilter = new AkgfTrackingQualityFilter();
        [Range(0f, 1f)] public float defaultStaticMinimumSimilarity = 0.82f;
        [Range(0f, 1f)] public float defaultSequenceMinimumSimilarity = 0.72f;
        public float defaultStaticStableSeconds = 0.20f;
        public float defaultStaticCooldownSeconds = 0.75f;
        public float defaultSequenceCooldownSeconds = 1.0f;

        [Header("Sequence")]
        public float recognitionWindowSeconds = 1.25f;
        public float maxBufferSeconds = 3f;
        public float samplesPerSecond = 15f;
        public float recognitionsPerSecond = 10f;
        public int minimumWindowFrames = 8;
        [Range(1, 10)] public int requiredConsecutiveSequenceMatches = 2;

        [Header("Runtime Calibration")]
        public bool autoCalibrateNewUsers = true;
        [Tooltip("If true, a new user is ignored until their neutral profile is collected.")]
        public bool requireCalibrationBeforeRecognition = false;
        public float calibrationSeconds = 2.0f;
        [Range(0f, 1f)] public float calibrationStrength = 0.75f;
        public bool applyCalibrationToRecognition = true;

        [Header("Conflict Rules Per User")]
        public bool sequenceHasPriority = true;
        public float sequenceBlocksStaticSeconds = 0.60f;
        public float globalCooldownSeconds = 0.08f;
        public float sameGestureCooldownSeconds = 0.75f;
        public bool caseSensitiveGestureNames = false;

        [Header("Body ID Stability")]
        [Tooltip("When enabled, emitted bodyId values are stable user IDs, not raw Azure Kinect body IDs. This helps when the SDK loses and reacquires a person.")]
        public bool useStableUserIds = true;
        public bool autoCreateIdentityTracker = true;

        [Header("User Lifetime")]
        public float lostUserTimeoutSeconds = 2.50f;
        public int MaxActiveUsers => users.Count;

        [Header("Event Phases")]
        public bool emitDetectedPhase = true;
        public bool emitEnterPhase = true;
        public bool emitStayPhase = false;
        public bool emitExitPhase = true;
        public bool emitConfirmedPhase = true;
        public float exitAfterMissingSeconds = 0.45f;

        [Header("Events")]
        public AkgfMultiUserGestureRecognizedEvent onMultiUserGestureRecognized = new AkgfMultiUserGestureRecognizedEvent();
        public AkgfMultiUserGesturePhaseEvent onMultiUserGesturePhase = new AkgfMultiUserGesturePhaseEvent();

        public event Action<AkgfGestureMatchResult> MultiUserGestureRecognized;
        public event Action<AkgfGestureMatchResult> MultiUserGesturePhase;

        public int VisibleBodyCount { get; private set; }
        public float LastProcessingMilliseconds { get; private set; }
        public int ActiveUserCount => users.Count;
        public IReadOnlyList<int> ActiveBodyIds => activeBodyIds;

        private IAkgfMultiSkeletonSource multiSource;
        private readonly AkgfPoseNormalizer normalizer = new AkgfPoseNormalizer();
        private readonly Dictionary<int, UserState> users = new Dictionary<int, UserState>();
        private readonly List<int> activeBodyIds = new List<int>(8);
        private readonly List<int> bodyIdsToRemove = new List<int>(8);
        private readonly List<int> visibleRawBodyIds = new List<int>(8);
        private readonly List<AkgfTrackedBody> bodies = new List<AkgfTrackedBody>(8);

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (autoLoadDatabasesOnStart)
            {
                gestureDatabase?.LoadAll();
                sequenceGestureDatabase?.LoadAll();
                gestureSettingsDatabase?.LoadAll();
            }
        }

        private void Update()
        {
            float profilerStart = Time.realtimeSinceStartup;
            Tick();
            LastProcessingMilliseconds = (Time.realtimeSinceStartup - profilerStart) * 1000f;
        }

        public void ResolveReferences()
        {
            multiSource = multiSkeletonSourceBehaviour as IAkgfMultiSkeletonSource;

            if (gestureDatabase == null)
            {
                gestureDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureDatabase>();
            }

            if (sequenceGestureDatabase == null)
            {
                sequenceGestureDatabase = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureDatabase>();
            }

            if (gestureSettingsDatabase == null)
            {
                gestureSettingsDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureSettingsDatabase>();
            }

            if (groupController == null)
            {
                groupController = AkgfUnityObjectFinder.FindFirst<AkgfGestureGroupController>();
            }

            if (identityTracker == null)
            {
                identityTracker = GetComponent<AkgfUserIdentityTracker>();
                if (identityTracker == null)
                {
                    identityTracker = AkgfUnityObjectFinder.FindFirst<AkgfUserIdentityTracker>();
                }
            }

            if (identityTracker == null && autoCreateIdentityTracker)
            {
                identityTracker = gameObject.AddComponent<AkgfUserIdentityTracker>();
            }
        }

        public void ClearUsers()
        {
            users.Clear();
            activeBodyIds.Clear();
            bodyIdsToRemove.Clear();
        }

        public bool HasUser(int bodyId)
        {
            return users.ContainsKey(bodyId);
        }

        public bool TryGetUserCalibration(int bodyId, out AkgfCalibrationProfile profile)
        {
            profile = null;
            if (!users.TryGetValue(bodyId, out UserState state))
            {
                return false;
            }

            profile = state.calibrationProfile;
            return profile != null && profile.IsUsable;
        }

        public void Tick()
        {
            if (multiSource == null)
            {
                ResolveReferences();
            }

            if (multiSource == null)
            {
                VisibleBodyCount = 0;
                PruneLostUsers();
                return;
            }

            bodies.Clear();
            multiSource.GetTrackedBodies(bodies);
            VisibleBodyCount = bodies.Count;

            visibleRawBodyIds.Clear();
            for (int i = 0; i < bodies.Count; i++)
            {
                AkgfTrackedBody visibleBody = bodies[i];
                if (visibleBody != null && visibleBody.IsTracked)
                {
                    visibleRawBodyIds.Add(visibleBody.BodyId);
                }
            }

            if (useStableUserIds && identityTracker != null)
            {
                identityTracker.MarkFrameRawIdsVisible(visibleRawBodyIds, Time.time);
            }

            for (int i = 0; i < bodies.Count; i++)
            {
                AkgfTrackedBody body = bodies[i];
                if (body == null || !body.IsTracked)
                {
                    continue;
                }

                ProcessBody(body);
            }

            foreach (KeyValuePair<int, UserState> pair in users)
            {
                MaybeEmitExitForLostGesture(pair.Value);
            }

            PruneLostUsers();
            RefreshActiveBodyIdList();
        }

        private void ProcessBody(AkgfTrackedBody body)
        {
            Vector3 pelvisPosition = body.GetJoint(AkgfJointId.Pelvis);
            int userId = ResolveRuntimeBodyId(body.BodyId, pelvisPosition);
            UserState state = GetOrCreateUser(userId);
            state.lastSeenTime = Time.time;
            state.lastBodyPosition = pelvisPosition;

            state.lastTrackingQuality = trackingQualityFilter != null ? trackingQualityFilter.ComputeBodyQuality(body) : 1f;
            if (trackingQualityFilter != null && state.lastTrackingQuality < trackingQualityFilter.minimumOverallBodyQuality)
            {
                ResetStaticStability(state);
                ResetSequenceConsecutive(state);
                return;
            }

            if (!normalizer.TryNormalize(body, normalizerSettings, out AkgfNormalizedPose normalizedPose))
            {
                ResetStaticStability(state);
                ResetSequenceConsecutive(state);
                return;
            }

            UpdateRuntimeCalibration(state, normalizedPose);
            if (requireCalibrationBeforeRecognition && autoCalibrateNewUsers && !state.calibrationFinished)
            {
                return;
            }

            if (applyCalibrationToRecognition && state.calibrationProfile != null && state.calibrationProfile.IsUsable)
            {
                normalizedPose = AkgfCalibrationProcessor.Apply(normalizedPose, state.calibrationProfile, calibrationStrength);
            }

            state.lastPose = normalizedPose;

            if (enableStaticPoseRecognition)
            {
                TryRecognizeStatic(state, body, normalizedPose);
            }

            if (enableSequenceRecognition)
            {
                TryRecognizeSequence(state, body, normalizedPose);
            }

            ResolvePendingCandidates(state);
            ClearPendingCandidates(state);
        }

        private int ResolveRuntimeBodyId(int rawBodyId, Vector3 bodyPosition)
        {
            if (!useStableUserIds || identityTracker == null)
            {
                return rawBodyId;
            }

            return identityTracker.ResolveStableId(rawBodyId, bodyPosition, Time.time);
        }

        private UserState GetOrCreateUser(int bodyId)
        {
            if (users.TryGetValue(bodyId, out UserState state))
            {
                return state;
            }

            state = new UserState
            {
                bodyId = bodyId,
                firstSeenTime = Time.time,
                lastSeenTime = Time.time,
                staticStableSinceTime = Time.time,
                activeMatch = AkgfGestureMatchResult.None
            };
            users.Add(bodyId, state);
            return state;
        }

        private void UpdateRuntimeCalibration(UserState state, AkgfNormalizedPose rawNormalizedPose)
        {
            if (!autoCalibrateNewUsers || state.calibrationFinished || rawNormalizedPose == null || !rawNormalizedPose.IsValid)
            {
                return;
            }

            if (!state.calibrationStarted)
            {
                state.calibrationStarted = true;
                state.calibrationSamples.Clear();
            }

            state.calibrationSamples.Add(rawNormalizedPose.Clone());
            if (Time.time - state.firstSeenTime < Mathf.Max(0.25f, calibrationSeconds))
            {
                return;
            }

            AkgfCalibrationProfile profile = AkgfCalibrationProfile.FromSamples("Body_" + state.bodyId, state.calibrationSamples);
            if (profile != null && profile.IsUsable)
            {
                state.calibrationProfile = profile;
                state.calibrationFinished = true;
            }
        }

        private void TryRecognizeStatic(UserState state, AkgfTrackedBody body, AkgfNormalizedPose pose)
        {
            if (gestureDatabase == null)
            {
                return;
            }

            AkgfGestureMatchResult match = AkgfGestureMatcher.FindBestMatch(pose, gestureDatabase.Gestures, staticMatcherSettings, gestureSettingsDatabase, groupController);
            if (!match.isValid)
            {
                ResetStaticStability(state);
                return;
            }

            AkgfGestureSettings settings = GetSettings(match.gestureName, AkgfGestureKind.StaticPose);
            if (!ValidateMatchAgainstSettings(body, ref match, settings, defaultStaticMinimumSimilarity))
            {
                ResetStaticStability(state);
                return;
            }

            if (!string.Equals(state.stableStaticName, match.gestureName, StringComparisonForNames()))
            {
                state.stableStaticName = match.gestureName;
                state.staticStableSinceTime = Time.time;
                return;
            }

            float requiredStable = settings != null ? settings.requiredStableSeconds : defaultStaticStableSeconds;
            if (Time.time - state.staticStableSinceTime < Mathf.Max(0f, requiredStable))
            {
                return;
            }

            float cooldown = settings != null ? settings.cooldownSeconds : defaultStaticCooldownSeconds;
            if (string.Equals(state.lastFiredStaticName, match.gestureName, StringComparisonForNames()) &&
                Time.time - state.lastStaticFireTime < Mathf.Max(0f, cooldown))
            {
                return;
            }

            match.bodyId = state.bodyId;
            match.bodyPosition = state.lastBodyPosition;
            match.gestureKind = AkgfGestureKind.StaticPose;
            state.lastFiredStaticName = match.gestureName;
            state.lastStaticFireTime = Time.time;

            if (!state.hasPendingStatic || IsHigherPriority(match, state.pendingStatic))
            {
                state.pendingStatic = match;
                state.hasPendingStatic = true;
            }
        }

        private void TryRecognizeSequence(UserState state, AkgfTrackedBody body, AkgfNormalizedPose pose)
        {
            if (sequenceGestureDatabase == null)
            {
                return;
            }

            SampleSequencePoseIfDue(state, pose);
            PruneSequenceBuffer(state);

            if (CountFramesInsideWindow(state) < Mathf.Max(2, minimumWindowFrames))
            {
                ResetSequenceConsecutive(state);
                return;
            }

            if (Time.time < state.nextSequenceRecognitionTime)
            {
                return;
            }

            state.nextSequenceRecognitionTime = Time.time + 1f / Mathf.Max(1f, recognitionsPerSecond);

            AkgfPoseSequence window = BuildCurrentSequenceWindow(state);
            if (window == null || !window.IsValid)
            {
                ResetSequenceConsecutive(state);
                return;
            }

            AkgfGestureMatchResult match = AkgfSequenceGestureMatcher.FindBestMatch(window, sequenceGestureDatabase.Gestures, sequenceMatcherSettings, gestureSettingsDatabase, groupController);
            if (!match.isValid)
            {
                ResetSequenceConsecutive(state);
                return;
            }

            AkgfGestureSettings settings = GetSettings(match.gestureName, AkgfGestureKind.Sequence);
            if (!ValidateMatchAgainstSettings(body, ref match, settings, defaultSequenceMinimumSimilarity))
            {
                ResetSequenceConsecutive(state);
                return;
            }

            if (!string.Equals(state.consecutiveSequenceName, match.gestureName, StringComparisonForNames()))
            {
                state.consecutiveSequenceName = match.gestureName;
                state.consecutiveSequenceCount = 1;
                return;
            }

            state.consecutiveSequenceCount++;
            if (state.consecutiveSequenceCount < Mathf.Max(1, requiredConsecutiveSequenceMatches))
            {
                return;
            }

            float cooldown = settings != null ? settings.cooldownSeconds : defaultSequenceCooldownSeconds;
            if (string.Equals(state.lastFiredSequenceName, match.gestureName, StringComparisonForNames()) &&
                Time.time - state.lastSequenceFireTime < Mathf.Max(0f, cooldown))
            {
                return;
            }

            match.bodyId = state.bodyId;
            match.bodyPosition = state.lastBodyPosition;
            match.gestureKind = AkgfGestureKind.Sequence;
            state.lastFiredSequenceName = match.gestureName;
            state.lastSequenceFireTime = Time.time;

            if (!state.hasPendingSequence || IsHigherPriority(match, state.pendingSequence))
            {
                state.pendingSequence = match;
                state.hasPendingSequence = true;
            }
        }

        private bool ValidateMatchAgainstSettings(AkgfTrackedBody body, ref AkgfGestureMatchResult match, AkgfGestureSettings settings, float defaultMinimumSimilarity)
        {
            if (settings != null)
            {
                if (!settings.enabled)
                {
                    return false;
                }

                if (groupController != null && !groupController.IsGroupActive(settings.groupName))
                {
                    return false;
                }
            }

            float gestureQuality = 1f;
            if (trackingQualityFilter != null)
            {
                if (!trackingQualityFilter.Passes(body, settings, out gestureQuality))
                {
                    return false;
                }

                match.similarity = trackingQualityFilter.ApplyQualityPenalty(match.similarity, gestureQuality, settings);
            }

            match.trackingQuality = gestureQuality;
            match.groupName = settings != null ? settings.groupName : match.groupName;
            match.priority = settings != null ? settings.priority : match.priority;

            float threshold = settings != null ? settings.minimumSimilarity : defaultMinimumSimilarity;
            return match.similarity >= threshold;
        }

        private void ResolvePendingCandidates(UserState state)
        {
            if (sequenceHasPriority && state.hasPendingSequence)
            {
                TryAccept(state, state.pendingSequence);
                return;
            }

            if (state.hasPendingStatic && Time.time >= state.staticBlockedUntil)
            {
                if (TryAccept(state, state.pendingStatic))
                {
                    return;
                }
            }

            if (state.hasPendingSequence)
            {
                TryAccept(state, state.pendingSequence);
            }
        }

        private bool TryAccept(UserState state, AkgfGestureMatchResult match)
        {
            if (!match.isValid || string.IsNullOrWhiteSpace(match.gestureName))
            {
                return false;
            }

            AkgfGestureSettings settings = GetSettings(match.gestureName, match.gestureKind);
            if (settings != null)
            {
                if (!settings.enabled)
                {
                    return false;
                }

                if (groupController != null && !groupController.IsGroupActive(settings.groupName))
                {
                    return false;
                }
            }

            if (Time.time - state.lastAnyFireTime < Mathf.Max(0f, globalCooldownSeconds))
            {
                return false;
            }

            string key = NormalizeKey(match.gestureName);
            float cooldown = settings != null ? settings.cooldownSeconds : sameGestureCooldownSeconds;
            if (state.lastFireByGesture.TryGetValue(key, out float lastTime) &&
                Time.time - lastTime < Mathf.Max(0f, cooldown))
            {
                return false;
            }

            bool isSameAsActive = IsActive(state, match);
            state.lastAnyFireTime = Time.time;
            state.lastFireByGesture[key] = Time.time;

            if (match.gestureKind == AkgfGestureKind.Sequence)
            {
                state.staticBlockedUntil = Time.time + Mathf.Max(0f, sequenceBlocksStaticSeconds);
            }

            if (!isSameAsActive)
            {
                if (!string.IsNullOrWhiteSpace(state.activeGestureName) && emitExitPhase)
                {
                    Emit(state, state.activeMatch, AkgfGesturePhase.Exit);
                }

                state.activeGestureName = match.gestureName;
                state.activeGestureKind = match.gestureKind;
                state.activeMatch = match;
                state.lastActiveRefreshTime = Time.time;

                if (emitEnterPhase && ShouldEmit(settings, AkgfGesturePhase.Enter))
                {
                    Emit(state, match, AkgfGesturePhase.Enter);
                }
            }
            else
            {
                state.activeMatch = match;
                state.lastActiveRefreshTime = Time.time;
                if (emitStayPhase && ShouldEmit(settings, AkgfGesturePhase.Stay))
                {
                    Emit(state, match, AkgfGesturePhase.Stay);
                }
            }

            if (emitDetectedPhase)
            {
                Emit(state, match, AkgfGesturePhase.Detected);
            }

            if (emitConfirmedPhase && ShouldEmit(settings, AkgfGesturePhase.Confirmed))
            {
                Emit(state, match, AkgfGesturePhase.Confirmed);
            }

            return true;
        }

        private void Emit(UserState state, AkgfGestureMatchResult match, AkgfGesturePhase phase)
        {
            if (!match.isValid)
            {
                return;
            }

            match.bodyId = state.bodyId;
            match.bodyPosition = state.lastBodyPosition;
            match.phase = phase;

            MultiUserGestureRecognized?.Invoke(match);
            MultiUserGesturePhase?.Invoke(match);
            onMultiUserGestureRecognized?.Invoke(state.bodyId, match.gestureName, match.similarity, match.gestureKind);
            onMultiUserGesturePhase?.Invoke(AkgfGestureEventData.FromMatch(match, AkgfTrackingMode.MultiUser));
        }

        private void MaybeEmitExitForLostGesture(UserState state)
        {
            if (!emitExitPhase || state == null || string.IsNullOrWhiteSpace(state.activeGestureName))
            {
                return;
            }

            if (Time.time - state.lastActiveRefreshTime < Mathf.Max(0.05f, exitAfterMissingSeconds))
            {
                return;
            }

            Emit(state, state.activeMatch, AkgfGesturePhase.Exit);
            state.activeGestureName = string.Empty;
            state.activeMatch = AkgfGestureMatchResult.None;
        }

        private void PruneLostUsers()
        {
            bodyIdsToRemove.Clear();
            foreach (KeyValuePair<int, UserState> pair in users)
            {
                UserState state = pair.Value;
                if (Time.time - state.lastSeenTime <= Mathf.Max(0.1f, lostUserTimeoutSeconds))
                {
                    continue;
                }

                if (emitExitPhase && !string.IsNullOrWhiteSpace(state.activeGestureName))
                {
                    Emit(state, state.activeMatch, AkgfGesturePhase.Exit);
                }

                bodyIdsToRemove.Add(pair.Key);
            }

            for (int i = 0; i < bodyIdsToRemove.Count; i++)
            {
                users.Remove(bodyIdsToRemove[i]);
            }
        }

        private void RefreshActiveBodyIdList()
        {
            activeBodyIds.Clear();
            foreach (KeyValuePair<int, UserState> pair in users)
            {
                activeBodyIds.Add(pair.Key);
            }
            activeBodyIds.Sort();
        }

        private void SampleSequencePoseIfDue(UserState state, AkgfNormalizedPose pose)
        {
            if (Time.time < state.nextSequenceSampleTime)
            {
                return;
            }

            state.nextSequenceSampleTime = Time.time + 1f / Mathf.Max(1f, samplesPerSecond);
            state.sequenceBuffer.Add(new TimedPose
            {
                time = Time.time,
                pose = pose.Clone()
            });
        }

        private AkgfPoseSequence BuildCurrentSequenceWindow(UserState state)
        {
            float cutoff = Time.time - Mathf.Max(0.05f, recognitionWindowSeconds);
            List<AkgfNormalizedPose> frames = new List<AkgfNormalizedPose>();
            for (int i = 0; i < state.sequenceBuffer.Count; i++)
            {
                TimedPose timedPose = state.sequenceBuffer[i];
                if (timedPose.time >= cutoff && timedPose.pose != null)
                {
                    frames.Add(timedPose.pose.Clone());
                }
            }

            if (frames.Count < Mathf.Max(2, minimumWindowFrames))
            {
                return null;
            }

            AkgfPoseSequence sequence = new AkgfPoseSequence
            {
                durationSeconds = recognitionWindowSeconds,
                sampleRate = samplesPerSecond,
                frames = frames
            };
            sequence.EnsureValid();
            return sequence;
        }

        private int CountFramesInsideWindow(UserState state)
        {
            float cutoff = Time.time - Mathf.Max(0.05f, recognitionWindowSeconds);
            int count = 0;
            for (int i = 0; i < state.sequenceBuffer.Count; i++)
            {
                if (state.sequenceBuffer[i].time >= cutoff)
                {
                    count++;
                }
            }
            return count;
        }

        private void PruneSequenceBuffer(UserState state)
        {
            float cutoff = Time.time - Mathf.Max(recognitionWindowSeconds, maxBufferSeconds);
            for (int i = state.sequenceBuffer.Count - 1; i >= 0; i--)
            {
                if (state.sequenceBuffer[i].time < cutoff)
                {
                    state.sequenceBuffer.RemoveAt(i);
                }
            }
        }

        private void ResetStaticStability(UserState state)
        {
            state.stableStaticName = string.Empty;
            state.staticStableSinceTime = Time.time;
        }

        private void ResetSequenceConsecutive(UserState state)
        {
            state.consecutiveSequenceName = string.Empty;
            state.consecutiveSequenceCount = 0;
        }

        private void ClearPendingCandidates(UserState state)
        {
            state.hasPendingStatic = false;
            state.hasPendingSequence = false;
            state.pendingStatic = AkgfGestureMatchResult.None;
            state.pendingSequence = AkgfGestureMatchResult.None;
        }

        private bool IsActive(UserState state, AkgfGestureMatchResult match)
        {
            return string.Equals(NormalizeKey(state.activeGestureName), NormalizeKey(match.gestureName), StringComparison.Ordinal) && state.activeGestureKind == match.gestureKind;
        }

        private bool ShouldEmit(AkgfGestureSettings settings, AkgfGesturePhase phase)
        {
            if (settings == null)
            {
                return true;
            }

            switch (phase)
            {
                case AkgfGesturePhase.Enter:
                    return settings.fireOnEnter;
                case AkgfGesturePhase.Stay:
                    return settings.fireOnStay;
                case AkgfGesturePhase.Exit:
                    return settings.fireOnExit;
                case AkgfGesturePhase.Confirmed:
                    return settings.fireOnConfirmed;
                default:
                    return true;
            }
        }

        private AkgfGestureSettings GetSettings(string gestureName, AkgfGestureKind kind)
        {
            return gestureSettingsDatabase != null ? gestureSettingsDatabase.GetSettings(gestureName, kind) : null;
        }

        private bool IsHigherPriority(AkgfGestureMatchResult a, AkgfGestureMatchResult b)
        {
            if (a.priority != b.priority)
            {
                return a.priority > b.priority;
            }

            return a.similarity > b.similarity;
        }

        private string NormalizeKey(string gestureName)
        {
            if (string.IsNullOrWhiteSpace(gestureName))
            {
                return string.Empty;
            }

            string trimmed = gestureName.Trim();
            return caseSensitiveGestureNames ? trimmed : trimmed.ToLowerInvariant();
        }

        private StringComparison StringComparisonForNames()
        {
            return caseSensitiveGestureNames ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        }
    }
}
