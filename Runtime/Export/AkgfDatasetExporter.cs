using System;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AzureKinectGestureFramework
{
    public sealed class AkgfDatasetExporter : MonoBehaviour
    {
        public AkgfGestureDatabase staticGestureDatabase;
        public AkgfSequenceGestureDatabase sequenceGestureDatabase;
        public string exportFolder = "AzureKinectGestureFramework/Exports";
        public bool includeHeader = true;

        public string ExportStaticGesturesCsv()
        {
            ResolveReferences();
            string folder = GetExportFolderPath();
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "static_gestures.csv");
            File.WriteAllText(path, BuildStaticCsv());
            RefreshAssetDatabaseIfNeeded(path);
            return path;
        }

        public string ExportSequenceGesturesCsv()
        {
            ResolveReferences();
            string folder = GetExportFolderPath();
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "sequence_gestures.csv");
            File.WriteAllText(path, BuildSequenceCsv());
            RefreshAssetDatabaseIfNeeded(path);
            return path;
        }

        public string ExportSummaryJson()
        {
            ResolveReferences();
            string folder = GetExportFolderPath();
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "dataset_summary.json");
            AkgfDatasetSummary summary = new AkgfDatasetSummary
            {
                exportedUtc = DateTime.UtcNow.ToString("o"),
                staticGestureCount = staticGestureDatabase != null ? staticGestureDatabase.Count : 0,
                sequenceGestureCount = sequenceGestureDatabase != null ? sequenceGestureDatabase.Count : 0,
                jointCount = AkgfJointIdExtensions.JointCount
            };
            File.WriteAllText(path, JsonUtility.ToJson(summary, true));
            RefreshAssetDatabaseIfNeeded(path);
            return path;
        }

        private void ResolveReferences()
        {
            if (staticGestureDatabase == null)
            {
                staticGestureDatabase = AkgfUnityObjectFinder.FindFirst<AkgfGestureDatabase>();
            }

            if (sequenceGestureDatabase == null)
            {
                sequenceGestureDatabase = AkgfUnityObjectFinder.FindFirst<AkgfSequenceGestureDatabase>();
            }
        }

        private string BuildStaticCsv()
        {
            StringBuilder sb = new StringBuilder(1024 * 32);
            if (includeHeader)
            {
                sb.Append("label,kind,sample_index");
                AppendPoseHeader(sb);
                sb.AppendLine();
            }

            if (staticGestureDatabase == null || staticGestureDatabase.Gestures == null)
            {
                return sb.ToString();
            }

            for (int g = 0; g < staticGestureDatabase.Gestures.Count; g++)
            {
                AkgfGestureData gesture = staticGestureDatabase.Gestures[g];
                if (gesture == null || gesture.samples == null) continue;
                for (int s = 0; s < gesture.samples.Count; s++)
                {
                    AkgfNormalizedPose pose = gesture.samples[s];
                    if (pose == null || !pose.IsValid) continue;
                    sb.Append(Escape(gesture.gestureName)).Append(",StaticPose,").Append(s);
                    AppendPoseValues(sb, pose);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private string BuildSequenceCsv()
        {
            StringBuilder sb = new StringBuilder(1024 * 64);
            if (includeHeader)
            {
                sb.Append("label,kind,sample_index,frame_index,time01");
                AppendPoseHeader(sb);
                sb.AppendLine();
            }

            if (sequenceGestureDatabase == null || sequenceGestureDatabase.Gestures == null)
            {
                return sb.ToString();
            }

            for (int g = 0; g < sequenceGestureDatabase.Gestures.Count; g++)
            {
                AkgfSequenceGestureData gesture = sequenceGestureDatabase.Gestures[g];
                if (gesture == null || gesture.samples == null) continue;
                for (int s = 0; s < gesture.samples.Count; s++)
                {
                    AkgfPoseSequence sequence = gesture.samples[s];
                    if (sequence == null || !sequence.IsValid || sequence.frames == null) continue;
                    for (int f = 0; f < sequence.frames.Count; f++)
                    {
                        AkgfNormalizedPose pose = sequence.frames[f];
                        if (pose == null || !pose.IsValid) continue;
                        float t = sequence.frames.Count <= 1 ? 0f : (float)f / (sequence.frames.Count - 1);
                        sb.Append(Escape(gesture.gestureName)).Append(",Sequence,").Append(s).Append(',').Append(f).Append(',').Append(t.ToString("0.000000"));
                        AppendPoseValues(sb, pose);
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }

        private static void AppendPoseHeader(StringBuilder sb)
        {
            for (int j = 0; j < AkgfJointIdExtensions.JointCount; j++)
            {
                string name = ((AkgfJointId)j).ToDisplayName();
                sb.Append(',').Append(name).Append("_x");
                sb.Append(',').Append(name).Append("_y");
                sb.Append(',').Append(name).Append("_z");
                sb.Append(',').Append(name).Append("_w");
            }
        }

        private static void AppendPoseValues(StringBuilder sb, AkgfNormalizedPose pose)
        {
            for (int j = 0; j < AkgfJointIdExtensions.JointCount; j++)
            {
                int bi = j * 3;
                sb.Append(',').Append(pose.values[bi].ToString("0.######"));
                sb.Append(',').Append(pose.values[bi + 1].ToString("0.######"));
                sb.Append(',').Append(pose.values[bi + 2].ToString("0.######"));
                sb.Append(',').Append(pose.weights[j].ToString("0.######"));
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        private string GetExportFolderPath()
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "AzureKinectGestureFramework/Exports");
#else
            return Path.Combine(Application.persistentDataPath, exportFolder);
#endif
        }

        private static void RefreshAssetDatabaseIfNeeded(string path)
        {
#if UNITY_EDITOR
            if (path.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase) || path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.Refresh();
            }
#endif
        }

        [Serializable]
        private sealed class AkgfDatasetSummary
        {
            public string exportedUtc;
            public int staticGestureCount;
            public int sequenceGestureCount;
            public int jointCount;
        }
    }
}
