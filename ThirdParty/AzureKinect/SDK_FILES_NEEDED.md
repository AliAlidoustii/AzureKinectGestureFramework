# Azure Kinect SDK files needed by the standalone AKGF Kinect pipeline

AKGF includes the Unity-side C# scripts for an Azure Kinect tracking pipeline, but it does **not** redistribute Microsoft SDK binaries.
Install the official NuGet packages / Azure Kinect SDK files and copy the runtime files into a Unity plugin folder.

Recommended AKGF folder:

```text
Assets/AzureKinectGestureFramework/ThirdParty/AzureKinect/Plugins/
```

Do not keep duplicate copies of the same Microsoft assemblies in multiple Unity plugin folders.

## Managed assemblies usually needed in `Assets/.../Plugins/`

From `Microsoft.Azure.Kinect.BodyTracking` package:

```text
Microsoft.Azure.Kinect.BodyTracking.dll
Microsoft.Azure.Kinect.BodyTracking.deps.json
Microsoft.Azure.Kinect.BodyTracking.xml
```

From `Microsoft.Azure.Kinect.Sensor` package:

```text
Microsoft.Azure.Kinect.Sensor.dll
Microsoft.Azure.Kinect.Sensor.deps.json
Microsoft.Azure.Kinect.Sensor.xml
```

Common .NET dependency DLLs used by the official sample:

```text
System.Buffers.dll
System.Memory.dll
System.Reflection.Emit.Lightweight.dll
System.Runtime.CompilerServices.Unsafe.dll
```

## Native runtime files usually needed

```text
k4a.dll
k4arecord.dll
k4abt.dll
depthengine_2_0.dll
onnxruntime.dll
onnxruntime_providers_shared.dll
onnxruntime_providers_cuda.dll
onnxruntime_providers_tensorrt.dll
directml.dll
```

## Model file usually needed near the player executable / project root depending on setup

```text
dnn_model_2_0_op11.onnx
```

For Unity builds, ONNX Runtime / DirectML / CUDA / TensorRT files may need to be next to the built `.exe`, depending on the selected tracker processing mode.
