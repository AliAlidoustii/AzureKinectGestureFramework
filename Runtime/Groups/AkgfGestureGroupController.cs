using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfGestureGroupState
    {
        public string groupName = "Default";
        public bool active = true;
    }

    public sealed class AkgfGestureGroupController : MonoBehaviour
    {
        public bool allowUngroupedGestures = true;
        public bool defaultStateForUnknownGroups = true;
        public List<AkgfGestureGroupState> groups = new List<AkgfGestureGroupState>
        {
            new AkgfGestureGroupState { groupName = "Default", active = true }
        };

        public bool IsGroupActive(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return allowUngroupedGestures;
            }

            if (groups == null)
            {
                return defaultStateForUnknownGroups;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                AkgfGestureGroupState state = groups[i];
                if (state == null || string.IsNullOrWhiteSpace(state.groupName))
                {
                    continue;
                }

                if (string.Equals(state.groupName.Trim(), groupName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return state.active;
                }
            }

            return defaultStateForUnknownGroups;
        }

        public void SetGroupActive(string groupName, bool active)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return;
            }

            if (groups == null)
            {
                groups = new List<AkgfGestureGroupState>();
            }

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null && string.Equals(groups[i].groupName, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    groups[i].active = active;
                    return;
                }
            }

            groups.Add(new AkgfGestureGroupState { groupName = groupName.Trim(), active = active });
        }

        public void ActivateOnly(string groupName)
        {
            if (groups == null)
            {
                groups = new List<AkgfGestureGroupState>();
            }

            bool found = false;
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] == null)
                {
                    continue;
                }

                bool isTarget = string.Equals(groups[i].groupName, groupName, StringComparison.OrdinalIgnoreCase);
                groups[i].active = isTarget;
                found |= isTarget;
            }

            if (!found && !string.IsNullOrWhiteSpace(groupName))
            {
                groups.Add(new AkgfGestureGroupState { groupName = groupName.Trim(), active = true });
            }
        }
    }
}
