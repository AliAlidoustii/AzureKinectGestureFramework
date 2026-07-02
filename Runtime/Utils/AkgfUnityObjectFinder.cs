using UnityEngine;

namespace AzureKinectGestureFramework
{
    public static class AkgfUnityObjectFinder
    {
        /// <summary>
        /// Finds the first object of type T in the active scene, including inactive GameObjects.
        /// This matters because AKGF switches between SingleUserSystem and MultiUserSystem by
        /// enabling/disabling roots; references must still be resolvable while one root is inactive.
        /// </summary>
        public static T FindFirst<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            T[] items = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            T[] items = Object.FindObjectsOfType<T>(true);
#endif
            for (int i = 0; i < items.Length; i++)
            {
                if (IsSceneObject(items[i]))
                {
                    return items[i];
                }
            }

            return null;
        }

        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            T[] items = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            T[] items = Object.FindObjectsOfType<T>(true);
#endif
            if (items == null || items.Length == 0)
            {
                return new T[0];
            }

            System.Collections.Generic.List<T> sceneItems = new System.Collections.Generic.List<T>();
            for (int i = 0; i < items.Length; i++)
            {
                if (IsSceneObject(items[i]))
                {
                    sceneItems.Add(items[i]);
                }
            }

            return sceneItems.ToArray();
        }

        private static bool IsSceneObject(Object item)
        {
            if (item == null)
            {
                return false;
            }

            Component component = item as Component;
            if (component != null)
            {
                return component.gameObject != null && component.gameObject.scene.IsValid();
            }

            GameObject go = item as GameObject;
            if (go != null)
            {
                return go.scene.IsValid();
            }

            return true;
        }
    }
}
