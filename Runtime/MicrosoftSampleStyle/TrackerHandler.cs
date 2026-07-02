#if AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// AKGF-owned equivalent of Microsoft's sample TrackerHandler.cs.
    /// It owns AkgfSkeletalTrackingProvider, exposes the result as both single-user and multi-user AKGF sources,
    /// and optionally renders simple joint markers without requiring Microsoft's prefab or scene.
    /// </summary>
    public sealed class AkgfTrackerHandler : MonoBehaviour, IAkgfSkeletonSource, IAkgfMultiSkeletonSource
    {
        [Header("Startup")]
        public bool startOnEnable = true;
        public int deviceIndex = 0;

        [Header("Provider")]
        public AkgfSkeletalTrackingProvider provider = new AkgfSkeletalTrackingProvider();

        [Header("Selection")]
        public AkgfSingleUserSelectionPolicy singleUserSelectionMode = AkgfSingleUserSelectionPolicy.ClosestToCamera;

        [Header("Debug Rendering")]
        public bool renderSkeletons = true;
        public bool createDebugRendererOnStart = true;
        public AkgfKinectSimpleSkeletonRenderer debugRenderer;

        [Header("Runtime Status")]
        [SerializeField] private bool hasFrame;
        [SerializeField] private int bodyCount;
        [SerializeField] private string lastError;

        private readonly List<AkgfTrackedBody> latestBodies = new List<AkgfTrackedBody>(8);
        private readonly AkgfTrackedBody singleBodyBuffer = new AkgfTrackedBody();
        private readonly List<AkgfTrackedBody> selectionBuffer = new List<AkgfTrackedBody>(8);

        public bool HasFrame => hasFrame;
        public int BodyCount => bodyCount;
        public string LastError => lastError;

        private void OnEnable()
        {
            if (createDebugRendererOnStart && debugRenderer == null)
            {
                GameObject rendererObject = new GameObject("AKGF_KinectSimpleSkeletonRenderer");
                rendererObject.transform.SetParent(transform, false);
                debugRenderer = rendererObject.AddComponent<AkgfKinectSimpleSkeletonRenderer>();
            }

            if (startOnEnable)
            {
                StartTracking();
            }
        }

        private void Update()
        {
            UpdateLatestFrame();

            if (renderSkeletons && debugRenderer != null)
            {
                debugRenderer.Render(latestBodies);
            }
        }

        private void OnDisable()
        {
            StopTracking();
        }

        private void OnDestroy()
        {
            StopTracking();
        }

        public void StartTracking()
        {
            provider?.Start(deviceIndex);
        }

        public void StopTracking()
        {
            provider?.Stop();
            latestBodies.Clear();
            hasFrame = false;
            bodyCount = 0;
        }

        public bool TryGetBody(out AkgfTrackedBody body)
        {
            selectionBuffer.Clear();
            for (int i = 0; i < latestBodies.Count; i++)
            {
                selectionBuffer.Add(latestBodies[i]);
            }

            bool found;
            if (singleUserSelectionMode == AkgfSingleUserSelectionPolicy.LowestBodyId)
            {
                found = TryGetLowestBodyId(selectionBuffer, out AkgfTrackedBody selected);
                body = selected;
            }
            else
            {
                found = AkgfSkeletonSourceUtility.TryGetClosestBody(selectionBuffer, out AkgfTrackedBody selected);
                body = selected;
            }

            if (found && body != null)
            {
                singleBodyBuffer.CopyFrom(body);
                body = singleBodyBuffer;
                return true;
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
            for (int i = 0; i < latestBodies.Count; i++)
            {
                results.Add(AkgfSkeletonSourceUtility.CloneBody(latestBodies[i]));
            }
        }

        private void UpdateLatestFrame()
        {
            if (provider == null)
            {
                hasFrame = false;
                bodyCount = 0;
                lastError = "Provider is null.";
                return;
            }

            lastError = provider.LastError;

            if (!provider.TryGetLatestFrame(out AkgfKinectFrameData frame))
            {
                hasFrame = false;
                bodyCount = 0;
                latestBodies.Clear();
                return;
            }

            latestBodies.Clear();
            for (int i = 0; i < frame.bodies.Count; i++)
            {
                latestBodies.Add(frame.bodies[i].ToTrackedBody());
            }

            hasFrame = true;
            bodyCount = latestBodies.Count;
        }

        private static bool TryGetLowestBodyId(List<AkgfTrackedBody> bodies, out AkgfTrackedBody body)
        {
            body = null;
            if (bodies == null || bodies.Count == 0)
            {
                return false;
            }

            AkgfTrackedBody best = bodies[0];
            for (int i = 1; i < bodies.Count; i++)
            {
                if (bodies[i].BodyId < best.BodyId)
                {
                    best = bodies[i];
                }
            }

            body = best;
            return true;
        }
    }
}
#endif
