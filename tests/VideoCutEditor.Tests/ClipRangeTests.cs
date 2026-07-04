using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Tests;

public sealed class ClipRangeTests
{
    [Fact]
    public void Duration_returns_end_minus_start()
    {
        var range = new ClipRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(42));

        Assert.Equal(TimeSpan.FromSeconds(32), range.Duration);
        Assert.True(range.IsValid);
    }

    [Fact]
    public void Validate_rejects_end_before_start()
    {
        var range = new ClipRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(9));

        Assert.Throws<ArgumentException>(() => range.Validate());
    }
}
