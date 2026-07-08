using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class FfmpegAdditionalArgumentValidatorTests
{
    [Fact]
    public void Validate_allows_metadata_and_muxer_options()
    {
        IReadOnlyList<string> arguments = FfmpegArgumentParser.Parse("-metadata title=\"Sample clip\" -movflags +faststart");

        FfmpegAdditionalArgumentValidator.Validate(arguments);
    }

    [Theory]
    [InlineData("-i")]
    [InlineData("-ss")]
    [InlineData("-t")]
    [InlineData("-map")]
    [InlineData("-c:v")]
    [InlineData("-b:v")]
    [InlineData("-vf")]
    [InlineData("-af")]
    [InlineData("-y")]
    [InlineData("-avoid_negative_ts")]
    public void Validate_rejects_app_managed_options(string option)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => FfmpegAdditionalArgumentValidator.Validate([option, "value"]));

        Assert.Contains(option, exception.Message);
        Assert.Contains("managed by VideoCutEditor", exception.Message);
    }

    [Fact]
    public void Validate_rejects_app_managed_options_with_inline_values()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => FfmpegAdditionalArgumentValidator.Validate(["-ss=00:00:01"]));

        Assert.Contains("-ss", exception.Message);
    }
}
