using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public sealed class AkgfAutoAvatarPreviewDriver : MonoBehaviour
    {
        [Header("Avatar Model")]
        [Tooltip("Drag your rigged humanoid model prefab or FBX model here.")]
        [SerializeField] private GameObject avatarModel;

        [Tooltip("If true, the avatar model is generated automatically at runtime.")]
        [SerializeField] private bool autoCreateAvatar = true;

        [Tooltip("Optional parent for the generated avatar. If empty, it will be created under this GameObject.")]
        [SerializeField] private Transform avatarParent;

        [Tooltip("Destroy the old generated preview when creating a new one.")]
        [SerializeField] private bool destroyOldGeneratedAvatar = true;

        [Header("AKGF Source")]
        [Tooltip("Optional. If empty, the script tries to find AKGF_KinectTrackerHandler automatically.")]
        [SerializeField] private MonoBehaviour skeletonSourceBehaviour;

        private IAkgfSkeletonSource skeletonSource;

        [Header("Position")]
        [SerializeField] private bool moveAvatarRoot = true;
        [SerializeField] private float positionScale = 1.0f;
        [SerializeField] private Vector3 positionOffset = Vector3.zero;

        [Tooltip("Extra rotation offset for the whole avatar. Use this if the model faces the wrong direction.")]
        [SerializeField] private Vector3 avatarRotationOffsetEuler = Vector3.zero;

        [Tooltip("Extra scale for generated avatar.")]
        [SerializeField] private Vector3 avatarScale = Vector3.one;

        [Header("Driving")]
        [SerializeField] private bool driveHead = true;
        [SerializeField] private bool driveNeck = true;
        [SerializeField] private bool driveSpine = false;
        [SerializeField] private bool driveShoulders = true;
        [SerializeField] private bool driveArms = true;
        [SerializeField] private bool driveHands = false;

        [Header("Smoothing")]
        [SerializeField] private float positionSmoothing = 12f;
        [SerializeField] private float rotationSmoothing = 14f;

        [Header("Joint Confidence")]
        [SerializeField] private float minimumJointConfidence = 0.01f;

        [Header("Debug")]
        [SerializeField] private bool logSetup = true;
        [SerializeField] private bool logIfNoBody = false;
        [SerializeField] private bool showGeneratedAvatarName = true;

        private GameObject generatedAvatar;
        private Animator animator;

        private Transform hips;
        private Transform spine;
        private Transform chest;
        private Transform upperChest;
        private Transform neck;
        private Transform head;

        private Transform leftShoulder;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform leftHand;

        private Transform rightShoulder;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightHand;

        private readonly List<BoneAim> boneAims = new List<BoneAim>();

        private Vector3 rootVelocity;

        private void Reset()
        {
            TryAutoAssignSource();
        }

        private void Awake()
        {
            ResolveSource();

            if (autoCreateAvatar)
            {
                CreateAvatarPreview();
            }
        }

        private void Start()
        {
            if (generatedAvatar == null && avatarModel != null)
            {
                CreateAvatarPreview();
            }
        }

        private void OnValidate()
        {
            positionSmoothing = Mathf.Max(0.01f, positionSmoothing);
            rotationSmoothing = Mathf.Max(0.01f, rotationSmoothing);
            minimumJointConfidence = Mathf.Clamp01(minimumJointConfidence);
        }

        [ContextMenu("Create Avatar Preview Now")]
        public void CreateAvatarPreview()
        {
            if (avatarModel == null)
            {
                Debug.LogWarning("[AKGF Avatar Preview] No avatar model assigned.");
                return;
            }

            if (generatedAvatar != null && destroyOldGeneratedAvatar)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedAvatar);
                }
                else
                {
                    DestroyImmediate(generatedAvatar);
                }
            }

            Transform parent = avatarParent != null ? avatarParent : transform;

            generatedAvatar = Instantiate(avatarModel, parent);
            generatedAvatar.name = showGeneratedAvatarName
                ? $"AKGF_GeneratedAvatarPreview_{avatarModel.name}"
                : avatarModel.name;

            generatedAvatar.transform.localPosition = Vector3.zero;
            generatedAvatar.transform.localRotation = Quaternion.Euler(avatarRotationOffsetEuler);
            generatedAvatar.transform.localScale = avatarScale;

            animator = generatedAvatar.GetComponentInChildren<Animator>();

            if (animator == null)
            {
                Debug.LogError("[AKGF Avatar Preview] Generated avatar has no Animator.");
                return;
            }

            if (!animator.isHuman)
            {
                Debug.LogError(
                    "[AKGF Avatar Preview] Avatar is not Humanoid. " +
                    "Select the model asset and set Rig > Animation Type = Humanoid."
                );
                return;
            }

            CacheBones();
            BuildBoneAims();

            if (logSetup)
            {
                Debug.Log(
                    "[AKGF Avatar Preview] Avatar generated and calibrated.\n" +
                    $"Avatar: {generatedAvatar.name}\n" +
                    $"Bone aims: {boneAims.Count}"
                );
            }
        }

        [ContextMenu("Destroy Generated Avatar")]
        public void DestroyGeneratedAvatar()
        {
            if (generatedAvatar == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedAvatar);
            }
            else
            {
                DestroyImmediate(generatedAvatar);
            }

            generatedAvatar = null;
            animator = null;
            boneAims.Clear();
        }

        private void LateUpdate()
        {
            if (skeletonSource == null)
            {
                ResolveSource();
            }

            if (skeletonSource == null)
            {
                return;
            }

            if (generatedAvatar == null || animator == null)
            {
                return;
            }

            if (!skeletonSource.TryGetBody(out AkgfTrackedBody body))
            {
                if (logIfNoBody)
                {
                    Debug.Log("[AKGF Avatar Preview] No tracked body found.");
                }

                return;
            }

            ApplyBody(body);
        }

        private void ResolveSource()
        {
            skeletonSource = null;

            if (skeletonSourceBehaviour != null)
            {
                skeletonSource = skeletonSourceBehaviour as IAkgfSkeletonSource;

                if (skeletonSource != null)
                {
                    return;
                }
            }

            TryAutoAssignSource();

            if (skeletonSourceBehaviour != null)
            {
                skeletonSource = skeletonSourceBehaviour as IAkgfSkeletonSource;
            }

            if (skeletonSource == null && logSetup)
            {
                Debug.LogWarning(
                    "[AKGF Avatar Preview] Could not find IAkgfSkeletonSource. " +
                    "Put this script on AKGF_KinectTrackerHandler or assign AKGF_KinectTrackerHandler manually."
                );
            }
        }

        private void TryAutoAssignSource()
        {
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IAkgfSkeletonSource)
                {
                    skeletonSourceBehaviour = behaviour;
                    return;
                }
            }
        }

        private void CacheBones()
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);

            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private void BuildBoneAims()
        {
            boneAims.Clear();

            if (driveSpine)
            {
                AddBoneAim(spine, hips, spine, AkgfJointId.Pelvis, AkgfJointId.SpineNavel, "Spine");
                AddBoneAim(chest, spine, chest, AkgfJointId.SpineNavel, AkgfJointId.SpineChest, "Chest");
                AddBoneAim(upperChest, spine, upperChest, AkgfJointId.SpineNavel, AkgfJointId.SpineChest, "UpperChest");
            }

            if (driveNeck)
            {
                AddBoneAim(neck, chest != null ? chest : spine, neck, AkgfJointId.SpineChest, AkgfJointId.Neck, "Neck");
            }

            if (driveHead)
            {
                AddBoneAim(head, neck, head, AkgfJointId.Neck, AkgfJointId.Head, "Head");
            }

            if (driveShoulders)
            {
                AddBoneAim(leftShoulder, chest != null ? chest : spine, leftShoulder, AkgfJointId.SpineChest, AkgfJointId.ShoulderLeft, "LeftShoulder");
                AddBoneAim(rightShoulder, chest != null ? chest : spine, rightShoulder, AkgfJointId.SpineChest, AkgfJointId.ShoulderRight, "RightShoulder");
            }

            if (driveArms)
            {
                AddBoneAim(leftUpperArm, leftShoulder, leftUpperArm, AkgfJointId.ShoulderLeft, AkgfJointId.ElbowLeft, "LeftUpperArm");
                AddBoneAim(leftLowerArm, leftUpperArm, leftLowerArm, AkgfJointId.ElbowLeft, AkgfJointId.WristLeft, "LeftLowerArm");

                AddBoneAim(rightUpperArm, rightShoulder, rightUpperArm, AkgfJointId.ShoulderRight, AkgfJointId.ElbowRight, "RightUpperArm");
                AddBoneAim(rightLowerArm, rightUpperArm, rightLowerArm, AkgfJointId.ElbowRight, AkgfJointId.WristRight, "RightLowerArm");
            }

            if (driveHands)
            {
                AddBoneAim(leftHand, leftLowerArm, leftHand, AkgfJointId.WristLeft, AkgfJointId.HandLeft, "LeftHand");
                AddBoneAim(rightHand, rightLowerArm, rightHand, AkgfJointId.WristRight, AkgfJointId.HandRight, "RightHand");
            }
        }

        private void AddBoneAim(
            Transform bone,
            Transform restFrom,
            Transform restTo,
            AkgfJointId sourceJoint,
            AkgfJointId targetJoint,
            string label)
        {
            if (bone == null || restFrom == null || restTo == null)
            {
                if (logSetup)
                {
                    Debug.LogWarning($"[AKGF Avatar Preview] Skipped missing bone: {label}");
                }

                return;
            }

            Vector3 restDirection = restTo.position - restFrom.position;

            if (restDirection.sqrMagnitude < 0.000001f)
            {
                if (logSetup)
                {
                    Debug.LogWarning($"[AKGF Avatar Preview] Invalid rest direction for: {label}");
                }

                return;
            }

            boneAims.Add(new BoneAim
            {
                label = label,
                bone = bone,
                sourceJoint = sourceJoint,
                targetJoint = targetJoint,
                restDirectionWorld = restDirection.normalized,
                restRotationWorld = bone.rotation
            });
        }

        private void ApplyBody(AkgfTrackedBody body)
        {
            if (moveAvatarRoot)
            {
                ApplyRootPosition(body);
            }

            ApplyBoneRotations(body);
        }

        private void ApplyRootPosition(AkgfTrackedBody body)
        {
            if (!TryGetJoint(body, AkgfJointId.Pelvis, out Vector3 pelvis))
            {
                return;
            }

            Vector3 targetPosition = pelvis * positionScale + positionOffset;

            generatedAvatar.transform.position = Vector3.SmoothDamp(
                generatedAvatar.transform.position,
                targetPosition,
                ref rootVelocity,
                1f / Mathf.Max(1f, positionSmoothing)
            );

            Quaternion targetRootRotation = Quaternion.Euler(avatarRotationOffsetEuler);

            generatedAvatar.transform.rotation = Quaternion.Slerp(
                generatedAvatar.transform.rotation,
                targetRootRotation,
                Time.deltaTime * positionSmoothing
            );
        }

        private void ApplyBoneRotations(AkgfTrackedBody body)
        {
            for (int i = 0; i < boneAims.Count; i++)
            {
                BoneAim aim = boneAims[i];

                if (aim.bone == null)
                {
                    continue;
                }

                if (!TryGetJoint(body, aim.sourceJoint, out Vector3 from))
                {
                    continue;
                }

                if (!TryGetJoint(body, aim.targetJoint, out Vector3 to))
                {
                    continue;
                }

                Vector3 targetDirection = to - from;

                if (targetDirection.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                targetDirection.Normalize();

                Quaternion delta = Quaternion.FromToRotation(aim.restDirectionWorld, targetDirection);
                Quaternion targetRotation = delta * aim.restRotationWorld;

                aim.bone.rotation = Quaternion.Slerp(
                    aim.bone.rotation,
                    targetRotation,
                    Time.deltaTime * rotationSmoothing
                );
            }
        }

        private bool TryGetJoint(AkgfTrackedBody body, AkgfJointId jointId, out Vector3 position)
        {
            position = Vector3.zero;

            if (body == null || !body.IsTracked)
            {
                return false;
            }

            if (!body.HasJoint(jointId, minimumJointConfidence))
            {
                return false;
            }

            position = body.GetJoint(jointId);
            return true;
        }

        [System.Serializable]
        private struct BoneAim
        {
            public string label;
            public Transform bone;
            public AkgfJointId sourceJoint;
            public AkgfJointId targetJoint;
            public Vector3 restDirectionWorld;
            public Quaternion restRotationWorld;
        }
    }
}