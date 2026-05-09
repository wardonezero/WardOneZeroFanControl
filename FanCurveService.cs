using Microsoft.Extensions.Options;

namespace WardOneZeroFanControl;

public sealed class FanCurveService
{
    private readonly FanControlOptions _options;

    public FanCurveService(IOptions<FanControlOptions> options)
    {
        _options = options.Value;
        _options.FanCurve.Sort((a, b) => a.Temperature.CompareTo(b.Temperature));
    }

    public byte GetFanPercent(float tempC)
    {
        List<FanCurvePoint> curve = _options.FanCurve;
        if (curve.Count == 0) return 20;

        // Below the first point → use minimum fan speed
        if (tempC <= curve[0].Temperature) return curve[0].FanPercent;

        // Above the last point → run at maximum fan speed
        if (tempC >= curve[^1].Temperature) return curve[^1].FanPercent;

        // Find the two curve points that straddle our temperature
        for (byte i = 0; i < curve.Count - 1; i++)
        {
            FanCurvePoint low = curve[i];
            FanCurvePoint high = curve[i + 1];

            if (tempC >= low.Temperature && tempC <= high.Temperature)
            {
                // Linear interpolation formula: y = y0 + (x-x0) * (y1-y0)/(x1-x0)
                float ratio = (tempC - low.Temperature) / (high.Temperature - low.Temperature);
                return (byte)(low.FanPercent + ratio * (high.FanPercent - low.FanPercent));
            }
        }

        return curve[^1].FanPercent;
    }
}