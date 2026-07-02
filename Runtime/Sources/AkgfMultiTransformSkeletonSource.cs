using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfTransformBodySlot
    {
        public string displayName = "User";
        public int bodyId;
        public Transform jointRoot;
        public Transform[] jointTransforms = new Transform[AkgfJointIdExtensions.JointCount];
        public bool enabled = true;

        public void EnsureJointArray()
        {
            if (jointTransforms == null || jointTransforms.Length != AkgfJointIdExtensions.JointCount)
            {
                Transform[] newArray = new Transform[AkgfJointIdExtensions.JointCount];
                if (jointTransforms != null)
                {
                    for (int i = 0; i < Mathf.Min(jointTransforms.Length, newArray.Length); i++)
                    {
                        newArray[i] = jointTransforms[i];
                    }
                }

                jointTransforms = newArray;
            }
        }
    }

    /// <summary>
    /// Reads several already-rendered skeleton transform hierarchies.
    /// This is useful for testing multi-user mode without writing SDK bridge code first.
    /// </summary>
    public sealed class AkgfMultiTransformSkeletonSource : MonoBehaviour, IAkgfMultiSkeletonSource, IAkgfSkeletonSource
    {
        public List<AkgfTransformBodySlot> bodySlots = new List<AkgfTransformBodySlot>();
        public AkgfTransformReadMode readMode = AkgfTransformReadMode.LocalPosition;
        public bool autoAssignOnAwake = true;
        public bool searchOneLevelDownFor32Joints = true;
        [Range(0f, 1f)] public float confidenceWhenTransformExists = 1f;

        private readonly List<AkgfTrackedBody> currentBodies = new List<AkgfTrackedBody>(8);

        private void Awake()
        {
            EnsureSlots();
            if (autoAssignOnAwake)
            {
                AutoAssignAllFromRoots();
            }
        }

        public void GetTrackedBodies(List<AkgfTrackedBody> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            BuildCurrentBodies();
            for (int i = 0; i < currentBodies.Count; i++)
            {
                results.Add(currentBodies[i]);
            }
        }

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            BuildCurrentBodies();
            return AkgfSkeletonSourceUtility.TryGetClosestBody(currentBodies, out body);
        }

        [ContextMenu("Auto Assign All Joints From Roots")]
        public void AutoAssignAllFromRoots()
        {
            EnsureSlots();
            for (int i = 0; i < bodySlots.Count; i++)
            {
                AutoAssignSlot(bodySlots[i]);
            }
        }

        private void BuildCurrentBodies()
        {
            currentBodies.Clear();
            EnsureSlots();

            for (int i = 0; i < bodySlots.Count; i++)
            {
                AkgfTransformBodySlot slot = bodySlots[i];
                if (slot == null || !slot.enabled)
                {
                    continue;
                }

                slot.EnsureJointArray();
                if (!HasRequiredTransforms(slot))
                {
                    continue;
                }

                AkgfTrackedBody body = new AkgfTrackedBody();
                body.BeginFrame(slot.bodyId, Time.time);
                for (int j = 0; j < AkgfJointIdExtensions.JointCount; j++)
                {
                    Transform t = slot.jointTransforms[j];
                    Vector3 position = t == null ? Vector3.zero : (readMode == AkgfTransformReadMode.LocalPosition ? t.localPosition : t.position);
                    float confidence = t == null ? 0f : confidenceWhenTransformExists;
                    body.SetJoint((AkgfJointId)j, position, confidence);
                }

                currentBodies.Add(body);
            }
        }

        private void EnsureSlots()
        {
            if (bodySlots == null)
            {
                bodySlots = new List<AkgfTransformBodySlot>();
            }

            for (int i = 0; i < bodySlots.Count; i++)
            {
                if (bodySlots[i] == null)
                {
                    bodySlots[i] = new AkgfTransformBodySlot { bodyId = i };
                }

                bodySlots[i].EnsureJointArray();
            }
        }

        private bool HasRequiredTransforms(AkgfTransformBodySlot slot)
        {
            return slot.jointTransforms != null &&
                   slot.jointTransforms.Length >= AkgfJointIdExtensions.JointCount &&
                   slot.jointTransforms[(int)AkgfJointId.Pelvis] != null &&
                   slot.jointTransforms[(int)AkgfJointId.SpineChest] != null &&
                   slot.jointTransforms[(int)AkgfJointId.Neck] != null &&
                   slot.jointTransforms[(int)AkgfJointId.ShoulderLeft] != null &&
                   slot.jointTransforms[(int)AkgfJointId.ShoulderRight] != null;
        }

        private void AutoAssignSlot(AkgfTransformBodySlot slot)
        {
            if (slot == null)
            {
                return;
            }

            slot.EnsureJointArray();
            if (slot.jointRoot == null)
            {
                return;
            }

            Transform candidate = slot.jointRoot;
            if (candidate.childCount < AkgfJointIdExtensions.JointCount && searchOneLevelDownFor32Joints)
            {
                for (int i = 0; i < slot.jointRoot.childCount; i++)
                {
                    Transform child = slot.jointRoot.GetChild(i);
                    if (child.childCount >= AkgfJointIdExtensions.JointCount)
                    {
                        candidate = child;
                        break;
                    }
                }
            }

            if (candidate.childCount < AkgfJointIdExtensions.JointCount)
            {
                Debug.LogWarning($"Could not auto-assign joints for slot '{slot.displayName}'. '{candidate.name}' has {candidate.childCount} children, but 32 are required.", this);
                return;
            }

            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                slot.jointTransforms[i] = candidate.GetChild(i);
            }

            slot.jointRoot = candidate;
        }
    }
}
