using Microsoft.Extensions.Options;
using WardOneZeroFanControl.Services;

namespace WardOneZeroFanControl;

public class Worker(ILogger<Worker> logger, HardwareMonitorService hardware,
    FanCurveService fanCurve, ECService ec,
    IOptions<FanControlOptions> options) : BackgroundService
{
    private readonly FanControlOptions _options = options.Value;
    private int _lastFanPercent = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Fan control service starting.");
        float? _ = hardware.GetCpuTemperature();
        ec.EnableManualControl();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Read temperatures
                float? cpuTemp = hardware.GetCpuTemperature();
                float? gpuTemp = hardware.GetGpuTemperature();

                if (cpuTemp is null && gpuTemp is null)
                {
                    logger.LogWarning("No temperature sensors found. Skipping cycle.");
                }
                else
                {
                    // 2. Use whichever is hotter to drive the fans (safe choice)
                    float hottest = Math.Max(cpuTemp ?? 0f, gpuTemp ?? 0f);

                    // 3. Map temperature → fan %
                    int fanPercent = fanCurve.GetFanPercent(hottest);

                    // 4. Only write to EC if the value changed (reduces EC traffic)
                    if (fanPercent != _lastFanPercent)
                    {
                        ec.SetFanSpeed(fanPercent);
                        _lastFanPercent = fanPercent;

                        logger.LogInformation(
                            "CPU={CpuTemp}°C GPU={GpuTemp}°C → Fan={Fan}%",
                            cpuTemp?.ToString("F1") ?? "N/A",
                            gpuTemp?.ToString("F1") ?? "N/A",
                            fanPercent);
                    }
                }

                // 5. Wait before next poll
                await Task.Delay(_options.PollingIntervalMs, stoppingToken);
            }
        }
        finally
        {
            // This block ALWAYS runs on shutdown — critical for fan safety!
            logger.LogInformation("Fan control service stopping. Restoring auto fan control.");
            ec.RestoreAutoControl();
        }
    }
}
