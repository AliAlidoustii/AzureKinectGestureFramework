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

        [Header("Normal Candidate Acceptance")]
        [Tooltip("Same idea as SingleUser > Normal Candidate Acceptance. If ON, MultiUser accepts the current best matcher candidate directly when it passes the direct threshold, then still applies Enter/Stay/Exit phases and cooldowns.")]
        public bool acceptCurrentCandidateDirectly = true;
        [Range(0f, 1f)] public float directStaticMinimumSimilarity = 0.55f;
        [Range(0f, 1f)] public float directSequenceMinimumSimilarity = 0.50f;
        [Tooltip("If ON, explicit per-gesture minimumSimilarity can override the direct threshold. Usually keep OFF while tuning.")]
        public bool usePerGestureThresholdForDirectCandidates = false;

        [Header("SingleUser-Compatible Acceptance")]
        [Tooltip("Legacy/general MultiUser threshold override. Usually OFF when Accept Current Candidate Directly is ON.")]
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

        [Header("MultiUser Diagnostics")]
        [Tooltip("Emergency debug only. If ON, MultiUser emits the best valid candidate directly so you can prove the matcher/API path works.")]
        public bool debugForceBestCandidateAsResult = false;
        [Tooltip("Emergency debug only. If ON, MultiUser ignores body/gesture tracking-quality filters.")]
        public bool debugIgnoreQualityFilter = false;
        [Tooltip("Keeps the last candidate/decision visible for a short time so the MultiUser debug window does not flicker between throttled sequence checks.")]
        public bool holdLastDebugValues = true;
        [Tooltip("How long the MultiUser debug UI keeps the last candidate visible after a skipped/throttled frame or short tracking drop.")]
        public float debugCandidateHoldSeconds = 0.45f;
        [Tooltip("Writes a compact MultiUser status line to Console every Debug Log Interval seconds.")]
        public bool debugLogDiagnosticsToConsole = false;
        public float debugLogIntervalSeconds = 1.0f;

        public event Action<AkgfGestureMatchResult> MultiUserGestureRecognized;
        public event Action<AkgfGestureMatchResult> MultiUserGesturePhase;

        public int VisibleBodyCount { get; private set; }
        public float LastProcessingMilliseconds { get; private set; }
        public AkgfGestureMatchResult LastStaticCandidate { get; private set; } = AkgfGestureMatchResult.None;
        public AkgfGestureMatchResult LastSequenceCandidate { get; private set; } = AkgfGestureMatchResult.None;
        public AkgfGestureMatchResult LastOutput { get; private set; } = AkgfGestureMatchResult.None;
        public string LastDecision { get; private set; } = "not evaluated yet";
        public string LastDebugSummary { get; private set; } = "not evaluated yet";
        public bool HasMultiSource => multiSource != null;
        public string MultiSourceName => multiSkeletonSourceBehaviour != null ? multiSkeletonSourceBehaviour.name : "None";
        public int LastRawBodyCount { get; private set; }
        public int LastTrackedBodyCount { get; private set; }
        public int LastNormalizedBodyCount { get; private set; }
        public int LoadedStaticGestureCount => gestureDatabase != null && gestureDatabase.Gestures != null ? gestureDatabase.Gestures.Count : 0;
        public int LoadedSequenceGestureCount => sequenceGestureDatabase != null && sequenceGestureDatabase.Gestures != null ? sequenceGestureDatabase.Gestures.Count : 0;
        public int ActiveUserCount => users.Count;
        public IReadOnlyList<int> ActiveBodyIds => activeBodyIds;

        private IAkgfMultiSkeletonSource multiSource;
        private readonly AkgfPoseNormalizer normalizer = new AkgfPoseNormalizer();
        private readonly Dictionary<int, UserState> users = new Dictionary<int, UserState>();
        private readonly List<int> activeBodyIds = new List<int>(8);
        private readonly List<int> bodyIdsToRemove = new List<int>(8);
        private readonly List<int> visibleRawBodyIds = new List<int>(8);
        private readonly List<AkgfTrackedBody> bodies = new List<AkgfTrackedBody>(8);
        private float nextDebugLogTime = -9999f;
        private float lastStaticCandidateDebugTime = -9999f;
        private float lastSequenceCandidateDebugTime = -9999f;
        private float lastOutputDebugTime = -9999f;

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
            LastRawBodyCount = 0;
            LastTrackedBodyCount = 0;
            LastNormalizedBodyCount = 0;
            lastStaticCandidateDebugTime = -9999f;
            lastSequenceCandidateDebugTime = -9999f;
            lastOutputDebugTime = -9999f;
            LastDecision = "users cleared";
            LastDebugSummary = "users cleared";
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

        public void ForceLoadDatabases()
        {
            ResolveReferences();
            gestureDatabase?.LoadAll();
            sequenceGestureDatabase?.LoadAll();
            gestureSettingsDatabase?.LoadAll();
            AdoptBestStaticDatabaseIfNeeded();
            AdoptBestSequenceDatabaseIfNeeded();
            LastDecision = $"databases loaded: static={LoadedStaticGestureCount}, sequence={LoadedSequenceGestureCount}";
            LastDebugSummary = LastDecision;
        }

        public void Tick()
        {
            // Do not clear candidate/decision every Update. Sequence recognition is intentionally throttled
            // by recognitionsPerSecond, so clearing here makes the MultiUser debug window flicker
            // between "candidate" and "none" on skipped frames even though the recognizer is healthy.
            ExpireHeldDebugValuesIfNeeded();

            LastRawBodyCount = 0;
            LastTrackedBodyCount = 0;
            LastNormalizedBodyCount = 0;

            EnsureDatabasesLoaded();

            if (multiSource == null)
            {
                ResolveReferences();
            }

            if (multiSource == null)
            {
                VisibleBodyCount = 0;
                LastDecision = "no multi-user skeleton source assigned";
                UpdateDebugSummary();
                MaybeLogDiagnostics();
                PruneLostUsers();
                RefreshActiveBodyIdList();
                return;
            }

            bodies.Clear();
            multiSource.GetTrackedBodies(bodies);
            VisibleBodyCount = bodies.Count;
            LastRawBodyCount = bodies.Count;

            visibleRawBodyIds.Clear();
            for (int i = 0; i < bodies.Count; i++)
            {
                AkgfTrackedBody visibleBody = bodies[i];
                if (visibleBody != null && visibleBody.IsTracked)
                {
                    LastTrackedBodyCount++;
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
            UpdateDebugSummary();
            MaybeLogDiagnostics();
        }

        private void ProcessBody(AkgfTrackedBody body)
        {
            Vector3 pelvisPosition = body.GetJoint(AkgfJointId.Pelvis);
            int userId = ResolveRuntimeBodyId(body.BodyId, pelvisPosition);
            UserState state = GetOrCreateUser(userId);
            state.lastSeenTime = Time.time;
            state.lastBodyPosition = pelvisPosition;

            state.lastTrackingQuality = useTrackingQualityFilter && trackingQualityFilter != null && !debugIgnoreQualityFilter
                ? trackingQualityFilter.ComputeBodyQuality(body)
                : 1f;

            if (useTrackingQualityFilter && trackingQualityFilter != null && !debugIgnoreQualityFilter && state.lastTrackingQuality < trackingQualityFilter.minimumOverallBodyQuality)
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

            LastNormalizedBodyCount++;
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

            bool hasStaticDatabase = gestureDatabase != null && gestureDatabase.Gestures != null && gestureDatabase.Gestures.Count > 0;
            bool hasSequenceDatabase = sequenceGestureDatabase != null && sequenceGestureDatabase.Gestures != null && sequenceGestureDatabase.Gestures.Count > 0;

            // Sequence-only projects are valid. SingleUser supports this because static and sequence recognizers
            // are separate components. MultiUser must therefore never treat an empty static DB as a blocking error.
            // If sequences are available and sequence priority is enabled, evaluate sequence first so a moving
            // gesture can win before any accidental static pose candidate.
            if (enableSequenceRecognition && hasSequenceDatabase && sequenceHasPriority)
            {
                RecognizeSequenceForUser(state, body, normalizedPose);
            }
            else if (enableSequenceRecognition && !hasSequenceDatabase)
            {
                LastDecision = "multi sequence: no sequence gesture database or no recorded sequence gestures";
                ResetSequenceConsecutive(state);
            }

            if (enableStaticPoseRecognition && hasStaticDatabase)
            {
                RecognizeStaticForUser(state, body, normalizedPose);
            }
            else if (enableStaticPoseRecognition && !hasStaticDatabase && !enableSequenceRecognition)
            {
                LastDecision = "multi static: no static gesture database or no recorded static gestures";
                ResetStaticStability(state);
            }

            if (enableSequenceRecognition && hasSequenceDatabase && !sequenceHasPriority)
            {
                RecognizeSequenceForUser(state, body, normalizedPose);
            }

            ResolvePendingCandidates(state);
            ClearPendingCandidates(state);
        }

        private void EnsureDatabasesLoaded()
        {
            if (gestureDatabase != null && (gestureDatabase.Gestures == null || gestureDatabase.Gestures.Count == 0))
            {
                gestureDatabase.LoadAll();
            }

            AdoptBestStaticDatabaseIfNeeded();

            if (sequenceGestureDatabase != null && (sequenceGestureDatabase.Gestures == null || sequenceGestureDatabase.Gestures.Count == 0))
            {
                sequenceGestureDatabase.LoadAll();
            }

            AdoptBestSequenceDatabaseIfNeeded();
        }

        private void AdoptBestStaticDatabaseIfNeeded()
        {
            if (gestureDatabase != null && gestureDatabase.Gestures != null && gestureDatabase.Gestures.Count > 0)
            {
                return;
            }

            AkgfGestureDatabase best = null;
            int bestCount = 0;
            AkgfGestureDatabase[] databases = AkgfUnityObjectFinder.FindAll<AkgfGestureDatabase>();
            for (int i = 0; i < databases.Length; i++)
            {
                AkgfGestureDatabase candidate = databases[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.Gestures == null || candidate.Gestures.Count == 0)
                {
                    candidate.LoadAll();
                }

                int count = candidate.Gestures != null ? candidate.Gestures.Count : 0;
                if (count > bestCount)
                {
                    best = candidate;
                    bestCount = count;
                }
            }

            if (best != null && bestCount > 0)
            {
                gestureDatabase = best;
            }
        }

        private void AdoptBestSequenceDatabaseIfNeeded()
        {
            if (sequenceGestureDatabase != null && sequenceGestureDatabase.Gestures != null && sequenceGestureDatabase.Gestures.Count > 0)
            {
                return;
            }

            AkgfSequenceGestureDatabase best = null;
            int bestCount = 0;
            AkgfSequenceGestureDatabase[] databases = AkgfUnityObjectFinder.FindAll<AkgfSequenceGestureDatabase>();
            for (int i = 0; i < databases.Length; i++)
            {
                AkgfSequenceGestureDatabase candidate = databases[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.Gestures == null || candidate.Gestures.Count == 0)
                {
                    candidate.LoadAll();
                }

                int count = candidate.Gestures != null ? candidate.Gestures.Count : 0;
                if (count > bestCount)
                {
                    best = candidate;
                    bestCount = count;
                }
            }

            if (best != null && bestCount > 0)
            {
                sequenceGestureDatabase = best;
            }
        }

        private void ExpireHeldDebugValuesIfNeeded()
        {
            if (!holdLastDebugValues)
            {
                LastStaticCandidate = AkgfGestureMatchResult.None;
                LastSequenceCandidate = AkgfGestureMatchResult.None;
                return;
            }

            float hold = Mathf.Max(0.05f, debugCandidateHoldSeconds);
            if (LastStaticCandidate.isValid && Time.time - lastStaticCandidateDebugTime > hold)
            {
                LastStaticCandidate = AkgfGestureMatchResult.None;
            }

            if (LastSequenceCandidate.isValid && Time.time - lastSequenceCandidateDebugTime > hold)
            {
                LastSequenceCandidate = AkgfGestureMatchResult.None;
            }

            if (LastOutput.isValid && Time.time - lastOutputDebugTime > Mathf.Max(hold, sameGestureCooldownSeconds))
            {
                LastOutput = AkgfGestureMatchResult.None;
            }
        }

        private void SetLastStaticCandidate(AkgfGestureMatchResult match)
        {
            LastStaticCandidate = match;
            if (match.isValid)
            {
                lastStaticCandidateDebugTime = Time.time;
            }
        }

        private void SetLastSequenceCandidate(AkgfGestureMatchResult match)
        {
            LastSequenceCandidate = match;
            if (match.isValid)
            {
                lastSequenceCandidateDebugTime = Time.time;
            }
        }

        private void SetLastOutput(AkgfGestureMatchResult match)
        {
            LastOutput = match;
            if (match.isValid)
            {
                lastOutputDebugTime = Time.time;
            }
        }

        private void UpdateDebugSummary()
        {
            LastDebugSummary =
                $"source={(HasMultiSource ? MultiSourceName : "NONE")}, " +
                $"raw={LastRawBodyCount}, tracked={LastTrackedBodyCount}, normalized={LastNormalizedBodyCount}, " +
                $"active={ActiveUserCount}, staticDB={LoadedStaticGestureCount}, seqDB={LoadedSequenceGestureCount}, " +
                $"static={FormatDebugMatch(LastStaticCandidate)}, seq={FormatDebugMatch(LastSequenceCandidate)}, " +
                $"out={FormatDebugMatch(LastOutput)}, decision={LastDecision}";
        }

        private void MaybeLogDiagnostics()
        {
            if (!debugLogDiagnosticsToConsole)
            {
                return;
            }

            if (Time.time < nextDebugLogTime)
            {
                return;
            }

            nextDebugLogTime = Time.time + Mathf.Max(0.1f, debugLogIntervalSeconds);
            Debug.Log("[AKGF MULTI DIAG] " + LastDebugSummary);
        }

        private static string FormatDebugMatch(AkgfGestureMatchResult match)
        {
            if (!match.isValid || string.IsNullOrWhiteSpace(match.gestureName))
            {
                return "none";
            }

            return $"{match.gestureName}/{match.gestureKind}/{AkgfGestureMatcher.FormatSimilarityPercent(match.similarity)}/q={match.trackingQuality:0.00}";
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
                // Empty static DB is not an error. Many apps use only sequence gestures.
                // Do not overwrite sequence diagnostics or block sequence output.
                ResetStaticStability(state);
                return;
            }

            AkgfGestureSettingsDatabase matcherSettingsDb = useExplicitGestureSettings ? gestureSettingsDatabase : null;
            AkgfGestureGroupController matcherGroups = useExplicitGestureSettings ? groupController : null;
            AkgfGestureMatchResult match = AkgfGestureMatcher.FindBestMatch(pose, gestureDatabase.Gestures, staticMatcherSettings, matcherSettingsDb, matcherGroups);
            match.bodyId = state.bodyId;
            match.bodyPosition = state.lastBodyPosition;
            match.gestureKind = AkgfGestureKind.StaticPose;
            SetLastStaticCandidate(match);

            if (!match.isValid)
            {
                LastDecision = $"body {state.bodyId}: no valid static match";
                ResetStaticStability(state);
                return;
            }

            AkgfGestureSettings explicitSettings = GetExplicitSettings(match.gestureName, AkgfGestureKind.StaticPose);
            if (!ValidateCandidate(body, ref match, explicitSettings, AkgfGestureKind.StaticPose))
            {
                SetLastStaticCandidate(match);
                ResetStaticStability(state);
                return;
            }

            SetLastStaticCandidate(match);

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
                // Recognition is throttled. Keep the previous candidate/decision visible instead of
                // briefly showing none in the debug UI.
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
            SetLastSequenceCandidate(match);

            if (!match.isValid)
            {
                LastDecision = $"body {state.bodyId}: no valid sequence match";
                ResetSequenceConsecutive(state);
                return;
            }

            AkgfGestureSettings explicitSettings = GetExplicitSettings(match.gestureName, AkgfGestureKind.Sequence);
            if (!ValidateCandidate(body, ref match, explicitSettings, AkgfGestureKind.Sequence))
            {
                SetLastSequenceCandidate(match);
                ResetSequenceConsecutive(state);
                return;
            }

            SetLastSequenceCandidate(match);

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
            if (useTrackingQualityFilter && trackingQualityFilter != null && !debugIgnoreQualityFilter)
            {
                AkgfGestureSettings qualitySettings = useExplicitGestureSettings ? explicitSettings : null;
                if (!trackingQualityFilter.Passes(body, qualitySettings, out gestureQuality))
                {
                    LastDecision = $"blocked: tracking quality {gestureQuality:0.00} below requirement for {match.gestureName}";
                    return false;
                }

                match.similarity = trackingQualityFilter.ApplyQualityPenalty(match.similarity, gestureQuality, qualitySettings);
            }
            else if (debugIgnoreQualityFilter)
            {
                gestureQuality = 1f;
            }

            match.trackingQuality = gestureQuality;
            match.groupName = explicitSettings != null ? explicitSettings.groupName : match.groupName;
            match.priority = explicitSettings != null ? explicitSettings.priority : match.priority;

            float threshold = GetAcceptanceThreshold(kind, explicitSettings);
            threshold = Mathf.Clamp01(threshold);

            if (!debugForceBestCandidateAsResult && match.similarity < threshold)
            {
                string thresholdMode = acceptCurrentCandidateDirectly ? "direct" : "normal";
                LastDecision = $"body {match.bodyId}: blocked {match.gestureName} {kind} {AkgfGestureMatcher.FormatSimilarityPercent(match.similarity)} < {AkgfGestureMatcher.FormatSimilarityPercent(threshold)} ({thresholdMode} threshold)";
                return false;
            }

            return true;
        }

        private float GetAcceptanceThreshold(AkgfGestureKind kind, AkgfGestureSettings explicitSettings)
        {
            if (acceptCurrentCandidateDirectly)
            {
                float directThreshold = kind == AkgfGestureKind.Sequence
                    ? directSequenceMinimumSimilarity
                    : directStaticMinimumSimilarity;

                if (usePerGestureThresholdForDirectCandidates && explicitSettings != null)
                {
                    return explicitSettings.minimumSimilarity;
                }

                return directThreshold;
            }

            float threshold = kind == AkgfGestureKind.Sequence
                ? defaultSequenceMinimumSimilarity
                : defaultStaticMinimumSimilarity;

            if (usePerGestureThresholds && explicitSettings != null)
            {
                threshold = explicitSettings.minimumSimilarity;
            }

            return threshold;
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

            // Force mode bypasses only the similarity threshold. It must still respect
            // phase state and cooldowns, otherwise Emit Enter fires every frame.
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

            // Even in force mode, keep the normal phase logic.
            // This lets Emit Enter fire once, and prevents per-frame Console spam while the same gesture is still active.
            bool isSameAsActive = IsActive(state, match);
            state.lastAnyFireTime = Time.time;
            state.lastFireByGesture[key] = Time.time;
            SetLastOutput(match);
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
