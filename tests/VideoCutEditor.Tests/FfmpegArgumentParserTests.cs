using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class FfmpegArgumentParserTests
{
    [Fact]
    public void Parse_returns_empty_arguments_for_blank_input()
    {
        IReadOnlyList<string> arguments = FfmpegArgumentParser.Parse("   ");

        Assert.Empty(arguments);
    }

    [Fact]
    public void Parse_splits_arguments_by_whitespace()
    {
        IReadOnlyList<string> arguments = FfmpegArgumentParser.Parse("-preset slow -movflags +faststart");

        Assert.Equal(["-preset", "slow", "-movflags", "+faststart"], arguments);
    }

    [Fact]
    public void Parse_preserves_quoted_values_as_single_arguments()
    {
        IReadOnlyList<string> arguments = FfmpegArgumentParser.Parse("-metadata title=\"Sample clip\" -tag:v hvc1");

        Assert.Equal(["-metadata", "title=Sample clip", "-tag:v", "hvc1"], arguments);
    }

    [Fact]
    public void Parse_rejects_unterminated_quotes()
    {
        FormatException exception = Assert.Throws<FormatException>(() => FfmpegArgumentParser.Parse("-metadata title=\"Sample clip"));

        Assert.Contains("unterminated quote", exception.Message);
    }
}
