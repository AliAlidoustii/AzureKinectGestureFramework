using System;
using System.IO;
using UnityEngine;

namespace AzureKinectGestureFramework
{
    /// <summary>
    /// CSV logger for debugging, research, demos, and later ML analysis.
    /// Enable this component when you want event logs written to Application.persistentDataPath.
    /// </summary>
    public sealed class AkgfGestureEventLogger : MonoBehaviour
    {
        [Header("References")]
        public AkgfGestureSystemApi gestureSystemApi;
        public AkgfGestureCoordinator singleUserCoordinator;
        public AkgfMultiUserGestureManager multiUserManager;
        public bool autoFindReferences = true;

        [Header("Logging")]
        public bool logOnEnable = false;
        public bool alsoLogToConsole = false;
        public bool logDetected = true;
        public bool logEnter = true;
        public bool logStay = false;
        public bool logExit = true;
        public bool logConfirmed = true;
        public string relativeFolder = "AzureKinectGestureFramework/Logs";
        public string filePrefix = "gesture_log";
        public bool flushEveryEvent = true;

        public string CurrentLogPath { get; private set; } = string.Empty;
        public bool IsLogging => writer != null;

        private StreamWriter writer;
        private bool subscribed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            if (logOnEnable)
            {
                StartLogging();
            }
        }

        private void OnDisable()
        {
            StopLogging();
            Unsubscribe();
        }

        public void ResolveReferences()
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (gestureSystemApi == null)
            {
                gestureSystemApi = AkgfUnityObjectFinder.FindFirst<AkgfGestureSystemApi>();
            }

            if (singleUserCoordinator == null)
            {
                singleUserCoordinator = AkgfUnityObjectFinder.FindFirst<AkgfGestureCoordinator>();
            }

            if (multiUserManager == null)
            {
                multiUserManager = AkgfUnityObjectFinder.FindFirst<AkgfMultiUserGestureManager>();
            }
        }

        public void StartLogging()
        {
            if (writer != null)
            {
                return;
            }

            string folder = Path.Combine(Application.persistentDataPath, relativeFolder);
            Directory.CreateDirectory(folder);
            string filename = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            CurrentLogPath = Path.Combine(folder, filename);
            writer = new StreamWriter(CurrentLogPath, false);
            writer.WriteLine("utc,unityTime,mode,bodyId,gesture,kind,phase,confidencePercent,confidence01,group,priority,trackingQuality,mirrored,bodyX,bodyY,bodyZ");
            writer.Flush();
        }

        public void StopLogging()
        {
            if (writer == null)
            {
                return;
            }

            writer.Flush();
            writer.Dispose();
            writer = null;
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (gestureSystemApi != null)
            {
                gestureSystemApi.Gesture += HandleApiGesture;
                subscribed = true;
                return;
            }

            if (singleUserCoordinator != null)
            {
                singleUserCoordinator.GesturePhase += HandleSingleGesture;
            }

            if (multiUserManager != null)
            {
                multiUserManager.MultiUserGesturePhase += HandleMultiGesture;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (gestureSystemApi != null)
            {
                gestureSystemApi.Gesture -= HandleApiGesture;
            }

            if (singleUserCoordinator != null)
            {
                singleUserCoordinator.GesturePhase -= HandleSingleGesture;
            }

            if (multiUserManager != null)
            {
                multiUserManager.MultiUserGesturePhase -= HandleMultiGesture;
            }

            subscribed = false;
        }

        private void HandleApiGesture(AkgfGestureEventData data)
        {
            Write(data);
        }

        private void HandleSingleGesture(AkgfGestureMatchResult match)
        {
            Write(AkgfGestureEventData.FromMatch(match, AkgfTrackingMode.SingleUser));
        }

        private void HandleMultiGesture(AkgfGestureMatchResult match)
        {
            Write(AkgfGestureEventData.FromMatch(match, AkgfTrackingMode.MultiUser));
        }

        private void Write(AkgfGestureEventData data)
        {
            if (data == null || !ShouldLogPhase(data.phase))
            {
                return;
            }

            if (writer == null && logOnEnable)
            {
                StartLogging();
            }

            string line = string.Join(",", new string[]
            {
                Escape(DateTime.UtcNow.ToString("o")),
                data.unityTimeSeconds.ToString("0.000"),
                Escape(data.mode),
                data.bodyId.ToString(),
                Escape(data.gestureName),
                data.gestureKind.ToString(),
                data.phase.ToString(),
                data.confidencePercent.ToString("0.0"),
                data.confidence01.ToString("0.0000"),
                Escape(data.groupName),
                data.priority.ToString(),
                data.trackingQuality.ToString("0.0000"),
                data.wasMirrored ? "1" : "0",
                data.bodyPosition.x.ToString("0.000"),
                data.bodyPosition.y.ToString("0.000"),
                data.bodyPosition.z.ToString("0.000")
            });

            if (writer != null)
            {
                writer.WriteLine(line);
                if (flushEveryEvent)
                {
                    writer.Flush();
                }
            }

            if (alsoLogToConsole)
            {
                Debug.Log($"AKGF Log: {line}", this);
            }
        }

        private bool ShouldLogPhase(AkgfGesturePhase phase)
        {
            switch (phase)
            {
                case AkgfGesturePhase.Enter:
                    return logEnter;
                case AkgfGesturePhase.Stay:
                    return logStay;
                case AkgfGesturePhase.Exit:
                    return logExit;
                case AkgfGesturePhase.Confirmed:
                    return logConfirmed;
                default:
                    return logDetected;
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
