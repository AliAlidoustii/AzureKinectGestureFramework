#if AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
using System;
using System.Threading;
using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// AKGF-owned equivalent of Microsoft's sample SkeletalTrackingProvider.cs.
    /// It opens the Azure Kinect device, starts cameras, creates the BodyTracking tracker,
    /// and publishes AkgfKinectFrameData to the Unity/main-thread handler.
    /// </summary>
    [Serializable]
    public sealed class AkgfSkeletalTrackingProvider : AkgfKinectBackgroundDataProvider
    {
        public DepthMode depthMode = DepthMode.NFOV_Unbinned;
        public ColorResolution colorResolution = ColorResolution.Off;
        public ImageFormat colorFormat = ImageFormat.ColorBGRA32;
        public FPS cameraFps = FPS.FPS30;
        public WiredSyncMode wiredSyncMode = WiredSyncMode.Standalone;
        public TrackerProcessingMode processingMode = TrackerProcessingMode.Gpu;
        public SensorOrientation sensorOrientation = SensorOrientation.Default;
        public bool convertMillimetersToMeters = true;
        public bool invertYForUnity = true;
        public bool invertZForUnity = false;
        public float temporalSmoothing = 0.5f;
        public int popResultTimeoutMilliseconds = 25;

        private Device device;
        private Tracker tracker;

        protected override void RequestStop()
        {
            try
            {
                tracker?.Shutdown();
            }
            catch
            {
                // ignored during shutdown
            }

            try
            {
                device?.StopCameras();
            }
            catch
            {
                // ignored during shutdown
            }
        }

        protected override void RunBackgroundThread(int deviceIndex, CancellationToken token)
        {
            try
            {
                if (Device.GetInstalledCount() <= deviceIndex)
                {
                    throw new InvalidOperationException($"No Azure Kinect DK found at device index {deviceIndex}. Installed devices: {Device.GetInstalledCount()}.");
                }

                using (device = Device.Open(deviceIndex))
                {
                    DeviceConfiguration deviceConfiguration = new DeviceConfiguration
                    {
                        CameraFPS = cameraFps,
                        ColorResolution = colorResolution,
                        ColorFormat = colorFormat,
                        DepthMode = depthMode,
                        WiredSyncMode = wiredSyncMode,
                        SynchronizedImagesOnly = false
                    };

                    device.StartCameras(deviceConfiguration);
                    Calibration calibration = device.GetCalibration(depthMode, colorResolution);

                    TrackerConfiguration trackerConfiguration = new TrackerConfiguration
                    {
                        ProcessingMode = processingMode,
                        SensorOrientation = sensorOrientation
                    };

                    using (tracker = Tracker.Create(calibration, trackerConfiguration))
                    {
                        tracker.SetTemporalSmooting(Mathf.Clamp01(temporalSmoothing));

                        while (!token.IsCancellationRequested)
                        {
                            ProcessOneFrame(token);
                        }
                    }
                }
            }
            finally
            {
                tracker = null;
                device = null;
            }
        }

        private void ProcessOneFrame(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

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

                AkgfKinectFrameData data = new AkgfKinectFrameData
                {
                    timestampSeconds = CurrentTimestampSeconds()
                };

                for (uint i = 0; i < frame.NumberOfBodies; i++)
                {
                    Microsoft.Azure.Kinect.BodyTracking.Body sdkBody = frame.GetBody(i);
                    int bodyId = unchecked((int)frame.GetBodyId(i));

                    AkgfKinectBodyData body = new AkgfKinectBodyData();
                    body.CopyFromSdkBody(
                        sdkBody,
                        bodyId,
                        data.timestampSeconds,
                        convertMillimetersToMeters,
                        invertYForUnity,
                        invertZForUnity);

                    data.bodies.Add(body);
                }

                SetLatestFrame(data);
            }
        }

        private static double CurrentTimestampSeconds()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
#endif
