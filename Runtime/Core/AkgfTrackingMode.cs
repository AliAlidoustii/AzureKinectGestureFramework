namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Developer-selected tracking mode. SingleUser keeps the existing one-body flow.
    /// MultiUser runs one recognition state per visible body.
    /// </summary>
    public enum AkgfTrackingMode
    {
        SingleUser,
        MultiUser
    }
}
