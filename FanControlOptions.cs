namespace WardOneZeroFanControl;

public sealed class FanControlOptions
{
    public int PollingIntervalMs { get; set; } = 2000;
    public List<FanCurvePoint> FanCurve { get; set; } = [];
}