using System.Collections.Generic;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Implement this interface when one component can provide all tracked bodies from a Kinect frame.
    /// Returned bodies should use stable Azure Kinect body IDs for the current tracking session.
    /// </summary>
    public interface IAkgfMultiSkeletonSource
    {
        void GetTrackedBodies(List<AkgfTrackedBody> results);
    }
}
