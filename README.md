# WardOneZero Fan Control by Eduard Melik-Hakobyan

<p align="left">
  <img src="https://img.shields.io/badge/Windows-11-blue?logo=windows&logoColor=white" alt="Windows 11"/>
  <img src="https://img.shields.io/badge/C%23-10.0-239120?logo=c-sharp&logoColor=white" alt="C# 14"/>
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10"/>
</p>

A Windows Service written in C# (.NET 10) to automatically control laptop fan speeds based on CPU and GPU temperatures.

**⚠️ IMPORTANT WARNING ⚠️**  
This software writes directly to the Embedded Controller (EC) memory registers. **It was specifically designed, built, and tested ONLY for the MSI Katana GF76 11UD.**  
Using this on unsupported hardware or modifying EC registers incorrectly can result in permanent hardware damage, overheating, or system instability. **Use at your own risk!** The author is not responsible for any damage caused.

## Prerequisites
- Windows 11
- [.NET 10 Runtime](https://dotnet.microsoft.com/download)
- `inpoutx64.dll` - Required to access hardware ports.
- **Administrator Privileges** are required to interact with the EC, and run the background service.

## Installation Guide

1. **System Preparation**: Ensure `inpoutx64.dll` placed in the project root (so it is copied during publishing).
2. **Publish the Project**: Follow the *Publish Guide* below to compile the application.
3. **Install as a Windows Service**:
   Open an elevated **Administrator** PowerShell terminal, navigate to the published folder (e.g., `C:\WardOneZero\FanControl`), and execute:
   ```powershell
   sc.exe create "WardOneZeroFanControl" binPath= "C:\WardOneZero\FanControl\WardOneZeroFanControl.exe" start= auto
   ```
   *(Note: The space after `binPath=` and `start=` is strictly required by sc.exe).*
4. **Start the Service**:
   ```powershell
   sc.exe start "WardOneZeroFanControl"
   ```
5. **Configuration**: Edit `appsettings.json` in the publish directory to tweak your Custom Fan Curve, then restart the service.

## Publish Guide

The project is set up with a publish profile (`FolderProfile.pubxml`) configured to compile a self-contained, single-file Windows executable to `C:\WardOneZero\FanControl`.

### Using Visual Studio
1. Open Visual Studio as **Administrator** (this is necessary since the publish destination is a protected root folder on the `C:\` drive).
2. Right-click the `WardOneZeroFanControl` project in Solution Explorer and select **Publish**.
3. Ensure the `FolderProfile` profile is selected and click **Publish**.

## Technical Structure

- `FanCurvePoint.cs` & `FanControlOptions.cs`: Models for managing custom fan curves from configuration.
- `FanCurveService.cs`: Uses linear interpolation to accurately calculate the required fan speed based on current temperatures.
- `ECService.cs`: Handles all low-level Embedded Controller reads/writes via memory ports to get CPU/GPU temperatures and override fan modes.
- `Worker.cs` & `Program.cs`: The core .NET background worker loop handling polling intervals and safe recovery states.

#### Windows 11 C# .Net
#### Windows Service

**Fan Curve Point** `FanCurvePoint.cs`
- `float` Temperature
- `byte` Fan Percent
- 
**Fan Control Options** `FanControlOptions.cs`
- `int`                 Polling Interval Ms
- `List<FanCurvePoint>` Fan Curve

**Fan Curve Service** `FanCurveService.cs`
- `byte` GetFanPercent(float tempC)

**EC Service** *(Embedded Controller)* `ECService.cs`
    -          `float?` ReadCPUTemperature()
    -          `float?` ReadGPUTemperature()
    -          `void`   EnableManualFanControl()
    -          `void`   SetFanSpeed(`byte` percent)
    -          `void`   RestoreAutoFanControl()
    - `static` `void`   ECWrite(`byte` register, `byte` value)
    - `static` `byte`   ECRead(`byte` register)
    - `static` `void`   WaitOutputBufferFull()

- `static` `class` Port
  - DllImport(`"inpoutx64.dll"`)
    - `static` `extern` `bool`  IsInpOutDriverOpen()
    - `static` `extern` `void`  Out32(`short` portAddress, `short` data)
    - `static` `extern` `short` Inp32(`short` portAddress)
  - `static` `void` Out8(`int` port, `byte` value)
  - `static` `byte` In8(`int` port)

---
 
- `Program.cs`
- `Worker.cs`
- `inpoutx64.dll`
- `appsettings.json`