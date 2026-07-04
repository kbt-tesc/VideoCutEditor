using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class PredictedOutputSizeCalculatorTests
{
    [Fact]
    public void EstimateBytes_includes_video_audio_and_container_overhead()
    {
        long bytes = PredictedOutputSizeCalculator.EstimateBytes(
            videoBitrateKbps: 2500,
            audioBitrateBitsPerSecond: 128_000,
            duration: TimeSpan.FromSeconds(60));

        Assert.Equal(20_104_200, bytes);
    }

    [Fact]
    public void EstimateBytes_treats_missing_audio_as_video_only()
    {
        long bytes = PredictedOutputSizeCalculator.EstimateBytes(
            videoBitrateKbps: 1000,
            audioBitrateBitsPerSecond: null,
            duration: TimeSpan.FromSeconds(8));

        Assert.Equal(1_020_000, bytes);
    }

    [Fact]
    public void EstimateBytes_rejects_non_positive_duration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PredictedOutputSizeCalculator.EstimateBytes(
                videoBitrateKbps: 2500,
                audioBitrateBitsPerSecond: 128_000,
                duration: TimeSpan.Zero));
    }
}
