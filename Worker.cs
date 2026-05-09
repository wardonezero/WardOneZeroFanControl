using Microsoft.Extensions.Options;

namespace WardOneZeroFanControl;

public class Worker(ILogger<Worker> logger, FanCurveService fanCurve, ECService ec, IOptions<FanControlOptions> options) : BackgroundService
{
    private readonly FanControlOptions _options = options.Value;
    private int _lastFanPercent = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Fan control service starting");
        ec.EnableManualFanControl();

        try
        {
            float? cpuTemp;
            float? gpuTemp;
            float hottest;
            byte fanPercent;
            while (!stoppingToken.IsCancellationRequested)
            {
                cpuTemp = ec.ReadCPUTemperature();
                gpuTemp = ec.ReadGPUTemperature();
                if (cpuTemp is null && gpuTemp is null)
                    logger.LogWarning("No temperatures available");
                else
                {
                    hottest = Math.Max(cpuTemp ?? 40f, gpuTemp ?? 40f);
                    fanPercent = fanCurve.GetFanPercent(hottest);

                    if (fanPercent != _lastFanPercent)
                    {
                        ec.SetFanSpeed(fanPercent);
                        _lastFanPercent = fanPercent;

                        logger.LogInformation(
                            "CPU={CpuTemp}°C  GPU={GpuTemp}°C  →  Fan={Fan}%",
                            cpuTemp?.ToString("F0") ?? "N/A",
                            gpuTemp?.ToString("F0") ?? "N/A",
                            fanPercent);
                    }
                }
                await Task.Delay(_options.PollingIntervalMs, stoppingToken);
            }
        }
        catch (OperationCanceledException) {/* Normal shutdown — not an error */}
        catch (Exception ex)
        {
            logger.LogError(ex, "Fan control loop crashed");
        }
        finally
        {
            logger.LogInformation("Restoring automatic fan control");
            ec.RestoreAutoFanControl();
        }
    }
}