using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class MediaProbeService : IMediaProbeService
{
    public async Task<MediaInfo> ProbeAsync(
        string ffprobePath,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffprobePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (!File.Exists(ffprobePath))
        {
            throw new FileNotFoundException("ffprobe was not found.", ffprobePath);
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source video was not found.", sourcePath);
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            },
        };

        foreach (string argument in CreateArguments(sourcePath))
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            string error = string.IsNullOrWhiteSpace(stderr)
                ? $"ffprobe failed with exit code {process.ExitCode}."
                : stderr.Trim();
            throw new InvalidOperationException(error);
        }

        return ParseJson(sourcePath, stdout);
    }

    public static MediaInfo ParseJson(string sourcePath, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        TimeSpan duration = TimeSpan.Zero;
        string? formatName = null;
        long? formatBitrate = null;

        if (root.TryGetProperty("format", out JsonElement format))
        {
            duration = TimeSpan.FromSeconds(GetDouble(format, "duration") ?? 0);
            formatName = GetString(format, "format_name");
            formatBitrate = GetLong(format, "bit_rate");
        }

        var streams = new List<MediaStreamInfo>();
        if (root.TryGetProperty("streams", out JsonElement streamsElement)
            && streamsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement stream in streamsElement.EnumerateArray())
            {
                streams.Add(ParseStream(stream));
            }
        }

        if (duration <= TimeSpan.Zero)
        {
            duration = streams
                .Select(stream => GetStreamDuration(root, stream.Index))
                .Where(value => value > TimeSpan.Zero)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();
        }

        return new MediaInfo(sourcePath, duration, formatName, formatBitrate, streams);
    }

    private static IReadOnlyList<string> CreateArguments(string sourcePath) =>
    [
        "-v",
        "error",
        "-show_format",
        "-show_streams",
        "-print_format",
        "json",
        sourcePath,
    ];

    private static MediaStreamInfo ParseStream(JsonElement stream)
    {
        int index = GetInt(stream, "index") ?? 0;
        string codecType = GetString(stream, "codec_type") ?? "unknown";
        string? codecName = GetString(stream, "codec_name");
        long? bitrate = GetLong(stream, "bit_rate");
        int? width = GetInt(stream, "width");
        int? height = GetInt(stream, "height");
        double? frameRate = ParseFrameRate(GetString(stream, "avg_frame_rate"))
            ?? ParseFrameRate(GetString(stream, "r_frame_rate"));
        int? sampleRate = GetInt(stream, "sample_rate");
        int? channels = GetInt(stream, "channels");
        string? channelLayout = GetString(stream, "channel_layout");
        string? colorSpace = GetString(stream, "color_space");
        string? colorTransfer = GetString(stream, "color_transfer");
        string? colorPrimaries = GetString(stream, "color_primaries");
        double sampleAspectRatio = ParseRatio(GetString(stream, "sample_aspect_ratio"), ':') is > 0 and double aspect
            ? aspect
            : 1;
        bool isAttachedPicture = stream.TryGetProperty("disposition", out JsonElement disposition)
            && GetInt(disposition, "attached_pic") == 1;

        return new MediaStreamInfo(
            index,
            codecType,
            codecName,
            bitrate,
            width,
            height,
            frameRate,
            sampleRate,
            channels,
            channelLayout,
            colorSpace,
            colorTransfer,
            colorPrimaries,
            sampleAspectRatio,
            GetRotationDegrees(stream),
            isAttachedPicture);
    }

    private static double GetRotationDegrees(JsonElement stream)
    {
        if (stream.TryGetProperty("side_data_list", out JsonElement sideData) && sideData.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in sideData.EnumerateArray())
            {
                if (GetDouble(item, "rotation") is { } rotation)
                {
                    return rotation;
                }
            }
        }

        return stream.TryGetProperty("tags", out JsonElement tags) ? GetDouble(tags, "rotate") ?? 0 : 0;
    }

    private static TimeSpan GetStreamDuration(JsonElement root, int streamIndex)
    {
        if (!root.TryGetProperty("streams", out JsonElement streamsElement)
            || streamsElement.ValueKind != JsonValueKind.Array)
        {
            return TimeSpan.Zero;
        }

        foreach (JsonElement stream in streamsElement.EnumerateArray())
        {
            if (GetInt(stream, "index") == streamIndex
                && GetDouble(stream, "duration") is { } duration)
            {
                return TimeSpan.FromSeconds(duration);
            }
        }

        return TimeSpan.Zero;
    }

    private static double? ParseFrameRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "0/0")
        {
            return null;
        }

        return ParseRatio(value, '/')
            ?? (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : null);
    }

    private static double? ParseRatio(string? value, char separator)
    {
        string[]? parts = value?.Split(separator);
        return parts is [string numerator, string denominator]
            && double.TryParse(numerator, NumberStyles.Float, CultureInfo.InvariantCulture, out double dividend)
            && double.TryParse(denominator, NumberStyles.Float, CultureInfo.InvariantCulture, out double divisor) && divisor > 0
            ? dividend / divisor
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null,
        };
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        string? value = GetString(element, propertyName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : null;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        string? value = GetString(element, propertyName);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result)
            ? result
            : null;
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        string? value = GetString(element, propertyName);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : null;
    }
}
