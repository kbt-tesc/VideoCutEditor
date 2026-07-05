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

    public static int EstimateVideoBitrateKbps(
        long targetBytes,
        long? audioBitrateBitsPerSecond,
        TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetBytes, 0);
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero.");
        }

        double targetMediaBytes = targetBytes / (1 + DefaultContainerOverheadFraction);
        double totalBitrateBitsPerSecond = targetMediaBytes * 8 / duration.TotalSeconds;
        double videoBitrateBitsPerSecond = totalBitrateBitsPerSecond - Math.Max(0, audioBitrateBitsPerSecond.GetValueOrDefault());
        if (videoBitrateBitsPerSecond <= 0)
        {
            throw new InvalidOperationException("Target size is too small for the selected duration and audio bitrate.");
        }

        return (int)Math.Round(videoBitrateBitsPerSecond / 1000, MidpointRounding.AwayFromZero);
    }
}
