using UnityEngine;

namespace AzureKinectGestureFramework
{
    public sealed class AkgfHumanoidAvatarPreviewDriver : MonoBehaviour
    {
        [Header("AKGF Source")]
        [SerializeField] private MonoBehaviour skeletonSourceBehaviour;

        private IAkgfSkeletonSource skeletonSource;

        [Header("Avatar")]
        [SerializeField] private Animator avatarAnimator;

        [Header("Movement")]
        [SerializeField] private bool moveRoot = true;
        [SerializeField] private bool rotateRootToFaceCamera = false;
        [SerializeField] private float positionScale = 1.0f;
        [SerializeField] private Vector3 positionOffset = Vector3.zero;
        [SerializeField] private float smoothing = 12f;

        [Header("Bone Driving")]
        [SerializeField] private bool driveHead = true;
        [SerializeField] private bool driveShoulders = true;
        [SerializeField] private bool driveArms = true;
        [SerializeField] private bool driveHands = true;

        [Header("Joint Confidence")]
        [SerializeField] private float minimumJointConfidence = 0.01f;

        [Header("Debug")]
        [SerializeField] private bool logIfNoBody = false;
        [SerializeField] private bool logMissingSource = false;

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

        private Vector3 rootVelocity;

        private void Reset()
        {
            avatarAnimator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            if (avatarAnimator == null)
            {
                avatarAnimator = GetComponentInChildren<Animator>();
            }

            ResolveSource();
            CacheBones();
        }

        private void OnValidate()
        {
            if (avatarAnimator == null)
            {
                avatarAnimator = GetComponentInChildren<Animator>();
            }

            minimumJointConfidence = Mathf.Clamp01(minimumJointConfidence);
            smoothing = Mathf.Max(0.01f, smoothing);
        }

        private void ResolveSource()
        {
            skeletonSource = null;

            if (skeletonSourceBehaviour == null)
            {
                return;
            }

            skeletonSource = skeletonSourceBehaviour as IAkgfSkeletonSource;

            if (skeletonSource == null && logMissingSource)
            {
                Debug.LogWarning(
                    "[AKGF Avatar Preview] Skeleton Source Behaviour does not implement IAkgfSkeletonSource. " +
                    "Assign AKGF_KinectTrackerHandler."
                );
            }
        }

        private void CacheBones()
        {
            if (avatarAnimator == null)
            {
                Debug.LogWarning("[AKGF Avatar Preview] Avatar Animator is missing.");
                return;
            }

            if (!avatarAnimator.isHuman)
            {
                Debug.LogWarning("[AKGF Avatar Preview] Animator is not Humanoid. Set Rig > Animation Type = Humanoid.");
                return;
            }

            hips = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
            spine = avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
            chest = avatarAnimator.GetBoneTransform(HumanBodyBones.Chest);
            upperChest = avatarAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            neck = avatarAnimator.GetBoneTransform(HumanBodyBones.Neck);
            head = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);

            leftShoulder = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            leftUpperArm = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftHand);

            rightShoulder = avatarAnimator.GetBoneTransform(HumanBodyBones.RightShoulder);
            rightUpperArm = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = avatarAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = avatarAnimator.GetBoneTransform(HumanBodyBones.RightHand);
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

            if (avatarAnimator == null || !avatarAnimator.isHuman)
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

        private void ApplyBody(AkgfTrackedBody body)
        {
            ApplyRoot(body);
            ApplyUpperBody(body);
        }

        private void ApplyRoot(AkgfTrackedBody body)
        {
            if (!moveRoot)
            {
                return;
            }

            if (!TryGetJoint(body, AkgfJointId.Pelvis, out Vector3 pelvis))
            {
                return;
            }

            Vector3 targetPosition = pelvis * positionScale + positionOffset;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref rootVelocity,
                1f / Mathf.Max(1f, smoothing)
            );

            if (rotateRootToFaceCamera)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.identity,
                    Time.deltaTime * smoothing
                );
            }
        }

        private void ApplyUpperBody(AkgfTrackedBody body)
        {
            if (driveHead)
            {
                AimBoneBetween(body, neck, AkgfJointId.SpineChest, AkgfJointId.Neck);
                AimBoneBetween(body, head, AkgfJointId.Neck, AkgfJointId.Head);
            }

            if (driveShoulders)
            {
                AimBoneBetween(body, spine, AkgfJointId.Pelvis, AkgfJointId.SpineNavel);
                AimBoneBetween(body, chest, AkgfJointId.SpineNavel, AkgfJointId.SpineChest);
                AimBoneBetween(body, upperChest, AkgfJointId.SpineNavel, AkgfJointId.SpineChest);

                AimBoneBetween(body, leftShoulder, AkgfJointId.SpineChest, AkgfJointId.ShoulderLeft);
                AimBoneBetween(body, rightShoulder, AkgfJointId.SpineChest, AkgfJointId.ShoulderRight);
            }

            if (driveArms)
            {
                AimBoneBetween(body, leftUpperArm, AkgfJointId.ShoulderLeft, AkgfJointId.ElbowLeft);
                AimBoneBetween(body, leftLowerArm, AkgfJointId.ElbowLeft, AkgfJointId.WristLeft);

                AimBoneBetween(body, rightUpperArm, AkgfJointId.ShoulderRight, AkgfJointId.ElbowRight);
                AimBoneBetween(body, rightLowerArm, AkgfJointId.ElbowRight, AkgfJointId.WristRight);
            }

            if (driveHands)
            {
                AimBoneBetween(body, leftHand, AkgfJointId.WristLeft, AkgfJointId.HandLeft);
                AimBoneBetween(body, rightHand, AkgfJointId.WristRight, AkgfJointId.HandRight);
            }
        }

        private void AimBoneBetween(
            AkgfTrackedBody body,
            Transform bone,
            AkgfJointId fromJoint,
            AkgfJointId toJoint)
        {
            if (bone == null)
            {
                return;
            }

            if (!TryGetJoint(body, fromJoint, out Vector3 from))
            {
                return;
            }

            if (!TryGetJoint(body, toJoint, out Vector3 to))
            {
                return;
            }

            Vector3 direction = to - from;

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            bone.rotation = Quaternion.Slerp(
                bone.rotation,
                targetRotation,
                Time.deltaTime * smoothing
            );
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
    }
}