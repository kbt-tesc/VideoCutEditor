namespace VideoCutEditor.Core.Services;

public static class FfmpegAdditionalArgumentValidator
{
    private static readonly HashSet<string> AppManagedOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "-avoid_negative_ts",
        "-b:v",
        "-c",
        "-c:a",
        "-c:v",
        "-codec",
        "-codec:a",
        "-codec:v",
        "-cq",
        "-crf",
        "-filter:a",
        "-filter:v",
        "-i",
        "-map",
        "-map_metadata",
        "-n",
        "-nostdin",
        "-ss",
        "-t",
        "-to",
        "-vf",
        "-af",
        "-y",
    };

    private static readonly HashSet<string> KnownValueOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "-bufsize",
        "-g",
        "-level",
        "-maxrate",
        "-metadata",
        "-movflags",
        "-pix_fmt",
        "-preset",
        "-profile:v",
        "-r",
        "-tag:v",
        "-threads",
    };

    public static void Validate(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            string optionName = GetOptionName(argument);
            if (AppManagedOptions.Contains(optionName))
            {
                throw new InvalidOperationException(
                    $"Additional ffmpeg argument '{optionName}' is managed by VideoCutEditor. Remove it from the additional arguments field.");
            }

            if (!LooksLikeOption(argument))
            {
                if (index == 0 || !LooksLikeOption(arguments[index - 1]))
                {
                    throw new InvalidOperationException(
                        $"Additional ffmpeg argument value '{argument}' does not belong to an option.");
                }

                continue;
            }

            if (KnownValueOptions.Contains(optionName) && !HasInlineValue(argument) && !HasFollowingValue(arguments, index))
            {
                throw new InvalidOperationException(
                    $"Additional ffmpeg argument '{optionName}' requires a value.");
            }
        }
    }

    private static string GetOptionName(string argument)
    {
        int equalsIndex = argument.IndexOf('=');
        return equalsIndex > 0
            ? argument[..equalsIndex]
            : argument;
    }

    private static bool HasInlineValue(string argument) =>
        argument.IndexOf('=') > 0;

    private static bool HasFollowingValue(IReadOnlyList<string> arguments, int optionIndex) =>
        optionIndex + 1 < arguments.Count && !LooksLikeOption(arguments[optionIndex + 1]);

    private static bool LooksLikeOption(string argument)
    {
        if (!argument.StartsWith('-') || argument.Length == 1)
        {
            return false;
        }

        return !double.TryParse(argument, out _);
    }
}
