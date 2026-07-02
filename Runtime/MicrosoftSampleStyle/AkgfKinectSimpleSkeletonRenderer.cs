#if AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
using System.Collections.Generic;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// Very small debug renderer for the AKGF-owned Kinect pipeline.
    /// It creates primitive spheres for joints. It is intentionally simple and does not require Microsoft's prefab.
    /// </summary>
    public sealed class AkgfKinectSimpleSkeletonRenderer : MonoBehaviour
    {
        public float jointScale = 0.04f;
        public int maxBodiesToRender = 6;
        public Material jointMaterial;

        private readonly List<Transform[]> bodyJointObjects = new List<Transform[]>(6);

        public void Render(List<AkgfTrackedBody> bodies)
        {
            int count = bodies == null ? 0 : Mathf.Min(maxBodiesToRender, bodies.Count);
            EnsureBodyObjectCount(count);

            for (int b = 0; b < bodyJointObjects.Count; b++)
            {
                bool active = b < count;
                Transform[] joints = bodyJointObjects[b];

                for (int j = 0; j < joints.Length; j++)
                {
                    joints[j].gameObject.SetActive(active);
                }

                if (!active)
                {
                    continue;
                }

                AkgfTrackedBody body = bodies[b];
                for (int j = 0; j < AkgfJointIdExtensions.JointCount; j++)
                {
                    Vector3 jointPosition = body.GetJoint((AkgfJointId)j);
                    joints[j].position = transform.TransformPoint(jointPosition);
                    joints[j].localScale = Vector3.one * jointScale;
                }
            }
        }

        private void EnsureBodyObjectCount(int count)
        {
            while (bodyJointObjects.Count < count)
            {
                CreateBodyObjects(bodyJointObjects.Count);
            }
        }

        private void CreateBodyObjects(int bodyIndex)
        {
            GameObject bodyRoot = new GameObject($"Body_{bodyIndex}_DebugJoints");
            bodyRoot.transform.SetParent(transform, false);

            Transform[] joints = new Transform[AkgfJointIdExtensions.JointCount];
            for (int i = 0; i < joints.Length; i++)
            {
                GameObject joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                joint.name = ((AkgfJointId)i).ToString();
                joint.transform.SetParent(bodyRoot.transform, false);
                joint.transform.localScale = Vector3.one * jointScale;

                Collider collider = joint.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                Renderer renderer = joint.GetComponent<Renderer>();
                if (renderer != null && jointMaterial != null)
                {
                    renderer.sharedMaterial = jointMaterial;
                }

                joints[i] = joint.transform;
            }

            bodyJointObjects.Add(joints);
        }
    }
}
#endif
