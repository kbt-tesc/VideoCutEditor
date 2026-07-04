namespace VideoCutEditor.Core.Models;

public sealed record FfmpegCapabilities(IReadOnlySet<string> Encoders)
{
    public bool SupportsEncoder(string encoderName) => Encoders.Contains(encoderName);

    public bool SupportsNvenc(CodecFamily codecFamily) =>
        SupportsEncoder(GetNvencEncoderName(codecFamily));

    public string? ChooseVideoEncoder(CodecFamily codecFamily, EncoderKind encoderKind)
    {
        return encoderKind switch
        {
            EncoderKind.Nvenc => SupportsNvenc(codecFamily) ? GetNvencEncoderName(codecFamily) : null,
            EncoderKind.Software => ChooseSoftwareEncoder(codecFamily),
            _ => SupportsNvenc(codecFamily)
                ? GetNvencEncoderName(codecFamily)
                : ChooseSoftwareEncoder(codecFamily),
        };
    }

    private string? ChooseSoftwareEncoder(CodecFamily codecFamily)
    {
        foreach (string encoderName in GetSoftwareEncoderPreferences(codecFamily))
        {
            if (SupportsEncoder(encoderName))
            {
                return encoderName;
            }
        }

        return null;
    }

    private static string GetNvencEncoderName(CodecFamily codecFamily) =>
        codecFamily switch
        {
            CodecFamily.H264 => "h264_nvenc",
            CodecFamily.H265 => "hevc_nvenc",
            CodecFamily.Av1 => "av1_nvenc",
            _ => throw new ArgumentOutOfRangeException(nameof(codecFamily)),
        };

    private static IReadOnlyList<string> GetSoftwareEncoderPreferences(CodecFamily codecFamily) =>
        codecFamily switch
        {
            CodecFamily.H264 => ["libx264"],
            CodecFamily.H265 => ["libx265"],
            CodecFamily.Av1 => ["libsvtav1", "libaom-av1"],
            _ => throw new ArgumentOutOfRangeException(nameof(codecFamily)),
        };
}
