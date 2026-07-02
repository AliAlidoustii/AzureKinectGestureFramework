using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Kinect-free demo source. It generates one or two simple synthetic skeletons so the UI, logger,
    /// profiler, and multi-user routing can be tested before connecting Azure Kinect.
    /// Use this only for samples; replace it with the official Kinect source in production.
    /// </summary>
    public sealed class AkgfDemoSimulatedMultiUserSource : MonoBehaviour, IAkgfMultiSkeletonSource, IAkgfSkeletonSource
    {
        public bool simulateTwoUsers = true;
        public float userSpacing = 1.1f;
        public float animationSpeed = 1f;
        public bool simulateCrossArms = true;
        public bool simulateWave = true;
        public bool simulateHeadUpDown = true;

        private readonly List<AkgfTrackedBody> bodies = new List<AkgfTrackedBody>(2);

        private void Awake()
        {
            EnsureBodies();
        }

        public void GetTrackedBodies(List<AkgfTrackedBody> results)
        {
            if (results == null)
            {
                return;
            }

            EnsureBodies();
            UpdateSyntheticBodies();
            results.Clear();
            for (int i = 0; i < bodies.Count; i++)
            {
                results.Add(bodies[i]);
            }
        }

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            EnsureBodies();
            UpdateSyntheticBodies();
            body = bodies.Count > 0 ? bodies[0] : null;
            return body != null && body.IsTracked;
        }

        private void EnsureBodies()
        {
            int needed = simulateTwoUsers ? 2 : 1;
            while (bodies.Count < needed)
            {
                bodies.Add(new AkgfTrackedBody());
            }
            while (bodies.Count > needed)
            {
                bodies.RemoveAt(bodies.Count - 1);
            }
        }

        private void UpdateSyntheticBodies()
        {
            float t = Time.time * Mathf.Max(0.05f, animationSpeed);
            for (int i = 0; i < bodies.Count; i++)
            {
                Vector3 offset = new Vector3((i - (bodies.Count - 1) * 0.5f) * userSpacing, 0f, 2.2f + i * 0.15f);
                PoseKind pose = GetPoseKind(t + i * 1.7f);
                FillBody(bodies[i], i + 1, offset, pose, t + i);
            }
        }

        private PoseKind GetPoseKind(float t)
        {
            float phase = Mathf.Repeat(t, 8f);
            if (simulateCrossArms && phase < 2f)
            {
                return PoseKind.CrossArms;
            }
            if (simulateWave && phase < 4f)
            {
                return PoseKind.Wave;
            }
            if (simulateHeadUpDown && phase < 6f)
            {
                return PoseKind.HeadUp;
            }
            return PoseKind.Neutral;
        }

        private static void FillBody(AkgfTrackedBody body, int bodyId, Vector3 root, PoseKind pose, float t)
        {
            body.Clear();
            body.BeginFrame(bodyId, Time.time);

            Vector3 pelvis = root + new Vector3(0f, 1.0f, 0f);
            Vector3 spineNavel = pelvis + new Vector3(0f, 0.20f, 0f);
            Vector3 spineChest = pelvis + new Vector3(0f, 0.52f, 0f);
            Vector3 neck = pelvis + new Vector3(0f, 0.78f, 0f);
            Vector3 head = pelvis + new Vector3(0f, 1.00f, 0f);

            if (pose == PoseKind.HeadUp)
            {
                head += new Vector3(0f, 0.10f, -0.07f);
            }

            Vector3 shoulderL = spineChest + new Vector3(-0.23f, 0.12f, 0f);
            Vector3 shoulderR = spineChest + new Vector3(0.23f, 0.12f, 0f);
            Vector3 elbowL = shoulderL + new Vector3(-0.24f, -0.23f, 0f);
            Vector3 elbowR = shoulderR + new Vector3(0.24f, -0.23f, 0f);
            Vector3 wristL = elbowL + new Vector3(-0.22f, -0.20f, 0f);
            Vector3 wristR = elbowR + new Vector3(0.22f, -0.20f, 0f);

            if (pose == PoseKind.CrossArms)
            {
                elbowL = spineChest + new Vector3(-0.18f, 0.02f, -0.03f);
                elbowR = spineChest + new Vector3(0.18f, 0.02f, -0.03f);
                wristL = spineChest + new Vector3(0.27f, -0.03f, -0.04f);
                wristR = spineChest + new Vector3(-0.27f, -0.03f, -0.04f);
            }
            else if (pose == PoseKind.Wave)
            {
                float wave = Mathf.Sin(t * 8f) * 0.18f;
                elbowR = shoulderR + new Vector3(0.18f, 0.16f, 0f);
                wristR = elbowR + new Vector3(wave, 0.31f, 0f);
            }

            Vector3 hipL = pelvis + new Vector3(-0.14f, -0.05f, 0f);
            Vector3 hipR = pelvis + new Vector3(0.14f, -0.05f, 0f);
            Vector3 kneeL = hipL + new Vector3(0f, -0.46f, 0.02f);
            Vector3 kneeR = hipR + new Vector3(0f, -0.46f, 0.02f);
            Vector3 ankleL = kneeL + new Vector3(0f, -0.43f, -0.01f);
            Vector3 ankleR = kneeR + new Vector3(0f, -0.43f, -0.01f);

            Set(body, AkgfJointId.Pelvis, pelvis);
            Set(body, AkgfJointId.SpineNavel, spineNavel);
            Set(body, AkgfJointId.SpineChest, spineChest);
            Set(body, AkgfJointId.Neck, neck);
            Set(body, AkgfJointId.ClavicleLeft, Vector3.Lerp(neck, shoulderL, 0.45f));
            Set(body, AkgfJointId.ShoulderLeft, shoulderL);
            Set(body, AkgfJointId.ElbowLeft, elbowL);
            Set(body, AkgfJointId.WristLeft, wristL);
            Set(body, AkgfJointId.HandLeft, wristL + new Vector3(0f, -0.04f, 0f));
            Set(body, AkgfJointId.HandTipLeft, wristL + new Vector3(0f, -0.10f, 0f));
            Set(body, AkgfJointId.ThumbLeft, wristL + new Vector3(0.04f, -0.04f, 0f));
            Set(body, AkgfJointId.ClavicleRight, Vector3.Lerp(neck, shoulderR, 0.45f));
            Set(body, AkgfJointId.ShoulderRight, shoulderR);
            Set(body, AkgfJointId.ElbowRight, elbowR);
            Set(body, AkgfJointId.WristRight, wristR);
            Set(body, AkgfJointId.HandRight, wristR + new Vector3(0f, -0.04f, 0f));
            Set(body, AkgfJointId.HandTipRight, wristR + new Vector3(0f, -0.10f, 0f));
            Set(body, AkgfJointId.ThumbRight, wristR + new Vector3(-0.04f, -0.04f, 0f));
            Set(body, AkgfJointId.HipLeft, hipL);
            Set(body, AkgfJointId.KneeLeft, kneeL);
            Set(body, AkgfJointId.AnkleLeft, ankleL);
            Set(body, AkgfJointId.FootLeft, ankleL + new Vector3(0f, -0.04f, 0.16f));
            Set(body, AkgfJointId.HipRight, hipR);
            Set(body, AkgfJointId.KneeRight, kneeR);
            Set(body, AkgfJointId.AnkleRight, ankleR);
            Set(body, AkgfJointId.FootRight, ankleR + new Vector3(0f, -0.04f, 0.16f));
            Set(body, AkgfJointId.Head, head);
            Set(body, AkgfJointId.Nose, head + new Vector3(0f, 0f, -0.10f));
            Set(body, AkgfJointId.EyeLeft, head + new Vector3(-0.04f, 0.03f, -0.08f));
            Set(body, AkgfJointId.EarLeft, head + new Vector3(-0.09f, 0.01f, 0f));
            Set(body, AkgfJointId.EyeRight, head + new Vector3(0.04f, 0.03f, -0.08f));
            Set(body, AkgfJointId.EarRight, head + new Vector3(0.09f, 0.01f, 0f));
        }

        private static void Set(AkgfTrackedBody body, AkgfJointId jointId, Vector3 position)
        {
            body.SetJoint(jointId, position, 1f);
        }

        private enum PoseKind
        {
            Neutral,
            CrossArms,
            Wave,
            HeadUp
        }
    }
}
