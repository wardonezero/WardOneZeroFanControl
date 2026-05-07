using Microsoft.Extensions.Options;

namespace WardOneZeroFanControl.Services;

/// <summary>
/// Given a temperature, calculates the correct fan speed percentage
/// by linearly interpolating between the configured curve points.
/// </summary>
public sealed class FanCurveService
{
    private readonly FanControlOptions _options;

    public FanCurveService(IOptions<FanControlOptions> options)
    {
        _options = options.Value;

        // Sort ascending by temperature so our interpolation works correctly
        _options.FanCurve.Sort((a, b) => a.TempC.CompareTo(b.TempC));
    }

    /// <summary>
    /// Maps a temperature to a fan percentage using linear interpolation.
    /// 
    /// Example with curve [50°→30%, 60°→50%]:
    ///   at 55° → returns 40%  (halfway between 30 and 50)
    /// </summary>
    public int GetFanPercent(float tempC)
    {
        List<FanCurvePoint> curve = _options.FanCurve;
        if (curve.Count == 0) return 20;

        // Below the first point → use minimum fan speed
        if (tempC <= curve[0].TempC) return curve[0].FanPercent;

        // Above the last point → run at 100%
        if (tempC >= curve[^1].TempC) return curve[^1].FanPercent;

        // Find the two curve points that straddle our temperature
        for (int i = 0; i < curve.Count - 1; i++)
        {
            FanCurvePoint low = curve[i];
            FanCurvePoint high = curve[i + 1];

            if (tempC >= low.TempC && tempC <= high.TempC)
            {
                // Linear interpolation formula: y = y0 + (x-x0) * (y1-y0)/(x1-x0)
                float ratio = (tempC - low.TempC) / (high.TempC - low.TempC);
                return (int)(low.FanPercent + ratio * (high.FanPercent - low.FanPercent));
            }
        }

        return curve[^1].FanPercent;
    }
}