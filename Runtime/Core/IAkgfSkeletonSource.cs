namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Implement this interface for any body source: Azure Kinect SDK, a rendered skeleton transform hierarchy,
    /// prerecorded data, or a test simulator.
    /// </summary>
    public interface IAkgfSkeletonSource
    {
        bool TryGetBody(out AkgfTrackedBody body);
    }
}
