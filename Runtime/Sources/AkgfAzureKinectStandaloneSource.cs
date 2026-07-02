#if AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using UnityEngine;
using K4ABody = Microsoft.Azure.Kinect.BodyTracking.Body;
using K4ASkeleton = Microsoft.Azure.Kinect.BodyTracking.Skeleton;
using K4AJoint = Microsoft.Azure.Kinect.BodyTracking.Joint;
using K4AJointId = Microsoft.Azure.Kinect.BodyTracking.JointId;
using K4AJointConfidenceLevel = Microsoft.Azure.Kinect.BodyTracking.JointConfidenceLevel;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Self-contained Azure Kinect DK sensor + body tracker source for AKGF.
    ///
    /// This component opens the Azure Kinect device directly, creates its own BodyTracking Tracker,
    /// copies every tracked body into AKGF's neutral AkgfTrackedBody format, and exposes both
    /// IAkgfSkeletonSource and IAkgfMultiSkeletonSource.
    ///
    /// IMPORTANT:
    /// - Enable scripting define symbol: AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
    /// - The official Microsoft.Azure.Kinect.Sensor and Microsoft.Azure.Kinect.BodyTracking assemblies
    ///   plus their native runtime DLLs must be present in the Unity project.
    /// - Do not run another Kinect tracker at the same time. Only one pipeline should own the device.
    /// </summary>
    public sealed class AkgfAzureKinectStandaloneSource : MonoBehaviour, IAkgfSkeletonSource, IAkgfMultiSkeletonSource
    {
        [Header("Startup")]
        public bool startOnEnable = true;
        public bool runOnBackgroundThread = true;
        public int deviceIndex = 0;

        [Header("Sensor Configuration")]
        public DepthMode depthMode = DepthMode.NFOV_Unbinned;
        public ColorResolution colorResolution = ColorResolution.Off;
        public ImageFormat colorFormat = ImageFormat.ColorBGRA32;
        public FPS cameraFps = FPS.FPS30;
        public bool synchronizedImagesOnly = false;

        [Header("Body Tracking Configuration")]
        public TrackerProcessingMode processingMode = TrackerProcessingMode.Gpu;
        public SensorOrientation sensorOrientation = SensorOrientation.Default;
        [Range(0f, 1f)] public float temporalSmoothing = 0.5f;
        public int popResultTimeoutMilliseconds = 100;

        [Header("Coordinate Conversion")]
        public bool convertMillimetersToMeters = true;
        public bool invertYForUnity = true;
        public bool invertZForUnity = false;

        [Header("Runtime Status")]
        [SerializeField] private bool isRunning;
        [SerializeField] private bool isInitialized;
        [SerializeField] private int latestBodyCount;
        [SerializeField] private string lastError;

        private readonly object bodyLock = new object();
        private readonly List<AkgfTrackedBody> latestBodies = new List<AkgfTrackedBody>(8);
        private readonly AkgfTrackedBody singleUserBody = new AkgfTrackedBody();

        private Device device;
        private Tracker tracker;
        private Thread workerThread;
        private volatile bool stopRequested;

        public bool IsRunning => isRunning;
        public bool IsInitialized => isInitialized;
        public int LatestBodyCount => latestBodyCount;
        public string LastError => lastError;

        private void OnEnable()
        {
            if (startOnEnable)
            {
                StartSensor();
            }
        }

        private void Update()
        {
            if (isRunning && !runOnBackgroundThread)
            {
                ProcessOneFrameSafe();
            }
        }

        private void OnDisable()
        {
            StopSensor();
        }

        private void OnDestroy()
        {
            StopSensor();
        }

        private void OnApplicationQuit()
        {
            StopSensor();
        }

        public void StartSensor()
        {
            if (isRunning)
            {
                return;
            }

            lastError = string.Empty;
            stopRequested = false;
            isRunning = true;

            if (runOnBackgroundThread)
            {
                workerThread = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = "AKGF Azure Kinect Standalone Source"
                };
                workerThread.Start();
            }
            else
            {
                try
                {
                    InitializeSdk();
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    Debug.LogError("[AKGF] Failed to start Azure Kinect standalone source: " + ex);
                    StopSensor();
                }
            }
        }

        public void StopSensor()
        {
            if (!isRunning && !isInitialized)
            {
                return;
            }

            stopRequested = true;
            isRunning = false;

            try
            {
                tracker?.Shutdown();
            }
            catch
            {
                // Ignore shutdown exceptions during application exit.
            }

            try
            {
                device?.StopCameras();
            }
            catch
            {
                // Ignore shutdown exceptions during application exit.
            }

            if (workerThread != null && workerThread.IsAlive)
            {
                if (!workerThread.Join(1500))
                {
                    Debug.LogWarning("[AKGF] Azure Kinect worker thread did not stop within 1.5 seconds. It will be left as a background thread.");
                }
            }

            workerThread = null;
            CleanupSdk();
            ClearBodies();
        }

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            lock (bodyLock)
            {
                if (AkgfSkeletonSourceUtility.TryGetClosestBody(latestBodies, out AkgfTrackedBody closest))
                {
                    singleUserBody.CopyFrom(closest);
                    body = singleUserBody;
                    return true;
                }
            }

            body = null;
            return false;
        }

        public void GetTrackedBodies(List<AkgfTrackedBody> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            lock (bodyLock)
            {
                for (int i = 0; i < latestBodies.Count; i++)
                {
                    results.Add(AkgfSkeletonSourceUtility.CloneBody(latestBodies[i]));
                }
            }
        }

        private void WorkerLoop()
        {
            try
            {
                InitializeSdk();
                while (!stopRequested)
                {
                    ProcessOneFrameSafe();
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Debug.LogError("[AKGF] Azure Kinect standalone worker stopped: " + ex);
            }
            finally
            {
                CleanupSdk();
                isRunning = false;
            }
        }

        private void InitializeSdk()
        {
            if (isInitialized)
            {
                return;
            }

            if (Device.GetInstalledCount() <= deviceIndex)
            {
                throw new InvalidOperationException($"No Azure Kinect DK found at device index {deviceIndex}. Installed devices: {Device.GetInstalledCount()}.");
            }

            device = Device.Open(deviceIndex);

            DeviceConfiguration deviceConfiguration = new DeviceConfiguration
            {
                DepthMode = depthMode,
                ColorResolution = colorResolution,
                ColorFormat = colorFormat,
                CameraFPS = cameraFps,
                SynchronizedImagesOnly = synchronizedImagesOnly
            };

            device.StartCameras(deviceConfiguration);

            Calibration calibration = device.GetCalibration(depthMode, colorResolution);
            TrackerConfiguration trackerConfiguration = new TrackerConfiguration
            {
                ProcessingMode = processingMode,
                SensorOrientation = sensorOrientation
            };

            tracker = Tracker.Create(calibration, trackerConfiguration);
            tracker.SetTemporalSmooting(Mathf.Clamp01(temporalSmoothing));
            isInitialized = true;
        }

        private void ProcessOneFrameSafe()
        {
            try
            {
                ProcessOneFrame();
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Debug.LogWarning("[AKGF] Azure Kinect frame processing issue: " + ex.Message);
            }
        }

        private void ProcessOneFrame()
        {
            if (device == null || tracker == null || stopRequested)
            {
                return;
            }

            using (Capture capture = device.GetCapture())
            {
                tracker.EnqueueCapture(capture);
            }

            using (Frame frame = tracker.PopResult(TimeSpan.FromMilliseconds(popResultTimeoutMilliseconds), false))
            {
                if (frame == null)
                {
                    return;
                }

                CopyFrame(frame);
            }
        }

        private void CopyFrame(Frame frame)
        {
            List<AkgfTrackedBody> frameBodies = new List<AkgfTrackedBody>((int)frame.NumberOfBodies);
            for (uint i = 0; i < frame.NumberOfBodies; i++)
            {
                K4ABody sdkBody = frame.GetBody(i);
                AkgfTrackedBody body = new AkgfTrackedBody();
                body.BeginFrame(unchecked((int)frame.GetBodyId(i)), CurrentTimestampSeconds());
                CopySkeleton(sdkBody.Skeleton, body);
                frameBodies.Add(body);
            }

            lock (bodyLock)
            {
                latestBodies.Clear();
                for (int i = 0; i < frameBodies.Count; i++)
                {
                    latestBodies.Add(frameBodies[i]);
                }

                latestBodyCount = latestBodies.Count;
            }
        }

        private void CopySkeleton(K4ASkeleton skeleton, AkgfTrackedBody trackedBody)
        {
            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                K4AJoint joint = skeleton.GetJoint((K4AJointId)i);
                Vector3 p = new Vector3(joint.Position.X, joint.Position.Y, joint.Position.Z);

                if (convertMillimetersToMeters)
                {
                    p /= 1000f;
                }

                if (invertYForUnity)
                {
                    p.y = -p.y;
                }

                if (invertZForUnity)
                {
                    p.z = -p.z;
                }

                trackedBody.SetJoint((AkgfJointId)i, p, ConfidenceToFloat(joint.ConfidenceLevel));
            }
        }

        private void ClearBodies()
        {
            lock (bodyLock)
            {
                latestBodies.Clear();
                latestBodyCount = 0;
            }

            singleUserBody.Clear();
        }

        private void CleanupSdk()
        {
            try
            {
                tracker?.Dispose();
            }
            catch
            {
                // ignored
            }

            try
            {
                device?.Dispose();
            }
            catch
            {
                // ignored
            }

            tracker = null;
            device = null;
            isInitialized = false;
        }

        private static double CurrentTimestampSeconds()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private static float ConfidenceToFloat(K4AJointConfidenceLevel confidence)
        {
            switch (confidence)
            {
                case K4AJointConfidenceLevel.High:
                    return 1f;
                case K4AJointConfidenceLevel.Medium:
                    return 0.75f;
                case K4AJointConfidenceLevel.Low:
                    return 0.35f;
                case K4AJointConfidenceLevel.None:
                default:
                    return 0f;
            }
        }
    }
}
#endif
