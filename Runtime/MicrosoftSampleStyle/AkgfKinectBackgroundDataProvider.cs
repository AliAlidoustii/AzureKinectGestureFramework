#if AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
using System;
using System.Threading;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// AKGF-owned background provider, inspired by Microsoft's sample BackgroundDataProvider.
    /// Keeps SDK work off Unity's main thread and exposes the latest frame through a thread-safe copy.
    /// </summary>
    public abstract class AkgfKinectBackgroundDataProvider : IDisposable
    {
        private readonly object frameLock = new object();
        private readonly AkgfKinectFrameData latestFrame = new AkgfKinectFrameData();
        private Thread workerThread;
        private CancellationTokenSource cancellation;

        public bool IsRunning { get; private set; }
        public string LastError { get; protected set; } = string.Empty;

        public void Start(int deviceIndex)
        {
            if (IsRunning)
            {
                return;
            }

            LastError = string.Empty;
            cancellation = new CancellationTokenSource();
            IsRunning = true;

            workerThread = new Thread(() => ThreadMain(deviceIndex, cancellation.Token))
            {
                IsBackground = true,
                Name = "AKGF Kinect Background Provider"
            };
            workerThread.Start();
        }

        public void Stop()
        {
            if (!IsRunning && workerThread == null)
            {
                return;
            }

            try
            {
                cancellation?.Cancel();
            }
            catch
            {
                // Ignore cancellation exceptions during shutdown.
            }

            try
            {
                RequestStop();
            }
            catch
            {
                // Provider-specific shutdown should not break Unity exit.
            }

            if (workerThread != null && workerThread.IsAlive)
            {
                workerThread.Join(1500);
            }

            workerThread = null;
            cancellation?.Dispose();
            cancellation = null;
            IsRunning = false;
        }

        public bool TryGetLatestFrame(out AkgfKinectFrameData frame)
        {
            lock (frameLock)
            {
                if (latestFrame.BodyCount == 0)
                {
                    frame = null;
                    return false;
                }

                frame = latestFrame.DeepCopy();
                return true;
            }
        }

        protected void SetLatestFrame(AkgfKinectFrameData frame)
        {
            if (frame == null)
            {
                return;
            }

            lock (frameLock)
            {
                latestFrame.Clear();
                latestFrame.timestampSeconds = frame.timestampSeconds;

                for (int b = 0; b < frame.bodies.Count; b++)
                {
                    AkgfKinectBodyData source = frame.bodies[b];
                    AkgfKinectBodyData body = new AkgfKinectBodyData
                    {
                        id = source.id,
                        timestampSeconds = source.timestampSeconds
                    };

                    for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
                    {
                        body.jointPositions[i] = source.jointPositions[i];
                        body.jointRotations[i] = source.jointRotations[i];
                        body.jointConfidences[i] = source.jointConfidences[i];
                    }

                    latestFrame.bodies.Add(body);
                }
            }
        }

        private void ThreadMain(int deviceIndex, CancellationToken token)
        {
            try
            {
                RunBackgroundThread(deviceIndex, token);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path.
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            finally
            {
                IsRunning = false;
            }
        }

        protected virtual void RequestStop()
        {
        }

        protected abstract void RunBackgroundThread(int deviceIndex, CancellationToken token);

        public void Dispose()
        {
            Stop();
        }
    }
}
#endif
