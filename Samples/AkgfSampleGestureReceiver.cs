using UnityEngine;

namespace AzureKinectGestureFramework.Samples
{
    public sealed class AkgfSampleGestureReceiver : MonoBehaviour
    {
        public void PrintGestureName(string gestureName, float similarity)
        {
            Debug.Log($"Gesture detected: {gestureName} ({similarity:0.000})", this);
        }

        public void PrintGestureDetailed(string gestureName, float similarity, AkgfGestureKind kind)
        {
            Debug.Log($"Gesture detected: {gestureName} [{kind}] ({similarity:0.000})", this);
        }

        public void PrintGesturePhase(string gestureName, float similarity, AkgfGestureKind kind, AkgfGesturePhase phase)
        {
            Debug.Log($"Gesture phase: {gestureName} [{kind}/{phase}] ({similarity:0.000})", this);
        }

        public void CrossArmsAction()
        {
            Debug.Log("CrossArms action fired.", this);
        }

        public void HeadUpAction()
        {
            Debug.Log("HeadUp action fired.", this);
        }

        public void HeadDownAction()
        {
            Debug.Log("HeadDown action fired.", this);
        }

        public void WaveAction()
        {
            Debug.Log("Wave sequence action fired.", this);
        }

        public void NodAction()
        {
            Debug.Log("Nod sequence action fired.", this);
        }
    }
}
