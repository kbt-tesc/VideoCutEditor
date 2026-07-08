using System.Text;

namespace VideoCutEditor.Core.Services;

public static class FfmpegArgumentParser
{
    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var arguments = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        bool hasCurrentArgument = false;

        foreach (char character in value)
        {
            if (quote is null && char.IsWhiteSpace(character))
            {
                AddCurrentArgumentIfNeeded(arguments, current, ref hasCurrentArgument);
                continue;
            }

            if (character is '"' or '\'')
            {
                if (quote is null)
                {
                    quote = character;
                    hasCurrentArgument = true;
                    continue;
                }

                if (quote == character)
                {
                    quote = null;
                    hasCurrentArgument = true;
                    continue;
                }
            }

            current.Append(character);
            hasCurrentArgument = true;
        }

        if (quote is not null)
        {
            throw new FormatException("Additional ffmpeg arguments contain an unterminated quote.");
        }

        AddCurrentArgumentIfNeeded(arguments, current, ref hasCurrentArgument);
        return arguments;
    }

    private static void AddCurrentArgumentIfNeeded(
        List<string> arguments,
        StringBuilder current,
        ref bool hasCurrentArgument)
    {
        if (!hasCurrentArgument)
        {
            return;
        }

        arguments.Add(current.ToString());
        current.Clear();
        hasCurrentArgument = false;
    }
}
