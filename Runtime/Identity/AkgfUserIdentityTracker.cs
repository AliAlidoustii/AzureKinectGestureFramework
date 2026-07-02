using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfStableUserInfo
    {
        public int stableUserId;
        public int lastRawBodyId;
        public Vector3 lastPosition;
        public float firstSeenTime;
        public float lastSeenTime;
        public bool isVisible;
    }

    /// <summary>
    /// Keeps a more stable user ID when Azure Kinect temporarily loses and reassigns a raw body ID.
    /// It is not biometric identity; it only uses position/time continuity inside the current session.
    /// </summary>
    public sealed class AkgfUserIdentityTracker : MonoBehaviour
    {
        [Header("Matching")]
        public bool enabledTracking = true;
        [Tooltip("Maximum distance from a lost user's last position for a new raw body to inherit that stable user ID.")]
        public float maxReacquireDistanceMeters = 0.70f;
        [Tooltip("A lost user slot is kept for this long so a briefly lost body can reclaim the same stable ID.")]
        public float keepLostUsersSeconds = 2.0f;
        public int firstStableUserId = 1;

        [Header("Debug")]
        public bool showDebugLogs = false;

        private sealed class UserSlot
        {
            public int stableUserId;
            public int lastRawBodyId;
            public Vector3 lastPosition;
            public float firstSeenTime;
            public float lastSeenTime;
            public bool isVisible;
        }

        private int nextStableUserId;
        private readonly Dictionary<int, int> rawToStable = new Dictionary<int, int>();
        private readonly Dictionary<int, UserSlot> stableUsers = new Dictionary<int, UserSlot>();
        private readonly List<int> rawIdsToRemove = new List<int>();
        private readonly List<int> stableIdsToRemove = new List<int>();

        public int StableUserCount => stableUsers.Count;

        private void Awake()
        {
            ResetTracker();
        }

        public void ResetTracker()
        {
            nextStableUserId = Mathf.Max(1, firstStableUserId);
            rawToStable.Clear();
            stableUsers.Clear();
        }

        public int ResolveStableId(int rawBodyId, Vector3 bodyPosition, float now)
        {
            if (!enabledTracking)
            {
                return rawBodyId;
            }

            Prune(now);

            if (rawToStable.TryGetValue(rawBodyId, out int knownStableId) && stableUsers.TryGetValue(knownStableId, out UserSlot knownSlot))
            {
                UpdateSlot(knownSlot, rawBodyId, bodyPosition, now);
                return knownSlot.stableUserId;
            }

            UserSlot reacquired = FindBestLostSlot(bodyPosition, now);
            if (reacquired != null)
            {
                UpdateSlot(reacquired, rawBodyId, bodyPosition, now);
                rawToStable[rawBodyId] = reacquired.stableUserId;
                if (showDebugLogs)
                {
                    Debug.Log($"AKGF: raw body {rawBodyId} reacquired stable user {reacquired.stableUserId}.", this);
                }
                return reacquired.stableUserId;
            }

            UserSlot created = new UserSlot
            {
                stableUserId = nextStableUserId++,
                lastRawBodyId = rawBodyId,
                lastPosition = bodyPosition,
                firstSeenTime = now,
                lastSeenTime = now,
                isVisible = true
            };
            stableUsers.Add(created.stableUserId, created);
            rawToStable[rawBodyId] = created.stableUserId;

            if (showDebugLogs)
            {
                Debug.Log($"AKGF: raw body {rawBodyId} assigned new stable user {created.stableUserId}.", this);
            }

            return created.stableUserId;
        }

        public void MarkFrameRawIdsVisible(IReadOnlyList<int> visibleRawIds, float now)
        {
            if (!enabledTracking)
            {
                return;
            }

            foreach (KeyValuePair<int, UserSlot> pair in stableUsers)
            {
                pair.Value.isVisible = false;
            }

            if (visibleRawIds != null)
            {
                for (int i = 0; i < visibleRawIds.Count; i++)
                {
                    if (rawToStable.TryGetValue(visibleRawIds[i], out int stableId) && stableUsers.TryGetValue(stableId, out UserSlot slot))
                    {
                        slot.isVisible = true;
                        slot.lastSeenTime = now;
                    }
                }
            }

            Prune(now);
        }

        public bool TryGetStableUserInfo(int stableUserId, out AkgfStableUserInfo info)
        {
            info = null;
            if (!stableUsers.TryGetValue(stableUserId, out UserSlot slot))
            {
                return false;
            }

            info = new AkgfStableUserInfo
            {
                stableUserId = slot.stableUserId,
                lastRawBodyId = slot.lastRawBodyId,
                lastPosition = slot.lastPosition,
                firstSeenTime = slot.firstSeenTime,
                lastSeenTime = slot.lastSeenTime,
                isVisible = slot.isVisible
            };
            return true;
        }

        private UserSlot FindBestLostSlot(Vector3 bodyPosition, float now)
        {
            UserSlot best = null;
            float bestDistance = float.PositiveInfinity;
            float maxDistance = Mathf.Max(0.05f, maxReacquireDistanceMeters);

            foreach (KeyValuePair<int, UserSlot> pair in stableUsers)
            {
                UserSlot slot = pair.Value;
                if (slot.isVisible)
                {
                    continue;
                }

                if (now - slot.lastSeenTime > Mathf.Max(0.1f, keepLostUsersSeconds))
                {
                    continue;
                }

                float distance = Vector3.Distance(slot.lastPosition, bodyPosition);
                if (distance < bestDistance && distance <= maxDistance)
                {
                    best = slot;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void UpdateSlot(UserSlot slot, int rawBodyId, Vector3 bodyPosition, float now)
        {
            slot.lastRawBodyId = rawBodyId;
            slot.lastPosition = bodyPosition;
            slot.lastSeenTime = now;
            slot.isVisible = true;
            rawToStable[rawBodyId] = slot.stableUserId;
        }

        private void Prune(float now)
        {
            rawIdsToRemove.Clear();
            stableIdsToRemove.Clear();

            foreach (KeyValuePair<int, UserSlot> pair in stableUsers)
            {
                UserSlot slot = pair.Value;
                if (!slot.isVisible && now - slot.lastSeenTime > Mathf.Max(0.1f, keepLostUsersSeconds))
                {
                    stableIdsToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < stableIdsToRemove.Count; i++)
            {
                stableUsers.Remove(stableIdsToRemove[i]);
            }

            foreach (KeyValuePair<int, int> pair in rawToStable)
            {
                if (!stableUsers.ContainsKey(pair.Value))
                {
                    rawIdsToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < rawIdsToRemove.Count; i++)
            {
                rawToStable.Remove(rawIdsToRemove[i]);
            }
        }
    }
}
