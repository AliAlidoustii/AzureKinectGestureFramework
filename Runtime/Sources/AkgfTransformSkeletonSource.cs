using UnityEngine;

namespace AzureKinectGestureFramework
{
    public enum AkgfTransformReadMode
    {
        LocalPosition,
        WorldPosition
    }

    /// <summary>
    /// Reads an already-rendered skeleton hierarchy. This is the easiest integration path with the Microsoft Unity sample:
    /// assign the transform that has 32 joint children in JointId order, or assign the parent and use AutoAssignJointsFromRoot().
    /// </summary>
    public sealed class AkgfTransformSkeletonSource : MonoBehaviour, IAkgfSkeletonSource
    {
        public Transform jointRoot;
        public Transform[] jointTransforms = new Transform[AkgfJointIdExtensions.JointCount];
        public AkgfTransformReadMode readMode = AkgfTransformReadMode.LocalPosition;
        public bool autoAssignOnAwake = true;
        public bool searchOneLevelDownFor32Joints = true;
        [Range(0f, 1f)] public float confidenceWhenTransformExists = 1f;

        private readonly AkgfTrackedBody currentBody = new AkgfTrackedBody();

        private void Awake()
        {
            EnsureJointArray();
            if (autoAssignOnAwake)
            {
                AutoAssignJointsFromRoot();
            }
        }

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            EnsureJointArray();
            currentBody.Clear();

            if (!HasEnoughAssignedJoints())
            {
                body = null;
                return false;
            }

            currentBody.BeginFrame(0, Time.time);
            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                Transform t = jointTransforms[i];
                Vector3 position = t == null ? Vector3.zero : (readMode == AkgfTransformReadMode.LocalPosition ? t.localPosition : t.position);
                float confidence = t == null ? 0f : confidenceWhenTransformExists;
                currentBody.SetJoint((AkgfJointId)i, position, confidence);
            }

            body = currentBody;
            return true;
        }

        [ContextMenu("Auto Assign Joints From Root")]
        public void AutoAssignJointsFromRoot()
        {
            EnsureJointArray();

            if (jointRoot == null)
            {
                return;
            }

            Transform candidate = jointRoot;
            if (candidate.childCount < AkgfJointIdExtensions.JointCount && searchOneLevelDownFor32Joints)
            {
                for (int i = 0; i < jointRoot.childCount; i++)
                {
                    Transform child = jointRoot.GetChild(i);
                    if (child.childCount >= AkgfJointIdExtensions.JointCount)
                    {
                        candidate = child;
                        break;
                    }
                }
            }

            if (candidate.childCount < AkgfJointIdExtensions.JointCount)
            {
                Debug.LogWarning($"Could not auto-assign joints. '{candidate.name}' has {candidate.childCount} children, but 32 are required.", this);
                return;
            }

            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                jointTransforms[i] = candidate.GetChild(i);
            }

            jointRoot = candidate;
        }

        private bool HasEnoughAssignedJoints()
        {
            if (jointTransforms == null || jointTransforms.Length < AkgfJointIdExtensions.JointCount)
            {
                return false;
            }

            // Only require stable normalization joints here. Other missing joints can get zero confidence.
            return jointTransforms[(int)AkgfJointId.Pelvis] != null &&
                   jointTransforms[(int)AkgfJointId.SpineChest] != null &&
                   jointTransforms[(int)AkgfJointId.Neck] != null &&
                   jointTransforms[(int)AkgfJointId.ShoulderLeft] != null &&
                   jointTransforms[(int)AkgfJointId.ShoulderRight] != null;
        }

        private void EnsureJointArray()
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
}
