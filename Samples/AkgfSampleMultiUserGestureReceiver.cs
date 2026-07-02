using UnityEngine;

namespace AzureKinectGestureFramework.Samples
{
    public sealed class AkgfSampleMultiUserGestureReceiver : MonoBehaviour
    {
        public void OnMultiUserGesture(int bodyId, string gestureName, float confidence, AkgfGestureKind kind)
        {
            Debug.Log($"[AKGF MultiUser] Body {bodyId} did {gestureName} ({kind}) confidence={confidence:0.00}", this);
        }

        public void OnMultiUserGesturePhase(int bodyId, string gestureName, float confidence, AkgfGestureKind kind, AkgfGesturePhase phase)
        {
            Debug.Log($"[AKGF MultiUser] Body {bodyId} {gestureName} phase={phase} ({kind}) confidence={confidence:0.00}", this);
        }
    }
}
