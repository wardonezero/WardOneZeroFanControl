using LibreHardwareMonitor.Hardware;

namespace WardOneZeroFanControl.Services;

/// <summary>
/// Wraps LibreHardwareMonitor to read CPU and GPU temperatures.
/// 
/// IDisposable: we must close the hardware handles when the app stops,
/// otherwise the kernel driver stays loaded and port handles leak.
/// </summary>
public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private readonly ILogger<HardwareMonitorService> _logger;

    public HardwareMonitorService(ILogger<HardwareMonitorService> logger)
    {
        _logger = logger;

        // Tell LibreHardwareMonitor what hardware categories to enable.
        // Only enable what you need — each category costs CPU and memory.
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,   // covers both NVIDIA and AMD
        };

        _computer.Open();  // loads the WinRing0 kernel driver
        _logger.LogInformation("Hardware monitor opened.");
    }

    /// <summary>
    /// Returns the highest temperature sensor value (°C) found for CPUs.
    /// Returns null if no sensor is available.
    /// </summary>
    public float? GetCpuTemperature()
        => GetMaxTemperature(HardwareType.Cpu);

    /// <summary>
    /// Returns the highest temperature sensor value (°C) found for GPUs.
    /// Checks both NVIDIA and AMD discrete GPUs.
    /// </summary>
    public float? GetGpuTemperature()
    {
        // Try NVIDIA first, fall back to AMD
        return GetMaxTemperature(HardwareType.GpuNvidia)
            ?? GetMaxTemperature(HardwareType.GpuAmd);
    }

    private float? GetMaxTemperature(HardwareType type)
    {
        float? max = null;

        foreach (IHardware? hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != type) continue;

            // IMPORTANT: you must call Update() before reading sensors,
            // otherwise you get stale values from the last poll.
            hardware.Update();

            foreach (ISensor? sensor in hardware.Sensors)
            {
                if (sensor.SensorType != SensorType.Temperature) continue;
                if (sensor.Value is null) continue;

                if (max is null || sensor.Value > max)
                    max = sensor.Value;
            }
        }

        return max;
    }

    public void Dispose()
    {
        _computer.Close();  // unloads driver handles cleanly
        _logger.LogInformation("Hardware monitor closed.");
    }
}