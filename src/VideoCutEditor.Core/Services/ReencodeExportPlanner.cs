using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class ReencodeExportPlanner : IExportPlanner
{
    private const int DefaultVideoBitrateKbps = 2500;

    private readonly FfmpegCapabilities capabilities;

    public ReencodeExportPlanner(FfmpegCapabilities capabilities)
    {
        this.capabilities = capabilities;
    }

    public ExportPlan CreatePlan(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Range.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Settings.FfmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);

        string? videoEncoder = capabilities.ChooseVideoEncoder(
            request.Settings.LastCodecFamily,
            request.Settings.LastEncoderKind);

        if (videoEncoder is null)
        {
            throw new InvalidOperationException(
                $"No supported video encoder is available for {request.Settings.LastCodecFamily} with {request.Settings.LastEncoderKind}.");
        }

        int videoBitrateKbps = request.Settings.LastVideoBitrateKbps.GetValueOrDefault(DefaultVideoBitrateKbps);
        string temporaryOutputPath = ExportPlanPathHelper.CreateTemporaryOutputPath(request.OutputPath);

        string[] arguments =
        [
            "-hide_banner",
            "-nostdin",
            "-y",
            "-ss",
            FastCopyExportPlanner.FormatTimestamp(request.Range.Start),
            "-i",
            request.SourcePath,
            "-t",
            FastCopyExportPlanner.FormatTimestamp(request.Range.Duration),
            "-map",
            "0",
            "-c",
            "copy",
            "-c:v",
            videoEncoder,
            "-b:v",
            $"{videoBitrateKbps}k",
            "-map_metadata",
            "0",
            "-avoid_negative_ts",
            "make_zero",
            temporaryOutputPath,
        ];

        return new ExportPlan(
            request.Settings.FfmpegPath!,
            request.SourcePath,
            temporaryOutputPath,
            request.OutputPath,
            arguments);
    }
}
