using System;
using UnityEngine.Events;

namespace AzureKinectGestureFramework
{
    [Serializable]
    public sealed class AkgfMultiUserGestureRecognizedEvent : UnityEvent<int, string, float, AkgfGestureKind>
    {
    }

    /// <summary>
    /// UnityEvent supports up to four generic parameters.
    /// Multi-user phase data needs five values, so we pass one serializable payload instead.
    /// </summary>
    [Serializable]
    public sealed class AkgfMultiUserGesturePhaseEvent : UnityEvent<AkgfGestureEventData>
    {
    }
}
