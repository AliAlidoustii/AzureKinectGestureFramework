using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Multi-user version of the working SingleUser pipeline.
    /// Each visible body gets its own independent state: static stability, sequence buffer,
    /// cooldowns, active phase, calibration, and stable body ID.
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
            public float lastTrackingQuality;
            public AkgfNormalizedPose lastPose;

            public bool calibrationStarted;
            public bool calibrationFinished;
            public readonly List<AkgfNormalizedPose> calibrationSamples = new List<AkgfNormalizedPose>();
            public AkgfCalibrationProfile calibrationProfile;

            public string stableStaticName = string.Empty;
            public float staticStableSinceTime = -9999f;

            public readonly List<TimedPose> sequenceBuffer = new List<TimedPose>(128);
            public float nextSequenceSampleTime;
            public float nextSequenceRecognitionTime;
            public string consecutiveSequenceName = string.Empty;
            public int consecutiveSequenceCount;

            public bool hasPendingStatic;
            public bool hasPendingSequence;
            public AkgfGestureMatchResult pendingStatic;
            public AkgfGestureMatchResult pendingSequence;

            public string activeGestureName = string.Empty;
            public AkgfGestureKind activeGestureKind = AkgfGestureKind.StaticPose;
            public AkgfGestureMatchResult activeMatch = AkgfGestureMatchResult.None;
            public float lastActiveRefreshTime = -9999f;

            public float staticBlockedUntil = -9999f;
            public float lastAnyFireTime = -9999f;
            public readonly Dictionary<string, float> lastFireByGesture = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        }

        [Header("Source")]
        [Tooltip("A MonoBehaviour that implements IAkgfMultiSkeletonSource, for example AKGF_KinectTrackerHandler.")]
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
        [Range(0f, 1f)] public float defaultStaticMinimumSimilarity = 0.55f;
        [Range(0f, 1f)] public float defaultSequenceMinimumSimilarity = 0.50f;
        public float defaultStaticStableSeconds = 0f;
        public float defaultStaticCooldownSeconds = 1.0f;
        public float defaultSequenceCooldownSeconds = 1.0f;

        [Header("SingleUser-Compatible Acceptance")]
        [Tooltip("OFF by default so MultiUser behaves like the working SingleUser direct-acceptance path. When OFF, per-gesture minimumSimilarity does not override the MultiUser default thresholds.")]
        public bool usePerGestureThresholds = false;
        [Tooltip("OFF by default so MultiUser cooldowns are controlled by this component. Turn ON if every gesture should use its own settings cooldown.")]
        public bool usePerGestureCooldowns = false;
        [Tooltip("OFF by default so MultiUser static hold time is controlled by Default Static Stable Seconds.")]
        public bool usePerGestureStableSeconds = false;
        [Tooltip("Use explicit gesture settings for enabled/group/priority/mirrorMode/requiredJoints when an entry exists in the settings database.")]
        public bool useExplicitGestureSettings = true;
        [Tooltip("If true, quality filtering and quality penalty are applied. Keep ON for real use, turn OFF only for emergency debugging.")]
        public bool useTrackingQualityFilter = true;

        [Header("Sequence")]
        public float recognitionWindowSeconds = 1.25f;
        public float maxBufferSeconds = 3f;
        public float samplesPerSecond = 15f;
        public float recognitionsPerSecond = 10f;
        public int minimumWindowFrames = 8;
        [Range(1, 10)] public int requiredConsecutiveSequenceMatches = 1;

        [Header("Runtime Calibration")]
        public bool autoCalibrateNewUsers = true;
        [Tooltip("If true, a new user is ignored until their neutral profile is collected.")]
        public bool requireCalibrationBeforeRecognition = false;
        public float calibrationSeconds = 2.0f;
        [Range(0f, 1f)] public float calibrationStrength = 0.75f;
        public bool applyCalibrationToRecognition = true;

        [Header("Conflict Rules Per User")]
        public bool sequenceHasPriority = true;
        public float sequenceBlocksStaticSeconds = 0.50f;
        public float globalCooldownSeconds = 0.30f;
        public float sameGestureCooldownSeconds = 1.0f;
        public bool caseSensitiveGestureNames = false;

        [Header("Body ID Stability")]
        [Tooltip("When enabled, emitted bodyId values are stable user IDs, not raw Azure Kinect body IDs.")]
        public bool useStableUserIds = true;
        public bool autoCreateIdentityTracker = true;

        [Header("User Lifetime")]
        public float lostUserTimeoutSeconds = 2.50f;
        public int MaxActiveUsers => users.Count;

        [Header("Event Phases")]
        public bool emitDetectedPhase = false;
        public bool emitEnterPhase = true;
        public bool emitStayPhase = false;
        public bool emitExitPhase = false;
        public bool emitConfirmedPhase = false;
        public float exitAfterMissingSeconds = 0.45f;

        [Header("Events")]
        public AkgfMultiUserGestureRecognizedEvent onMultiUserGestureRecognized = new AkgfMultiUserGestureRecognizedEvent();
        public AkgfMultiUserGesturePhaseEvent onMultiUserGesturePhase = new AkgfMultiUserGesturePhaseEvent();

        public event Action<AkgfGestureMatchResult> MultiUserGestureRecognized;
        public event Action<AkgfGestureMatchResult> MultiUserGesturePhase;

        public int VisibleBodyCount { get; private set; }
        public float LastProcessingMilliseconds { get; private set; }
        public AkgfGestureMatchResult LastStaticCandidate { get; private set; } = AkgfGestureMatchResult.None;
        public AkgfGestureMatchResult LastSequenceCandidate { get; private set; } = AkgfGestureMatchResult.None;
        public AkgfGestureMatchResult LastOutput { get; private set; } = AkgfGestureMatchResult.None;
        public string LastDecision { get; private set; } = "not evaluated yet";
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

        private void OnEnable()
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
            visibleRawBodyIds.Clear();
            bodies.Clear();
            LastStaticCandidate = AkgfGestureMatchResult.None;
            LastSequenceCandidate = AkgfGestureMatchResult.None;
            LastOutput = AkgfGestureMatchResult.None;
            LastDecision = "users cleared";
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
            LastStaticCandidate = AkgfGestureMatchResult.None;
            LastSequenceCandidate = AkgfGestureMatchResult.None;
            LastDecision = "not evaluated yet";

            if (multiSource == null)
            {
                ResolveReferences();
            }

            if (multiSource == null)
            {
                VisibleBodyCount = 0;
                LastDecision = "no multi-user skeleton source assigned";
                PruneLostUsers();
                RefreshActiveBodyIdList();
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

            if (VisibleBodyCount == 0)
            {
                LastDecision = "no visible bodies";
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

            state.lastTrackingQuality = useTrackingQualityFilter && trackingQualityFilter != null
                ? trackingQualityFilter.ComputeBodyQuality(body)
                : 1f;

            if (useTrackingQualityFilter && trackingQualityFilter != null && state.lastTrackingQuality < trackingQualityFilter.minimumOverallBodyQuality)
            {
                ResetStaticStability(state);
                ResetSequenceConsecutive(state);
                LastDecision = $"body {state.bodyId}: body quality {state.lastTrackingQuality:0.00} below minimum {trackingQualityFilter.minimumOverallBodyQuality:0.00}";
                return;
            }

            if (!normalizer.TryNormalize(body, normalizerSettings, out AkgfNormalizedPose normalizedPose))
            {
                ResetStaticStability(state);
                ResetSequenceConsecutive(state);
                LastDecision = $"body {state.bodyId}: could not normalize pose";
                return;
            }

            UpdateRuntimeCalibration(state, normalizedPose);
            if (requireCalibrationBeforeRecognition && autoCalibrateNewUsers && !state.calibrationFinished)
            {
                LastDecision = $"body {state.bodyId}: waiting for calibration";
                return;
            }

            if (applyCalibrationToRecognition && state.calibrationProfile != null && state.calibrationProfile.IsUsable)
            {
                normalizedPose = AkgfCalibrationProcessor.Apply(normalizedPose, state.calibrationProfile, calibrationStrength);
            }

            state.lastPose = normalizedPose;

            if (enableStaticPoseRecognition)
            {
                RecognizeStaticForUser(state, body, normalizedPose);
            }

            if (enableSequenceRecognition)
            {
                RecognizeSequenceForUser(state, body, normalizedPose);
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

        private void RecognizeStaticForUser(UserState state, AkgfTrackedBody body, AkgfNormalizedPose pose)
        {
            if (gestureDatabase == null || gestureDatabase.Gestures == null || gestureDatabase.Gestures.Count == 0)
            {
                LastDecision = "multi static: no static gesture database or no recorded static gestures";
                ResetStaticStability(state);
                return;
            }

            AkgfGestureSettingsDatabase matcherSettingsDb = useExplicitGestureSettings ? gestureSettingsDatabase : null;
            AkgfGestureGroupController matcherGroups = useExplicitGestureSettings ? groupController : null;
            AkgfGestureMatchResult match = AkgfGestureMatcher.FindBestMatch(pose, gestureDatabase.Gestures, staticMatcherSettings, matcherSettingsDb, matcherGroups);
            match.bodyId = state.bodyId;
            match.bodyPosition = state.lastBodyPosition;
            match.gestureKind = AkgfGestureKind.StaticPose;
            LastStaticCandidate = match;

            if (!match.isValid)
            {
                LastDecision = $"body {state.bodyId}: no valid static match";
                ResetStaticStability(state);
                return;
            }

            AkgfGestureSettings explicitSettings = GetExplicitSettings(match.gestureName, AkgfGestureKind.StaticPose);
            if (!ValidateCandidate(body, ref match, explicitSettings, AkgfGestureKind.StaticPose))
            {
                LastStaticCandidate = match;
                ResetStaticStability(state);
                return;
            }

            LastStaticCandidate = match;

            float requiredStable = usePerGestureStableSeconds && explicitSettings != null
                ? explicitSettings.requiredStableSeconds
                : defaultStaticStableSeconds;

            if (!UpdateStaticStabilityAndCheckReady(state, match, requiredStable))
            {
                return;
            }

            if (!state.hasPendingStatic || IsHigherPriority(match, state.pendingStatic))
            {
                state.pendingStatic = match;
                state.hasPendingStatic = true;
            }
        }

        private bool UpdateStaticStabilityAndCheckReady(UserState state, AkgfGestureMatchResult match, float requiredStableSeconds)
        {
            requiredStableSeconds = Mathf.Max(0f, requiredStableSeconds);
            bool sameAsStable = string.Equals(state.stableStaticName, match.gestureName, StringComparisonForNames());

            if (!sameAsStable)
            {
                state.stableStaticName = match.gestureName;
                state.staticStableSinceTime = Time.time;

                if (requiredStableSeconds > 0.0001f)
                {
                    LastDecision = $"body {state.bodyId}: static waiting for hold {match.gestureName} 0.00/{requiredStableSeconds:0.00}s";
                    return false;
                }
            }
            else if (Time.time - state.staticStableSinceTime < requiredStableSeconds)
            {
                LastDecision = $"body {state.bodyId}: static waiting for hold {match.gestureName} {(Time.time - state.staticStableSinceTime):0.00}/{requiredStableSeconds:0.00}s";
                return false;
            }

            return true;
        }

        private void RecognizeSequenceForUser(UserState state, AkgfTrackedBody body, AkgfNormalizedPose pose)
        {
            SampleSequencePoseIfDue(state, pose);
            PruneSequenceBuffer(state);

            if (sequenceGestureDatabase == null || sequenceGestureDatabase.Gestures == null || sequenceGestureDatabase.Gestures.Count == 0)
            {
                LastDecision = "multi sequence: no sequence gesture database or no recorded sequence gestures";
                ResetSequenceConsecutive(state);
                return;
            }

            int framesInWindow = CountFramesInsideWindow(state);
            if (framesInWindow < Mathf.Max(2, minimumWindowFrames))
            {
                ResetSequenceConsecutive(state);
                LastDecision = $"body {state.bodyId}: sequence buffer {framesInWindow}/{Mathf.Max(2, minimumWindowFrames)}";
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
                LastDecision = $"body {state.bodyId}: sequence window invalid";
                return;
            }

            AkgfGestureSettingsDatabase matcherSettingsDb = useExplicitGestureSettings ? gestureSettingsDatabase : null;
            AkgfGestureGroupController matcherGroups = useExplicitGestureSettings ? groupController : null;
            AkgfGestureMatchResult match = AkgfSequenceGestureMatcher.FindBestMatch(window, sequenceGestureDatabase.Gestures, sequenceMatcherSettings, matcherSettingsDb, matcherGroups);
            match.bodyId = state.bodyId;
            match.bodyPosition = state.lastBodyPosition;
            match.gestureKind = AkgfGestureKind.Sequence;
            LastSequenceCandidate = match;

            if (!match.isValid)
            {
                LastDecision = $"body {state.bodyId}: no valid sequence match";
                ResetSequenceConsecutive(state);
                return;
            }

            AkgfGestureSettings explicitSettings = GetExplicitSettings(match.gestureName, AkgfGestureKind.Sequence);
            if (!ValidateCandidate(body, ref match, explicitSettings, AkgfGestureKind.Sequence))
            {
                LastSequenceCandidate = match;
                ResetSequenceConsecutive(state);
                return;
            }

            LastSequenceCandidate = match;

            int requiredMatches = Mathf.Max(1, requiredConsecutiveSequenceMatches);
            if (!UpdateSequenceConsecutiveAndCheckReady(state, match, requiredMatches))
            {
                return;
            }

            if (!state.hasPendingSequence || IsHigherPriority(match, state.pendingSequence))
            {
                state.pendingSequence = match;
                state.hasPendingSequence = true;
            }
        }

        private bool UpdateSequenceConsecutiveAndCheckReady(UserState state, AkgfGestureMatchResult match, int requiredMatches)
        {
            if (!string.Equals(state.consecutiveSequenceName, match.gestureName, StringComparisonForNames()))
            {
                state.consecutiveSequenceName = match.gestureName;
                state.consecutiveSequenceCount = 1;

                if (requiredMatches > 1)
                {
                    LastDecision = $"body {state.bodyId}: sequence waiting for consecutive {match.gestureName} 1/{requiredMatches}";
                    return false;
                }
            }
            else
            {
                state.consecutiveSequenceCount++;
                if (state.consecutiveSequenceCount < requiredMatches)
                {
                    LastDecision = $"body {state.bodyId}: sequence waiting for consecutive {match.gestureName} {state.consecutiveSequenceCount}/{requiredMatches}";
                    return false;
                }
            }

            return true;
        }

        private bool ValidateCandidate(AkgfTrackedBody body, ref AkgfGestureMatchResult match, AkgfGestureSettings explicitSettings, AkgfGestureKind kind)
        {
            if (useExplicitGestureSettings && explicitSettings != null)
            {
                if (!explicitSettings.enabled)
                {
                    LastDecision = $"blocked: settings disabled for {match.gestureName}";
                    return false;
                }

                if (groupController != null && !groupController.IsGroupActive(explicitSettings.groupName))
                {
                    LastDecision = $"blocked: group '{explicitSettings.groupName}' is not active";
                    return false;
                }
            }

            float gestureQuality = 1f;
            if (useTrackingQualityFilter && trackingQualityFilter != null)
            {
                AkgfGestureSettings qualitySettings = useExplicitGestureSettings ? explicitSettings : null;
                if (!trackingQualityFilter.Passes(body, qualitySettings, out gestureQuality))
                {
                    LastDecision = $"blocked: tracking quality {gestureQuality:0.00} below requirement for {match.gestureName}";
                    return false;
                }

                match.similarity = trackingQualityFilter.ApplyQualityPenalty(match.similarity, gestureQuality, qualitySettings);
            }

            match.trackingQuality = gestureQuality;
            match.groupName = explicitSettings != null ? explicitSettings.groupName : match.groupName;
            match.priority = explicitSettings != null ? explicitSettings.priority : match.priority;

            float threshold = kind == AkgfGestureKind.Sequence ? defaultSequenceMinimumSimilarity : defaultStaticMinimumSimilarity;
            if (usePerGestureThresholds && explicitSettings != null)
            {
                threshold = explicitSettings.minimumSimilarity;
            }

            threshold = Mathf.Clamp01(threshold);
            if (match.similarity < threshold)
            {
                LastDecision = $"body {match.bodyId}: blocked {match.gestureName} {kind} {AkgfGestureMatcher.FormatSimilarityPercent(match.similarity)} < {AkgfGestureMatcher.FormatSimilarityPercent(threshold)}";
                return false;
            }

            return true;
        }

        private void ResolvePendingCandidates(UserState state)
        {
            if (sequenceHasPriority && state.hasPendingSequence)
            {
                if (TryAccept(state, state.pendingSequence))
                {
                    return;
                }
            }

            if (state.hasPendingStatic && Time.time >= state.staticBlockedUntil)
            {
                if (TryAccept(state, state.pendingStatic))
                {
                    return;
                }
            }

            if (!sequenceHasPriority && state.hasPendingSequence)
            {
                TryAccept(state, state.pendingSequence);
            }
        }

        private bool TryAccept(UserState state, AkgfGestureMatchResult match)
        {
            if (!match.isValid || string.IsNullOrWhiteSpace(match.gestureName))
            {
                LastDecision = $"body {state.bodyId}: candidate invalid or missing gesture name";
                return false;
            }

            AkgfGestureSettings explicitSettings = GetExplicitSettings(match.gestureName, match.gestureKind);
            if (useExplicitGestureSettings && explicitSettings != null)
            {
                if (!explicitSettings.enabled)
                {
                    LastDecision = $"blocked: settings disabled for {match.gestureName}";
                    return false;
                }

                if (groupController != null && !groupController.IsGroupActive(explicitSettings.groupName))
                {
                    LastDecision = $"blocked: group '{explicitSettings.groupName}' is not active";
                    return false;
                }
            }

            if (Time.time - state.lastAnyFireTime < Mathf.Max(0f, globalCooldownSeconds))
            {
                LastDecision = $"body {state.bodyId}: blocked global cooldown {globalCooldownSeconds:0.00}s";
                return false;
            }

            string key = NormalizeKey(match.gestureName);
            float kindCooldown = match.gestureKind == AkgfGestureKind.Sequence ? defaultSequenceCooldownSeconds : defaultStaticCooldownSeconds;
            float cooldown = usePerGestureCooldowns && explicitSettings != null
                ? explicitSettings.cooldownSeconds
                : Mathf.Max(kindCooldown, sameGestureCooldownSeconds);

            if (state.lastFireByGesture.TryGetValue(key, out float lastTime) &&
                Time.time - lastTime < Mathf.Max(0f, cooldown))
            {
                LastDecision = $"body {state.bodyId}: blocked same gesture cooldown {cooldown:0.00}s";
                return false;
            }

            bool isSameAsActive = IsActive(state, match);
            state.lastAnyFireTime = Time.time;
            state.lastFireByGesture[key] = Time.time;
            LastOutput = match;
            LastDecision = $"accepted: body {state.bodyId} {match.gestureName} {match.gestureKind} {AkgfGestureMatcher.FormatSimilarityPercent(match.similarity)}";

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

                if (emitEnterPhase && ShouldEmit(explicitSettings, AkgfGesturePhase.Enter))
                {
                    Emit(state, match, AkgfGesturePhase.Enter);
                }
            }
            else
            {
                state.activeMatch = match;
                state.lastActiveRefreshTime = Time.time;
                if (emitStayPhase && ShouldEmit(explicitSettings, AkgfGesturePhase.Stay))
                {
                    Emit(state, match, AkgfGesturePhase.Stay);
                }
            }

            if (emitDetectedPhase)
            {
                Emit(state, match, AkgfGesturePhase.Detected);
            }

            if (emitConfirmedPhase && ShouldEmit(explicitSettings, AkgfGesturePhase.Confirmed))
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
            return string.Equals(NormalizeKey(state.activeGestureName), NormalizeKey(match.gestureName), StringComparison.Ordinal) &&
                   state.activeGestureKind == match.gestureKind;
        }

        private bool ShouldEmit(AkgfGestureSettings settings, AkgfGesturePhase phase)
        {
            if (!useExplicitGestureSettings || settings == null)
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

        private AkgfGestureSettings GetExplicitSettings(string gestureName, AkgfGestureKind kind)
        {
            if (!useExplicitGestureSettings || gestureSettingsDatabase == null)
            {
                return null;
            }

            if (gestureSettingsDatabase.TryGetExplicitSettings(gestureName, kind, out AkgfGestureSettings settings))
            {
                return settings;
            }

            return null;
        }

        private bool IsHigherPriority(AkgfGestureMatchResult a, AkgfGestureMatchResult b)
        {
            if (a.priority != b.priority)
            {
                return a.priority > b.priority;
            }

            if (a.gestureKind != b.gestureKind)
            {
                return a.gestureKind == AkgfGestureKind.Sequence;
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
