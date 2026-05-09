# WardOneZero Fan Control - Architecture & Documentation

## Architecture Overview
The application is built as a **.NET 10 Worker Service**. It utilizes the Generic Host pattern for dependency injection, logging, and configuration. The system acts as a background daemon that periodically polls hardware temperatures, processes them against a user-defined mathematical curve, and transmits new fan speed commands directly to the hardware's Embedded Controller (EC).

The architecture is split into three main layers:
1. **Hardware Communication (Infrastructure):** Direct memory I/O interactions using `inpoutx64.dll` to read/write to the EC.
2. **Business Logic (Core):** Configuration parsing, sorting, and linear interpolation to calculate precise fan speeds.
3. **Orchestration (Host):** The background loop managing the system lifecycle, error recovery, and safe shutdown (restoring auto-fan control on exit).

---

## File Dictionary & Component Breakdown

### 1. The Worker Host / Orchestration Layer

#### `Program.cs`
- **Purpose**: The bootstrapper for the application.
- **Structure**: Uses `Host.CreateApplicationBuilder`.
- **Role**: 
  - Sets up the application to run as a Windows Service (`AddWindowsService`).
  - Binds the `appsettings.json` configuration to `FanControlOptions`.
  - Registers the Singleton services (`FanCurveService`, `ECService`).
  - Starts the background `Worker`.

#### `Worker.cs`
- **Purpose**: The main application loop and lifecycle manager.
- **Structure**: Inherits from `BackgroundService`.
- **Role**:
  - Sets the EC to Manual Fan Control on startup.
  - Runs a continuous `while (!stoppingToken.IsCancellationRequested)` loop.
  - Queries `ECService` for both CPU and GPU temperatures, finding the highest of the two.
  - Passes the highest temperature to `FanCurveService` to get the target fan percentage.
  - Applies the new speed to the EC *only* if the calculated value has changed (to prevent spamming the EC).
  - Contains a `try-catch-finally` block to ensure `ec.RestoreAutoFanControl()` is **always** called upon cancellation or crash, preventing the system from overheating if the service fails.

---

### 2. Business Logic Layer

#### `FanCurveService.cs`
- **Purpose**: Calculates the correct fan speed based on current temperatures.
- **Structure**: A simple Singleton service utilizing `IOptions<FanControlOptions>`.
- **Role**: 
  - On initialization, it sorts the configured fan curve by temperature to ensure the math evaluates correctly.
  - Implements `GetFanPercent(float tempC)`:
    - Automatically handles out-of-bounds temperatures (using lowest fan speed below the curve and max fan speed above the curve).
    - If the temperature falls between two defined curve points, it applies a **Linear Interpolation formula** `y = y0 + (x-x0) * (y1-y0)/(x1-x0)` to calculate a smooth, transitional fan speed.

#### `FanControlOptions.cs` & `FanCurvePoint.cs`
- **Purpose**: Configuration models (POCOs).
- **Structure**: Simple classes containing properties.
- **Role**: Maps the `appsettings.json` data into strongly-typed C# objects. `FanControlOptions` holds the polling speed and a list of `FanCurvePoint` objects (Temperature and Fan Percent limits).

---

### 3. Hardware & Infrastructure Layer

#### `ECService.cs`
- **Purpose**: Bridge between the C# application and the laptop's physical circuitry (Embedded Controller).
- **Structure**: Contains hardware register definitions (Magic Numbers) specific to the **MSI Katana GF76**, methods to read/write hardware states, and a nested `Port` class.
- **Role**:
  - `ReadCPUTemperature` / `ReadGPUTemperature`: Reads byte values from registers `0x68` and `0x80`.
  - `EnableManualFanControl` / `RestoreAutoFanControl`: Writes to register `0xF4` to toggle firmware vs. manual fan logic.
  - `SetFanSpeed`: Writes desired duty cycle to registers `0xB0` (CPU) and `0xB1` (GPU).
  - **Nested `Port` Class**: Acts as a P/Invoke wrapper around `inpoutx64.dll`, safely checking if the driver is loaded and exposing `In8` and `Out8` methods for direct memory port manipulation.

#### `inpoutx64.dll`
- **Purpose**: Hardware I/O Driver.
- **Role**: Modern Windows OSes block direct hardware register access from user mode. This third-party driver elevates the application's ability to read and write directly to hardware ports (`0x62` Data and `0x66` Command/Status). **This is why the application requires Administrator access.**

---

### 4. Configuration

#### `appsettings.json`
- **Purpose**: User configuration file.
- **Structure**: Standard JSON.
- **Role**: Holds logging verbosity, the `PollingIntervalMs` (default 2000ms), and the `FanCurve` array. Changing this file (after installation) dictates how aggressive the cooling profile is without needing to recompile the source code.
