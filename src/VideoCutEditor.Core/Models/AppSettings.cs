namespace VideoCutEditor.Core.Models;

public sealed record AppSettings
{
    public string? FfmpegPath { get; init; }

    public string? FfprobePath { get; init; }

    public string? OutputDirectory { get; init; }

    public ExportMode LastExportMode { get; init; } = ExportMode.FastCopy;

    public CodecFamily LastCodecFamily { get; init; } = CodecFamily.H264;

    public EncoderKind LastEncoderKind { get; init; } = EncoderKind.Auto;

    public BitrateMode LastBitrateMode { get; init; } = BitrateMode.Bitrate;

    public int? LastVideoBitrateKbps { get; init; }
}
