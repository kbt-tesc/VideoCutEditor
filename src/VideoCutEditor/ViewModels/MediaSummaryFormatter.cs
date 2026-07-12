using VideoCutEditor.Core.Models;

namespace VideoCutEditor.ViewModels;

public static class MediaSummaryFormatter
{
    public static string Create(MediaInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        MediaStreamInfo? video = info.Streams.FirstOrDefault(stream => stream.CodecType == "video");
        MediaStreamInfo? audio = info.Streams.FirstOrDefault(stream => stream.CodecType == "audio");

        string videoText = video is null
            ? "映像: なし"
            : $"映像: {CreateVideoDescription(video)}";
        string audioText = audio is null
            ? "音声: なし"
            : $"音声: {CreateAudioDescription(audio)}";
        string bitrateText = info.Bitrate is { } bitrate
            ? $"ビットレート: {FormatBitrate(bitrate)}"
            : "ビットレート: 不明";

        var lines = new List<string>
        {
            $"長さ: {FormatTime(info.Duration)}",
            videoText,
            audioText,
            bitrateText,
        };

        if (video is not null)
        {
            lines.Add($"ダイナミックレンジ: {CreateDynamicRangeDescription(video)}");
            lines.Add($"色空間: {video.ColorSpace ?? "不明"}");
            lines.Add($"伝達特性: {video.ColorTransfer ?? "不明"}");
            lines.Add($"色域: {video.ColorPrimaries ?? "不明"}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string CreateDynamicRangeDescription(MediaStreamInfo stream) =>
        stream.ColorTransfer?.ToLowerInvariant() switch
        {
            "smpte2084" => "HDR10 (PQ)",
            "arib-std-b67" => "HLG",
            _ => "SDR / 不明",
        };

    private static string CreateVideoDescription(MediaStreamInfo stream)
    {
        string codec = stream.CodecName ?? "unknown";
        string size = stream is { Width: > 0, Height: > 0 }
            ? $"{stream.Width}x{stream.Height}"
            : "サイズ不明";
        string fps = stream.FrameRate is { } frameRate
            ? $"{frameRate:0.###} fps"
            : "fps 不明";

        return $"{codec}, {size}, {fps}";
    }

    private static string CreateAudioDescription(MediaStreamInfo stream)
    {
        string codec = stream.CodecName ?? "不明";
        string channels = stream.ChannelLayout
            ?? (stream.Channels is { } channelCount ? $"{channelCount} ch" : "チャンネル不明");
        string sampleRate = stream.SampleRate is { } rate
            ? $"{rate / 1000.0:0.#} kHz"
            : "サンプルレート不明";

        return $"{codec}, {channels}, {sampleRate}";
    }

    private static string FormatBitrate(long bitsPerSecond) =>
        bitsPerSecond >= 1_000_000
            ? $"{bitsPerSecond / 1_000_000.0:0.##} Mbps"
            : $"{bitsPerSecond / 1000.0:0.#} kbps";

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss\.fff")
            : time.ToString(@"m\:ss\.fff");
    }
}
