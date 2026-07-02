using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfGestureRecognizedDetailedEvent : UnityEvent<string, float, AkgfGestureKind>
    {
    }

    [Serializable]
    public sealed class AkgfGesturePhaseDetailedEvent : UnityEvent<string, float, AkgfGestureKind, AkgfGesturePhase>
    {
    }

    public sealed class AkgfGestureCoordinator : MonoBehaviour
    {
        [Header("Recognizers")]
        public AkgfGestureRecognizer staticPoseRecognizer;
        public AkgfSequenceGestureRecognizer sequenceRecognizer;
        public AkgfGestureSettingsDatabase gestureSettingsDatabase;
        public AkgfGestureGroupController groupController;
        public bool autoFindRecognizers = true;

        [Header("Conflict Rules")]
        public bool sequenceHasPriority = true;
        [Tooltip("After a sequence gesture fires, static poses are ignored for this many seconds.")]
        public float sequenceBlocksStaticSeconds = 0.60f;
        [Tooltip("Prevents immediate back-to-back events, even if they are different gestures.")]
        public float globalCooldownSeconds = 0.08f;
        [Tooltip("Extra cooldown per gesture name at coordinator level. Per-gesture settings can override this.")]
        public float sameGestureCooldownSeconds = 0.75f;
        public bool caseSensitiveGestureNames = false;

        [Header("Normal Candidate Acceptance")]
        [Tooltip("When enabled, the coordinator can accept the recognizers' current LastMatch candidates directly, instead of relying only on recognizer events. This fixes cases where candidates are visible in debug UI but never become final output.")]
        public bool acceptCurrentCandidateDirectly = true;
        [Tooltip("Minimum similarity for directly accepting static pose candidates at coordinator level.")]
        [Range(0f, 1f)] public float directStaticMinimumSimilarity = 0.60f;
        [Tooltip("Minimum similarity for directly accepting sequence candidates at coordinator level.")]
        [Range(0f, 1f)] public float directSequenceMinimumSimilarity = 0.55f;
        [Tooltip("If enabled, per-gesture minimumSimilarity can raise the direct candidate threshold. Disable while debugging if settings database is too strict.")]
        public bool usePerGestureThresholdForDirectCandidates = false;
        [Tooltip("If enabled, recognizer minimumSimilarity can raise the direct candidate threshold. Disable while debugging if recognizer threshold is too strict.")]
        public bool useRecognizerThresholdForDirectCandidates = false;

        [Header("Debug Bypass")]
        [Tooltip("Debug only: promote the current best recognizer candidate directly into a final event, bypassing group, cooldown and per-gesture phase gates. Use this only while testing.")]
        public bool debugForceEmitBestCandidateAsResult = false;
        [Tooltip("Limits console/API spam while debugForceEmitBestCandidateAsResult is enabled.")]
        public float debugForceEmitIntervalSeconds = 0.20f;

        [Header("Event Modes")]
        public bool emitDetectedPhase = true;
        public bool emitEnterPhase = true;
        public bool emitStayPhase = false;
        public bool emitExitPhase = true;
        public bool emitConfirmedPhase = true;
        [Tooltip("If no recognizer candidate refreshes the active gesture for this long, an Exit event is emitted.")]
        public float exitAfterMissingSeconds = 0.45f;

        [Header("Events")]
        public AkgfGestureRecognizedEvent onGestureRecognized = new AkgfGestureRecognizedEvent();
        public AkgfGestureRecognizedDetailedEvent onGestureRecognizedDetailed = new AkgfGestureRecognizedDetailedEvent();
        public AkgfGesturePhaseDetailedEvent onGesturePhaseDetailed = new AkgfGesturePhaseDetailedEvent();

        public event Action<AkgfGestureMatchResult> GestureRecognized;
        public event Action<AkgfGestureMatchResult> GesturePhase;

        public AkgfGestureMatchResult LastOutput { get; private set; } = AkgfGestureMatchResult.None;
        public string LastDecision { get; private set; } = "not evaluated yet";
        public string ActiveGestureName { get; private set; } = string.Empty;
        public AkgfGestureKind ActiveGestureKind { get; private set; } = AkgfGestureKind.StaticPose;

        private bool staticRecognizerSubscribed;
        private bool sequenceRecognizerSubscribed;
        private bool hasPendingStatic;
        private bool hasPendingSequence;
        private AkgfGestureMatchResult pendingStatic;
        private AkgfGestureMatchResult pendingSequence;
        private float staticBlockedUntil = -9999f;
        private float lastAnyFireTime = -9999f;
        private float lastDebugForceEmitTime = -9999f;
        private float lastActiveRefreshTime = -9999f;
        private AkgfGestureMatchResult activeMatch;
        private readonly Dictionary<string, float> lastFireByGesture = new Dictionary<string, float>(StringComparer.Ordinal);

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            // Components can be created or assigned after this component was enabled.
            // Keep the event pipe connected instead of assuming OnEnable found everything.
            ResolveReferences();
            Subscribe();

            if (debugForceEmitBestCandidateAsResult && DebugForceEmitCurrentCandidate())
            {
                ClearPendingCandidates();
                MaybeEmitExitForLostGesture();
                return;
            }

            if (acceptCurrentCandidateDirectly)
            {
                PullCurrentRecognizerCandidatesIntoPending();
            }

            ResolvePendingCandidates();
            ClearPendingCandidates();
            MaybeEmitExitForLostGesture();
        }

        public void ResolveReferences()
        {
            if (!autoFindRecognizers)
            {
                return;
            }

            if (staticPoseRecognizer == null)
            {
                staticPoseRecognizer = AkgfUnityObjectFinder.FindFirst<AkgfGestureRecognizer>();
            }

            if (sequenceRecognizer == null)
            {
                sequenceRecognizer = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureRecognizer>();
            }

            if (gestureSettingsDatabase == null)
            {
                gestureSettingsDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureSettingsDatabase>();
            }

            if (groupController == null)
            {
                groupController = AkgfUnityObjectFinder.FindFirst<AkgfGestureGroupController>();
            }
        }

        public void Reconnect()
        {
            Unsubscribe();
            ResolveReferences();
            Subscribe();
        }

        private void Subscribe()
        {
            if (staticPoseRecognizer != null && !staticRecognizerSubscribed)
            {
                staticPoseRecognizer.GestureRecognized += HandleStaticGesture;
                staticRecognizerSubscribed = true;
            }

            if (sequenceRecognizer != null && !sequenceRecognizerSubscribed)
            {
                sequenceRecognizer.SequenceGestureRecognized += HandleSequenceGesture;
                sequenceRecognizerSubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (staticPoseRecognizer != null && staticRecognizerSubscribed)
            {
                staticPoseRecognizer.GestureRecognized -= HandleStaticGesture;
            }

            if (sequenceRecognizer != null && sequenceRecognizerSubscribed)
            {
                sequenceRecognizer.SequenceGestureRecognized -= HandleSequenceGesture;
            }

            staticRecognizerSubscribed = false;
            sequenceRecognizerSubscribed = false;
        }

        private void HandleStaticGesture(AkgfGestureMatchResult match)
        {
            if (!match.isValid)
            {
                return;
            }

            match.gestureKind = AkgfGestureKind.StaticPose;
            EnrichFromSettings(ref match);
            if (!hasPendingStatic || IsHigherPriority(match, pendingStatic))
            {
                pendingStatic = match;
                hasPendingStatic = true;
            }
        }

        private void HandleSequenceGesture(AkgfGestureMatchResult match)
        {
            if (!match.isValid)
            {
                return;
            }

            match.gestureKind = AkgfGestureKind.Sequence;
            EnrichFromSettings(ref match);
            if (!hasPendingSequence || IsHigherPriority(match, pendingSequence))
            {
                pendingSequence = match;
                hasPendingSequence = true;
            }
        }


        private void PullCurrentRecognizerCandidatesIntoPending()
        {
            if (staticPoseRecognizer != null)
            {
                AkgfGestureMatchResult staticMatch = staticPoseRecognizer.LastMatch;
                staticMatch.gestureKind = AkgfGestureKind.StaticPose;
                if (PassesDirectCandidateGate(staticMatch))
                {
                    EnrichFromSettings(ref staticMatch);
                    if (!hasPendingStatic || IsHigherPriority(staticMatch, pendingStatic))
                    {
                        pendingStatic = staticMatch;
                        hasPendingStatic = true;
                    }
                }
            }

            if (sequenceRecognizer != null)
            {
                AkgfGestureMatchResult sequenceMatch = sequenceRecognizer.LastMatch;
                sequenceMatch.gestureKind = AkgfGestureKind.Sequence;
                if (PassesDirectCandidateGate(sequenceMatch))
                {
                    EnrichFromSettings(ref sequenceMatch);
                    if (!hasPendingSequence || IsHigherPriority(sequenceMatch, pendingSequence))
                    {
                        pendingSequence = sequenceMatch;
                        hasPendingSequence = true;
                    }
                }
            }
        }

        private bool PassesDirectCandidateGate(AkgfGestureMatchResult match)
        {
            if (!match.isValid || string.IsNullOrWhiteSpace(match.gestureName))
            {
                return false;
            }

            float threshold = match.gestureKind == AkgfGestureKind.Sequence
                ? directSequenceMinimumSimilarity
                : directStaticMinimumSimilarity;

            if (useRecognizerThresholdForDirectCandidates)
            {
                if (match.gestureKind == AkgfGestureKind.Sequence && sequenceRecognizer != null)
                {
                    threshold = Mathf.Max(threshold, sequenceRecognizer.minimumSimilarity);
                }
                else if (match.gestureKind == AkgfGestureKind.StaticPose && staticPoseRecognizer != null)
                {
                    threshold = Mathf.Max(threshold, staticPoseRecognizer.minimumSimilarity);
                }
            }

            if (usePerGestureThresholdForDirectCandidates && gestureSettingsDatabase != null)
            {
                AkgfGestureSettings settings = gestureSettingsDatabase.GetSettings(match.gestureName, match.gestureKind);
                if (settings != null)
                {
                    threshold = Mathf.Max(threshold, settings.minimumSimilarity);
                }
            }

            if (match.similarity < Mathf.Clamp01(threshold))
            {
                LastDecision = $"candidate below coordinator threshold: {match.gestureName} {AkgfGestureMatcher.FormatSimilarityPercent(match.similarity)} < {AkgfGestureMatcher.FormatSimilarityPercent(threshold)}";
                return false;
            }

            return true;
        }

        private void ResolvePendingCandidates()
        {
            if (sequenceHasPriority && hasPendingSequence)
            {
                TryAccept(pendingSequence);
                return;
            }

            if (hasPendingStatic && Time.time >= staticBlockedUntil)
            {
                if (TryAccept(pendingStatic))
                {
                    return;
                }
            }

            if (hasPendingSequence)
            {
                TryAccept(pendingSequence);
            }
        }

        private bool TryAccept(AkgfGestureMatchResult match)
        {
            if (!match.isValid || string.IsNullOrWhiteSpace(match.gestureName))
            {
                LastDecision = "candidate invalid or missing gesture name";
                return false;
            }

            AkgfGestureSettings settings = gestureSettingsDatabase != null
                ? gestureSettingsDatabase.GetSettings(match.gestureName, match.gestureKind)
                : null;

            if (settings != null)
            {
                if (!settings.enabled)
                {
                    LastDecision = $"blocked: settings disabled for {match.gestureName}";
                    return false;
                }

                if (groupController != null && !groupController.IsGroupActive(settings.groupName))
                {
                    LastDecision = $"blocked: group '{settings.groupName}' is not active";
                    return false;
                }
            }

            if (Time.time - lastAnyFireTime < Mathf.Max(0f, globalCooldownSeconds))
            {
                LastDecision = $"blocked: global cooldown {globalCooldownSeconds:0.00}s";
                return false;
            }

            string key = NormalizeKey(match.gestureName);
            float cooldown = settings != null ? settings.cooldownSeconds : sameGestureCooldownSeconds;
            if (lastFireByGesture.TryGetValue(key, out float lastTime) &&
                Time.time - lastTime < Mathf.Max(0f, cooldown))
            {
                LastDecision = $"blocked: same gesture cooldown {cooldown:0.00}s";
                return false;
            }

            bool isSameAsActive = IsActive(match);
            lastAnyFireTime = Time.time;
            lastFireByGesture[key] = Time.time;
            LastOutput = match;

            if (match.gestureKind == AkgfGestureKind.Sequence)
            {
                staticBlockedUntil = Time.time + Mathf.Max(0f, sequenceBlocksStaticSeconds);
            }

            if (!isSameAsActive)
            {
                if (!string.IsNullOrWhiteSpace(ActiveGestureName) && emitExitPhase)
                {
                    Emit(activeMatch, AkgfGesturePhase.Exit);
                }

                ActiveGestureName = match.gestureName;
                ActiveGestureKind = match.gestureKind;
                activeMatch = match;
                lastActiveRefreshTime = Time.time;

                if (emitEnterPhase && ShouldEmit(settings, AkgfGesturePhase.Enter))
                {
                    Emit(match, AkgfGesturePhase.Enter);
                }
            }
            else
            {
                activeMatch = match;
                lastActiveRefreshTime = Time.time;
                if (emitStayPhase && ShouldEmit(settings, AkgfGesturePhase.Stay))
                {
                    Emit(match, AkgfGesturePhase.Stay);
                }
            }

            if (emitDetectedPhase)
            {
                Emit(match, AkgfGesturePhase.Detected);
            }

            if (emitConfirmedPhase && ShouldEmit(settings, AkgfGesturePhase.Confirmed))
            {
                Emit(match, AkgfGesturePhase.Confirmed);
            }

            LastDecision = $"accepted: {match.gestureName} {match.gestureKind} {AkgfGestureMatcher.FormatSimilarityPercent(match.similarity)}";
            return true;
        }

        private bool DebugForceEmitCurrentCandidate()
        {
            if (Time.time - lastDebugForceEmitTime < Mathf.Max(0.01f, debugForceEmitIntervalSeconds))
            {
                return false;
            }

            AkgfGestureMatchResult candidate;
            if (!TryGetBestAvailableCandidate(out candidate))
            {
                LastDecision = "force emit enabled, but no valid candidate exists";
                return false;
            }

            EnrichFromSettings(ref candidate);
            candidate.phase = AkgfGesturePhase.Detected;
            ActiveGestureName = candidate.gestureName;
            ActiveGestureKind = candidate.gestureKind;
            activeMatch = candidate;
            lastActiveRefreshTime = Time.time;
            lastAnyFireTime = Time.time;
            lastFireByGesture[NormalizeKey(candidate.gestureName)] = Time.time;
            lastDebugForceEmitTime = Time.time;
            LastOutput = candidate;
            LastDecision = $"FORCED RESULT: {candidate.gestureName} {candidate.gestureKind} {AkgfGestureMatcher.FormatSimilarityPercent(candidate.similarity)}";
            Emit(candidate, AkgfGesturePhase.Detected);
            return true;
        }

        private bool TryGetBestAvailableCandidate(out AkgfGestureMatchResult candidate)
        {
            candidate = AkgfGestureMatchResult.None;

            if (hasPendingSequence && pendingSequence.isValid)
            {
                candidate = pendingSequence;
                return true;
            }

            if (hasPendingStatic && pendingStatic.isValid)
            {
                candidate = pendingStatic;
                return true;
            }

            AkgfGestureMatchResult sequenceMatch = sequenceRecognizer != null ? sequenceRecognizer.LastMatch : AkgfGestureMatchResult.None;
            AkgfGestureMatchResult staticMatch = staticPoseRecognizer != null ? staticPoseRecognizer.LastMatch : AkgfGestureMatchResult.None;

            if (sequenceHasPriority && sequenceMatch.isValid)
            {
                sequenceMatch.gestureKind = AkgfGestureKind.Sequence;
                candidate = sequenceMatch;
                return true;
            }

            if (staticMatch.isValid)
            {
                staticMatch.gestureKind = AkgfGestureKind.StaticPose;
                candidate = staticMatch;
                return true;
            }

            if (sequenceMatch.isValid)
            {
                sequenceMatch.gestureKind = AkgfGestureKind.Sequence;
                candidate = sequenceMatch;
                return true;
            }

            return false;
        }

        private void MaybeEmitExitForLostGesture()
        {
            if (!emitExitPhase || string.IsNullOrWhiteSpace(ActiveGestureName))
            {
                return;
            }

            if (Time.time - lastActiveRefreshTime < Mathf.Max(0.05f, exitAfterMissingSeconds))
            {
                return;
            }

            Emit(activeMatch, AkgfGesturePhase.Exit);
            ActiveGestureName = string.Empty;
            activeMatch = AkgfGestureMatchResult.None;
        }

        private void Emit(AkgfGestureMatchResult match, AkgfGesturePhase phase)
        {
            if (!match.isValid)
            {
                return;
            }

            match.phase = phase;
            GestureRecognized?.Invoke(match);
            GesturePhase?.Invoke(match);
            onGestureRecognized?.Invoke(match.gestureName, match.similarity);
            onGestureRecognizedDetailed?.Invoke(match.gestureName, match.similarity, match.gestureKind);
            onGesturePhaseDetailed?.Invoke(match.gestureName, match.similarity, match.gestureKind, phase);
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

        private bool IsActive(AkgfGestureMatchResult match)
        {
            return string.Equals(NormalizeKey(ActiveGestureName), NormalizeKey(match.gestureName), StringComparison.Ordinal) && ActiveGestureKind == match.gestureKind;
        }

        private void EnrichFromSettings(ref AkgfGestureMatchResult match)
        {
            AkgfGestureSettings settings = gestureSettingsDatabase != null
                ? gestureSettingsDatabase.GetSettings(match.gestureName, match.gestureKind)
                : null;

            if (settings == null)
            {
                return;
            }

            match.groupName = settings.groupName;
            match.priority = settings.priority;
        }

        private bool IsHigherPriority(AkgfGestureMatchResult a, AkgfGestureMatchResult b)
        {
            if (a.priority != b.priority)
            {
                return a.priority > b.priority;
            }

            return a.similarity > b.similarity;
        }

        private void ClearPendingCandidates()
        {
            hasPendingStatic = false;
            hasPendingSequence = false;
            pendingStatic = AkgfGestureMatchResult.None;
            pendingSequence = AkgfGestureMatchResult.None;
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
    }
}
