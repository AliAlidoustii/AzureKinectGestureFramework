# Azure Kinect Gesture Framework — New Unity Project Setup Guide

This guide explains how to install and use the **Azure Kinect Gesture Framework (AKGF)** in a new Unity project from zero.

The framework supports:

- Azure Kinect DK body tracking
- Single-user gesture recognition
- Multi-user gesture recognition
- Static pose gestures
- Sequence / movement gestures
- Manual start/stop sequence recording
- Gesture confidence as percentage
- Control Panel tuning
- Pose, sequence, and calibration management
- Unity C# event output through `AkgfGestureSystemApi`

---

## 1. Requirements

### Hardware

You need:

- Azure Kinect DK
- Windows PC
- USB 3.0 port
- External power for the Kinect

Recommended camera placement:

- Distance from user: **2.0–2.5 m** for single user
- Distance for multiple users: **2.5–4.0 m**
- Height: **1.1–1.3 m**
- Camera should see the full upper body clearly

---

## 2. Software Requirements

### Unity

Recommended:

- Unity 2021 LTS or newer
- Unity 2022 LTS or Unity 2023 also works

Use a normal 3D Unity project.

### Azure Kinect SDKs

Install the official Microsoft SDKs:

1. **Azure Kinect Sensor SDK**
2. **Azure Kinect Body Tracking SDK**

After installation, Unity must be able to access these files:

```text
Microsoft.Azure.Kinect.Sensor.dll
Microsoft.Azure.Kinect.BodyTracking.dll
k4a.dll
k4abt.dll
onnxruntime.dll
body tracking model .onnx file
```

If your Kinect body tracking already works in Unity, you do not need to change anything.

If Unity shows errors like:

```text
Microsoft.Azure.Kinect not found
k4a.dll missing
k4abt.dll missing
```

then add the Microsoft SDK files to your Unity project, usually under:

```text
Assets/Plugins/
Assets/Plugins/x86_64/
```

Example:

```text
Assets/Plugins/Microsoft.Azure.Kinect.Sensor.dll
Assets/Plugins/Microsoft.Azure.Kinect.BodyTracking.dll
Assets/Plugins/x86_64/k4a.dll
Assets/Plugins/x86_64/k4abt.dll
Assets/Plugins/x86_64/onnxruntime.dll
```

---

## 3. Import the AKGF Package

Before importing a new AKGF version, always delete the old one.

Delete:

```text
Assets/AzureKinectGestureFramework
```

Also make sure there are no duplicate folders like:

```text
Assets/AKGF
Assets/AzureKinectGestureFramework_OLD
Assets/AzureKinectGestureFramework_PATCHED
```

Then import the new AKGF package so the final folder is:

```text
Assets/AzureKinectGestureFramework
```

---

## 4. Add the Required Scripting Define

Go to:

```text
Edit > Project Settings > Player > Other Settings > Scripting Define Symbols
```

Add:

```text
AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
```

Then let Unity recompile.

---

## 5. Create the Gesture System

In Unity, use:

```text
GameObject > Azure Kinect Gesture Framework > Create Gesture System
```

This creates:

```text
AzureKinectGestureSystem
├── SingleUserSystem
└── MultiUserSystem
```

The object `AzureKinectGestureSystem` should contain:

```text
AkgfGestureSystemApi
AkgfGestureSystemModeManager
```

---

## 6. Create the Azure Kinect Pipeline

Use:

```text
GameObject > Azure Kinect Gesture Framework > Create Full AKGF Kinect Pipeline
```

This creates:

```text
AKGF_KinectTrackerHandler
```

Your hierarchy should look like this:

```text
Hierarchy
├── AzureKinectGestureSystem
│   ├── SingleUserSystem
│   └── MultiUserSystem
├── AKGF_KinectTrackerHandler
└── Your own action scripts / scene objects
```

Important:

`AKGF_KinectTrackerHandler` should **not** be inside `SingleUserSystem` or `MultiUserSystem`.

It should stay active in both modes.

---

## 7. Open the AKGF Control Panel

Open:

```text
Tools > Azure Kinect Gesture Framework > AKGF Control Panel
```

In the Control Panel, set:

```text
Source Object = AKGF_KinectTrackerHandler
```

Then click:

```text
Apply Source To SingleUser
Apply Source To MultiUser
```

---

## 8. SingleUser Setup

Select:

```text
AzureKinectGestureSystem
```

Find:

```text
AkgfGestureSystemModeManager
```

Set:

```text
Tracking Mode = SingleUser
```

In the Control Panel, use:

```text
SingleUser Debug Mode
```

or:

```text
Percentage Confidence Defaults
```

Recommended starting values:

```text
Accept Current Candidate Directly = ON
Direct Static Min Similarity = 0.55
Direct Sequence Min Similarity = 0.50
Force Candidate As Result = OFF
Emit Enter = ON
Emit Detected = OFF
Emit Stay = OFF
Global Cooldown = 0.3
Same Gesture Cooldown = 1.0
```

If you want more sensitive detection, reduce the values slightly:

```text
Direct Static Min Similarity = 0.45
Direct Sequence Min Similarity = 0.40
```

---

## 9. MultiUser Setup

Set:

```text
Tracking Mode = MultiUser
```

In the Control Panel:

```text
Source Object = AKGF_KinectTrackerHandler
Apply Source To MultiUser
```

Then use:

```text
MultiUser Direct Acceptance
```

Recommended MultiUser starting values:

```text
Accept Current Candidate Directly = ON
Direct Static Min Similarity = 0.55
Direct Sequence Min Similarity = 0.50
Use Per-Gesture Threshold = OFF
Force MultiUser Candidate As Result = OFF
Emit Enter = ON
Emit Detected = OFF
Emit Stay = OFF
Global Cooldown = 0.3
Same Gesture Cooldown = 1.0
```

For sequence-only projects:

```text
Enable Static Recognition = OFF
Enable Sequence Recognition = ON
Sequence Has Priority = ON
```

For static-only projects:

```text
Enable Static Recognition = ON
Enable Sequence Recognition = OFF
```

For projects using both:

```text
Enable Static Recognition = ON
Enable Sequence Recognition = ON
Sequence Has Priority = ON
```

The framework should work in all of these cases:

```text
Static poses only
Sequence gestures only
Static + sequence together
No gestures loaded, output nothing safely
```

---

## 10. Confidence Formula

The framework uses a percentage-style confidence formula.

The matcher produces values like:

```text
0.55 = 55%
0.70 = 70%
0.85 = 85%
```

In code:

```csharp
data.confidence
```

returns percentage value:

```text
0 to 100
```

You can print it like this:

```csharp
Debug.Log($"Detected: {data.gestureName} | Confidence: {data.confidence:0}%");
```

You can also use:

```csharp
data.confidencePercent
```

For raw 0–1 value:

```csharp
data.confidence01
```

---

## 11. Recording Static Poses

Static poses are single body positions, for example:

```text
T-pose
CrossArms
HandsUp
HeadDown
```

Steps:

1. Enter Play Mode.
2. Stand in front of the Kinect.
3. Press `C` to calibrate neutral body posture.
4. Enter a gesture name in the recording panel or Control Panel.
5. Hold the pose.
6. Press `R` to record the static pose.

Saved static poses are stored under:

```text
Assets/AzureKinectGestureFramework/Resources/Gestures
```

or persistent data depending on your save settings.

---

## 12. Recording Sequence Gestures

Sequence gestures are movements over time, for example:

```text
Swipe-LeftToRight
Swipe-RightToLeft
HandWave
StepForward
```

### Manual Start/Stop Recording

Open the Control Panel or the in-game recording panel.

Use:

```text
Start Sequence Recording
Stop & Save Sequence Recording
```

Recommended process:

```text
1. Enter Play Mode
2. Set Gesture Name = Swipe-LeftToRight
3. Click Start Sequence Recording
4. Do the full movement
5. Click Stop & Save Sequence Recording
```

You can also use the hotkey `T` if toggle mode is enabled:

```text
Press T once = start recording
Press T again = stop and save
```

Saved sequence gestures are stored under:

```text
Assets/AzureKinectGestureFramework/Resources/SequenceGestures
```

or persistent data depending on your save settings.

---

## 13. Pose & Calibration Manager

Open:

```text
Tools > Azure Kinect Gesture Framework > Pose & Calibration Manager
```

This window lets you manage:

```text
Static Poses
Sequence Gestures
Calibrations
```

You can:

```text
Reload databases
Delete files
Reveal files in Explorer / Finder
Open JSON
Select asset in Unity
Add gesture to Settings Database
Load calibration
Save current calibration as default
```

Use this window whenever you want to check which gestures are actually saved and loaded.

---

## 14. Calibration

Calibration records the user’s neutral body posture.

It is useful for:

```text
Head up / head down
Subtle arm positions
Different body heights
Different standing postures
```

To calibrate:

```text
Stand naturally
Press C
Wait until calibration is saved
```

Calibration is not the same as recording a gesture.

You do not need to record calibration for every gesture, but calibration helps make recognition more stable.

---

## 15. Receiving Gesture Events in Your Own Script

Create a script called:

```text
AKGFPoseConsolePrinter.cs
```

Paste:

```csharp
using AzureKinectGestureFramework;
using UnityEngine;

public sealed class AKGFPoseConsolePrinter : MonoBehaviour
{
    [Header("AKGF")]
    [SerializeField] private AkgfGestureSystemApi gestureSystem;

    [Header("Print Settings")]
    [SerializeField] private float sameGesturePrintCooldown = 1.0f;
    [SerializeField] private bool printOnlyWhenGestureChanges = true;

    private string lastGestureName = "";
    private float lastPrintTime = -999f;

    private void Awake()
    {
        if (gestureSystem == null)
        {
#if UNITY_2023_1_OR_NEWER
            gestureSystem = Object.FindFirstObjectByType<AkgfGestureSystemApi>();
#else
            gestureSystem = Object.FindObjectOfType<AkgfGestureSystemApi>();
#endif
        }
    }

    private void OnEnable()
    {
        if (gestureSystem != null)
        {
            gestureSystem.Gesture += OnGestureRecognized;
        }
        else
        {
            Debug.LogWarning("[AKGF] No AkgfGestureSystemApi found in the scene.");
        }
    }

    private void OnDisable()
    {
        if (gestureSystem != null)
        {
            gestureSystem.Gesture -= OnGestureRecognized;
        }
    }

    private void OnGestureRecognized(AkgfGestureEventData data)
    {
        if (string.IsNullOrWhiteSpace(data.gestureName))
        {
            return;
        }

        if (printOnlyWhenGestureChanges && data.gestureName == lastGestureName)
        {
            return;
        }

        if (Time.time - lastPrintTime < sameGesturePrintCooldown)
        {
            return;
        }

        lastGestureName = data.gestureName;
        lastPrintTime = Time.time;

        Debug.Log(
            $"Detected: {data.gestureName} | " +
            $"Mode: {data.mode} | " +
            $"Body: {data.bodyId} | " +
            $"Kind: {data.gestureKind} | " +
            $"Confidence: {data.confidence:0}% | " +
            $"Phase: {data.phase}"
        );

        // Write your own actions here.
        // Example:
        // if (data.gestureName == "Swipe-LeftToRight")
        // {
        //     // Do something
        // }
    }
}
```

Add this script to an empty GameObject.

In the Inspector:

```text
Gesture System = AzureKinectGestureSystem
```

Do not assign `AKGF_KinectTrackerHandler` here.

Correct assignment:

```text
AkgfGestureSystemApi = AzureKinectGestureSystem
Source Object = AKGF_KinectTrackerHandler
```

---

## 16. SingleUser vs MultiUser Events

The same API event is used for both modes:

```csharp
gestureSystem.Gesture += OnGestureRecognized;
```

In SingleUser mode:

```text
bodyId is usually 1 or selected user
mode = SingleUser
```

In MultiUser mode:

```text
each tracked person gets their own bodyId
mode = MultiUser
```

Use:

```csharp
data.bodyId
```

if different people should trigger different actions.

---

## 17. Debug Windows

### Control Panel

Open:

```text
Tools > Azure Kinect Gesture Framework > AKGF Control Panel
```

Use it for:

```text
Mode switching
Source assignment
Threshold tuning
Cooldown tuning
Force/debug options
Recording controls
MultiUser direct acceptance
```

### Conflict Visualizer

Open:

```text
Tools > Azure Kinect Gesture Framework > Conflict Visualizer
```

Use it to see:

```text
Static candidate
Sequence candidate
Final output
Decision / block reason
```

### MultiUser Live Debug

Use:

```text
Tools > Azure Kinect Gesture Framework > Add MultiUser Live Debug UI
```

or open it from the Control Panel.

In Play Mode, toggle with:

```text
F9
```

It shows:

```text
Raw body count
Tracked body count
Normalized body count
Active users
Static DB count
Sequence DB count
MultiUser candidate
MultiUser output
Decision / block reason
Recent events
```

---

## 18. Recommended Debug Settings

### SingleUser

```text
Accept Current Candidate Directly = ON
Direct Static Min Similarity = 0.55
Direct Sequence Min Similarity = 0.50
Force Candidate As Result = OFF
Emit Enter = ON
Emit Detected = OFF
Emit Stay = OFF
Global Cooldown = 0.3
Same Gesture Cooldown = 1.0
```

### MultiUser

```text
Accept Current Candidate Directly = ON
Direct Static Min Similarity = 0.55
Direct Sequence Min Similarity = 0.50
Use Per-Gesture Threshold = OFF
Force MultiUser Candidate As Result = OFF
Emit Enter = ON
Emit Detected = OFF
Emit Stay = OFF
Global Cooldown = 0.3
Same Gesture Cooldown = 1.0
```

### Sequence-only MultiUser

```text
Enable Static Recognition = OFF
Enable Sequence Recognition = ON
Sequence Has Priority = ON
Required Consecutive Sequence Matches = 1
Recognition Window Seconds = 1.25
Minimum Window Frames = 8
```

---

## 19. Force Modes

There are debug-only force modes:

```text
Force Candidate As Result
Force MultiUser Candidate As Result
```

Use them only to check whether the matcher sees a candidate.

For real use, keep force modes OFF.

Normal use should rely on:

```text
Accept Current Candidate Directly = ON
```

Force mode bypasses thresholds and can create false positives.

---

## 20. Common Problems and Fixes

### Problem: Kinect works in SingleUser but MultiUser shows Visible 0 / Active 0

Check:

```text
Source Object = AKGF_KinectTrackerHandler
Apply Source To MultiUser
AKGF_KinectTrackerHandler is active
AKGF_KinectTrackerHandler is not inside SingleUserSystem
```

Correct hierarchy:

```text
AzureKinectGestureSystem
├── SingleUserSystem
└── MultiUserSystem
AKGF_KinectTrackerHandler
```

---

### Problem: MultiUser sees body but no gesture output

Open F9 MultiUser debug window.

Check:

```text
raw > 0
tracked > 0
normalized > 0
active > 0
staticDB or seqDB > 0
```

If sequence-only:

```text
staticDB = 0 is okay
seqDB > 0 is required
```

Then use:

```text
Enable Static Recognition = OFF
Enable Sequence Recognition = ON
Sequence Has Priority = ON
Accept Current Candidate Directly = ON
```

---

### Problem: Console prints every frame

Use:

```text
Emit Enter = ON
Emit Detected = OFF
Emit Stay = OFF
```

Set cooldowns:

```text
Global Cooldown = 0.3
Same Gesture Cooldown = 1.0
```

Also use the spam-protected console script from this guide.

---

### Problem: Static poses always win over sequence gestures

Use:

```text
Sequence Has Priority = ON
```

Increase static threshold:

```text
Direct Static Min Similarity = 0.65
```

or temporarily disable static recognition:

```text
Enable Static Recognition = OFF
```

---

### Problem: Sequence candidate flickers in MultiUser debug

Use:

```text
Hold Last Debug Candidate = ON
Debug Candidate Hold Seconds = 0.45
```

This affects only debug display stability, not real recognition.

---

### Problem: StaticDB = 0

If you only use sequences, this is fine.

If you need static poses, open:

```text
Tools > Azure Kinect Gesture Framework > Pose & Calibration Manager
```

Go to:

```text
Static Poses
```

Record or reload static poses.

---

### Problem: SeqDB = 0

Open:

```text
Tools > Azure Kinect Gesture Framework > Pose & Calibration Manager
```

Go to:

```text
Sequence Gestures
```

Check if your sequence JSON files exist.

Then click:

```text
Reload DBs
```

---

## 21. Best Testing Order

For a new project, test in this order:

```text
1. Kinect body tracking works
2. SingleUser mode shows body
3. Record one simple sequence gesture
4. SingleUser prints the sequence gesture
5. Switch to MultiUser
6. Apply source to MultiUser
7. Open F9 MultiUser debug window
8. Confirm raw/tracked/normalized/active are all > 0
9. Confirm seqDB > 0
10. Enable MultiUser Direct Acceptance
11. Confirm MultiUser prints the gesture
```

Do not start with many gestures at once.

Test first with one simple gesture, for example:

```text
Swipe-LeftToRight
```

Then add more.

---

## 22. Final Correct Scene Setup

Your final scene should usually have:

```text
Hierarchy
├── AzureKinectGestureSystem
│   ├── SingleUserSystem
│   └── MultiUserSystem
├── AKGF_KinectTrackerHandler
├── AKGF_PoseConsolePrinter
└── Your scene objects
```

Correct references:

```text
Source Object = AKGF_KinectTrackerHandler
AkgfGestureSystemApi = AzureKinectGestureSystem
```

---

## 23. Notes for Production Use

For production, avoid force mode.

Use:

```text
Accept Current Candidate Directly = ON
Emit Enter = ON
Emit Stay = OFF
Emit Detected = OFF
```

Tune thresholds gesture by gesture:

```text
Easy gestures: 0.65–0.80
Hard gestures: 0.45–0.60
Sequence gestures: 0.45–0.70
```

Use cooldowns to prevent repeated actions:

```text
Global Cooldown = 0.3
Same Gesture Cooldown = 1.0
```

Use MultiUser body IDs if each person should control different things.

---

## 24. Quick Reference

### Main menus

```text
GameObject > Azure Kinect Gesture Framework > Create Gesture System
GameObject > Azure Kinect Gesture Framework > Create Full AKGF Kinect Pipeline
Tools > Azure Kinect Gesture Framework > AKGF Control Panel
Tools > Azure Kinect Gesture Framework > Pose & Calibration Manager
Tools > Azure Kinect Gesture Framework > Conflict Visualizer
Tools > Azure Kinect Gesture Framework > Add MultiUser Live Debug UI
```

### Main hotkeys

```text
C  = calibrate neutral posture
R  = record static pose
T  = start/stop sequence recording, if toggle mode is enabled
F7 = recording panel
F8 = profiler / performance panel
F9 = MultiUser live debug UI
```

### Important objects

```text
AzureKinectGestureSystem = gesture API and mode manager
SingleUserSystem = single-user recognizers
MultiUserSystem = multi-user recognizer manager
AKGF_KinectTrackerHandler = Kinect skeleton source
```

### Most important settings

```text
Accept Current Candidate Directly = ON
Force Candidate As Result = OFF
Emit Enter = ON
Emit Detected = OFF
Emit Stay = OFF
```
