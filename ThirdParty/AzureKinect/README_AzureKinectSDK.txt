AKGF Azure Kinect SDK folder
============================

This folder is intentionally empty except for this note.

The framework now contains a standalone Kinect source script:

    Runtime/Sources/AkgfAzureKinectStandaloneSource.cs

That script opens the Azure Kinect DK directly and feeds skeletons into AKGF.
It means you do NOT need to modify Microsoft sample scripts such as TrackerHandler.cs.

However, the official Microsoft Azure Kinect Sensor and Body Tracking native/runtime DLLs are external Microsoft SDK files. They are not bundled here.

You need one of these setups:

1) If the Microsoft Unity body tracking sample is already imported and compiling:
   - You probably already have Microsoft.Azure.Kinect.Sensor and Microsoft.Azure.Kinect.BodyTracking assemblies in the project.
   - Add scripting define symbol:
       AKGF_MICROSOFT_AZURE_KINECT_STANDALONE
   - Then use the AKGF standalone source component.

2) If you want all SDK files physically under this AKGF folder:
   - Copy the required Microsoft Azure Kinect DLLs/native files from your installed SDK or NuGet packages into a Plugins folder under this directory, for example:
       Assets/AzureKinectGestureFramework/ThirdParty/AzureKinect/Plugins/
   - Then add scripting define symbol:
       AKGF_MICROSOFT_AZURE_KINECT_STANDALONE

Avoid duplicate SDK assemblies. Do not keep the same Microsoft.Azure.Kinect.* managed DLLs in multiple Unity plugin folders at the same time.

Also do not run two Kinect pipelines at once. If you use AKGF_StandaloneAzureKinectSource, disable the Microsoft sample tracker GameObject while testing AKGF.
