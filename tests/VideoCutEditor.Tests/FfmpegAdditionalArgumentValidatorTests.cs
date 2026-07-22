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
    [InlineData("-b:a")]
    [InlineData("-q:a")]
    [InlineData("-vbr")]
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

    [Fact]
    public void Validate_rejects_bare_values_without_an_option()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => FfmpegAdditionalArgumentValidator.Validate(["slow"]));

        Assert.Contains("does not belong to an option", exception.Message);
    }

    [Theory]
    [InlineData("-preset")]
    [InlineData("-metadata")]
    [InlineData("-movflags")]
    public void Validate_rejects_known_value_options_without_values(string option)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => FfmpegAdditionalArgumentValidator.Validate([option]));

        Assert.Contains(option, exception.Message);
        Assert.Contains("requires a value", exception.Message);
    }

    [Fact]
    public void Validate_rejects_known_value_options_followed_by_another_option()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => FfmpegAdditionalArgumentValidator.Validate(["-preset", "-movflags", "+faststart"]));

        Assert.Contains("-preset", exception.Message);
        Assert.Contains("requires a value", exception.Message);
    }

    [Fact]
    public void Validate_allows_inline_values_for_known_value_options()
    {
        FfmpegAdditionalArgumentValidator.Validate(["-metadata=title=Sample clip"]);
    }
}
