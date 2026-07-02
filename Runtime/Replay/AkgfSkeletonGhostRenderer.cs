using UnityEngine;

namespace AzureKinectGestureFramework
{
    public sealed class AkgfSkeletonGhostRenderer : MonoBehaviour
    {
        public AkgfGestureReplay replay;
        public Transform jointPrefab;
        public Transform bonePrefab;
        public float scale = 1f;
        public bool createOnStart = true;
        public bool updateEveryFrame = true;

        private Transform[] jointObjects;

        private void Start()
        {
            if (createOnStart)
            {
                CreateJoints();
            }
        }

        private void Update()
        {
            if (updateEveryFrame)
            {
                RenderCurrentPose();
            }
        }

        public void CreateJoints()
        {
            if (jointObjects != null && jointObjects.Length == AkgfJointIdExtensions.JointCount)
            {
                return;
            }

            jointObjects = new Transform[AkgfJointIdExtensions.JointCount];
            for (int i = 0; i < jointObjects.Length; i++)
            {
                Transform joint;
                if (jointPrefab != null)
                {
                    joint = Instantiate(jointPrefab, transform);
                }
                else
                {
                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.SetParent(transform, false);
                    go.transform.localScale = Vector3.one * 0.035f;
                    joint = go.transform;
                }

                joint.name = $"Ghost_{((AkgfJointId)i).ToDisplayName()}";
                jointObjects[i] = joint;
            }
        }

        public void RenderCurrentPose()
        {
            if (replay == null)
            {
                replay = AkgfUnityObjectFinder.FindFirst<AkgfGestureReplay>();
            }

            if (replay == null || replay.CurrentPose == null || !replay.CurrentPose.IsValid)
            {
                return;
            }

            if (jointObjects == null || jointObjects.Length != AkgfJointIdExtensions.JointCount)
            {
                CreateJoints();
            }

            for (int i = 0; i < AkgfJointIdExtensions.JointCount; i++)
            {
                Vector3 p = replay.CurrentPose.GetJoint((AkgfJointId)i) * scale;
                jointObjects[i].localPosition = p;
                jointObjects[i].gameObject.SetActive(replay.CurrentPose.weights[i] > 0.01f);
            }
        }
    }
}
