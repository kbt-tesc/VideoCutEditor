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

    public static void Validate(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        foreach (string argument in arguments)
        {
            string optionName = GetOptionName(argument);
            if (AppManagedOptions.Contains(optionName))
            {
                throw new InvalidOperationException(
                    $"Additional ffmpeg argument '{optionName}' is managed by VideoCutEditor. Remove it from the additional arguments field.");
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
}
