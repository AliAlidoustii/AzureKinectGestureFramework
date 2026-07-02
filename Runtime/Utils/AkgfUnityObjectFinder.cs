using UnityEngine;

namespace AzureKinectGestureFramework
{
    public static class AkgfUnityObjectFinder
    {
        public static T FindFirst<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
