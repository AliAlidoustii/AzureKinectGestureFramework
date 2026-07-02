using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    public sealed class AkgfDemoActionTarget : MonoBehaviour
    {
        public Light demoLight;
        public GameObject spawnPrefab;
        public Transform spawnParent;
        public float spawnRadius = 1.5f;
        public int maxSpawnedObjects = 20;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private Renderer cachedRenderer;
        private int colorIndex;

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        public void ChangeColor()
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }

            if (cachedRenderer != null)
            {
                Color[] colors = { Color.white, Color.cyan, Color.green, Color.yellow, Color.magenta };
                colorIndex = (colorIndex + 1) % colors.Length;
                cachedRenderer.material.color = colors[colorIndex];
            }
        }

        public void LightOn()
        {
            if (demoLight != null)
            {
                demoLight.enabled = true;
            }
        }

        public void LightOff()
        {
            if (demoLight != null)
            {
                demoLight.enabled = false;
            }
        }

        public void ToggleLight()
        {
            if (demoLight != null)
            {
                demoLight.enabled = !demoLight.enabled;
            }
        }

        public void SpawnObjectForGesture(AkgfGestureEventData data)
        {
            Vector3 origin = data != null ? data.bodyPosition : transform.position;
            SpawnObject(origin + new Vector3(0f, 0.5f, 0f));
        }

        public void SpawnObject(Vector3 position)
        {
            GameObject obj;
            if (spawnPrefab != null)
            {
                obj = Instantiate(spawnPrefab, position, Quaternion.identity, spawnParent);
            }
            else
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.position = position;
                obj.transform.localScale = Vector3.one * 0.18f;
                if (spawnParent != null)
                {
                    obj.transform.SetParent(spawnParent, true);
                }
            }

            spawned.Add(obj);
            while (spawned.Count > Mathf.Max(1, maxSpawnedObjects))
            {
                GameObject oldest = spawned[0];
                spawned.RemoveAt(0);
                if (oldest != null)
                {
                    Destroy(oldest);
                }
            }
        }

        public void ClearSpawned()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Destroy(spawned[i]);
                }
            }
            spawned.Clear();
        }
    }
}
