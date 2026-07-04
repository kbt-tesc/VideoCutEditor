namespace VideoCutEditor.Core.Services;

public static class PredictedOutputSizeCalculator
{
    private const double DefaultContainerOverheadFraction = 0.02;

    public static long EstimateBytes(
        int videoBitrateKbps,
        long? audioBitrateBitsPerSecond,
        TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(videoBitrateKbps, 0);
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero.");
        }

        long videoBitrateBitsPerSecond = videoBitrateKbps * 1000L;
        long totalBitrateBitsPerSecond = videoBitrateBitsPerSecond + Math.Max(0, audioBitrateBitsPerSecond.GetValueOrDefault());
        double mediaBytes = totalBitrateBitsPerSecond * duration.TotalSeconds / 8.0;

        return (long)Math.Round(mediaBytes * (1 + DefaultContainerOverheadFraction), MidpointRounding.AwayFromZero);
    }
}
