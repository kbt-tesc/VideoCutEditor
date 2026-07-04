using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace VideoCutEditor.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ISettingsService settingsService;
    private readonly OutputPathService outputPathService;
    private readonly IFfmpegToolPathService toolPathService;
    private readonly IFfmpegRunner ffmpegRunner;
    private readonly IMediaProbeService mediaProbeService;
    private readonly IFfmpegCapabilityService ffmpegCapabilityService;
    private CancellationTokenSource? mediaProbeCancellation;
    private CancellationTokenSource? exportCancellation;
    private FfmpegCapabilities currentCapabilities = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    private bool isUpdatingRangeText;

    [ObservableProperty]
    public partial string? SelectedSourcePath { get; set; }

    [ObservableProperty]
    public partial string SelectedFileName { get; set; } = "No video selected";

    [ObservableProperty]
    public partial string? PlannedOutputPath { get; set; }

    [ObservableProperty]
    public partial string MediaSummaryText { get; set; } = "No media loaded";

    [ObservableProperty]
    public partial string EncoderSummaryText { get; set; } = "Encoder capabilities not detected";

    [ObservableProperty]
    public partial MediaInfo? CurrentMediaInfo { get; set; }

    [ObservableProperty]
    public partial MediaSource? PreviewSource { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewPlaceholderVisible { get; set; } = true;

    [ObservableProperty]
    public partial string? FfmpegPath { get; set; }

    [ObservableProperty]
    public partial string? FfprobePath { get; set; }

    [ObservableProperty]
    public partial string? OutputDirectory { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready";

    [ObservableProperty]
    public partial string RangeStartText { get; set; } = "00:00:00";

    [ObservableProperty]
    public partial string RangeEndText { get; set; } = "00:00:00";

    [ObservableProperty]
    public partial double PositionSeconds { get; set; }

    [ObservableProperty]
    public partial double DurationSeconds { get; set; }

    [ObservableProperty]
    public partial double RangeStartSeconds { get; set; }

    [ObservableProperty]
    public partial double RangeEndSeconds { get; set; }

    [ObservableProperty]
    public partial double TimelineZoom { get; set; } = 1;

    [ObservableProperty]
    public partial double PlaybackRate { get; set; } = 1;

    [ObservableProperty]
    public partial double FrameStepSeconds { get; set; } = 1.0 / 30.0;

    [ObservableProperty]
    public partial double TimelineViewportWidth { get; set; } = 720;

    [ObservableProperty]
    public partial bool IsExporting { get; set; }

    [ObservableProperty]
    public partial double ExportProgressValue { get; set; }

    [ObservableProperty]
    public partial bool IsExportProgressIndeterminate { get; set; }

    [ObservableProperty]
    public partial string ExportLogText { get; set; } = "No export log yet.";

    [ObservableProperty]
    public partial bool HasExportLog { get; set; }

    [ObservableProperty]
    public partial int SelectedExportModeIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedCodecFamilyIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedEncoderKindIndex { get; set; }

    [ObservableProperty]
    public partial string VideoBitrateText { get; set; } = "2500";

    public double TimelineMaximum => Math.Max(DurationSeconds, 0.001);

    public string PositionText => FormatTime(TimeSpan.FromSeconds(PositionSeconds));

    public string DurationText => FormatTime(TimeSpan.FromSeconds(DurationSeconds));

    public string RangeStartDisplayText => FormatTime(TimeSpan.FromSeconds(RangeStartSeconds));

    public string RangeEndDisplayText => FormatTime(TimeSpan.FromSeconds(RangeEndSeconds));

    public string TimelineZoomText => $"{TimelineZoom:0.0}x";

    public string PlaybackRateText => $"{PlaybackRate:0.##}x";

    public double TimelineContentWidth => Math.Max(TimelineViewportWidth, 1) * TimelineZoom;

    public MainPageViewModel()
        : this(
            new JsonSettingsService(),
            new OutputPathService(),
            new FfmpegToolPathService(),
            new FfmpegRunner(),
            new MediaProbeService(),
            new FfmpegCapabilityService())
    {
    }

    public MainPageViewModel(
        ISettingsService settingsService,
        OutputPathService outputPathService,
        IFfmpegToolPathService toolPathService,
        IFfmpegRunner? ffmpegRunner = null,
        IMediaProbeService? mediaProbeService = null,
        IFfmpegCapabilityService? ffmpegCapabilityService = null)
    {
        this.settingsService = settingsService;
        this.outputPathService = outputPathService;
        this.toolPathService = toolPathService;
        this.ffmpegRunner = ffmpegRunner ?? new FfmpegRunner();
        this.mediaProbeService = mediaProbeService ?? new MediaProbeService();
        this.ffmpegCapabilityService = ffmpegCapabilityService ?? new FfmpegCapabilityService();
    }

    public async Task InitializeAsync()
    {
        AppLogger.Info("MainPageViewModel InitializeAsync starting");
        AppSettings settings = await settingsService.LoadAsync();
        FfmpegToolPaths paths = toolPathService.Resolve(settings);

        FfmpegPath = paths.FfmpegPath;
        FfprobePath = paths.FfprobePath;
        OutputDirectory = settings.OutputDirectory;
        ApplyExportSettings(settings);
        StatusMessage = CreateToolDetectionStatus(paths);
        await DetectEncoderCapabilitiesAsync();
        AppLogger.Info($"Tool paths resolved. ffmpeg={FfmpegPath ?? "(null)"}, ffprobe={FfprobePath ?? "(null)"}");
    }

    [RelayCommand]
    private async Task OpenVideoAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
        };
        picker.FileTypeFilter.Add(".mp4");
        picker.FileTypeFilter.Add(".mkv");
        picker.FileTypeFilter.Add(".mov");
        picker.FileTypeFilter.Add(".avi");
        picker.FileTypeFilter.Add(".webm");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();

        if (file is null)
        {
            return;
        }

        await OpenVideoFileAsync(file);
    }

    public async Task OpenVideoFileAsync(StorageFile file)
    {
        AppLogger.Info($"OpenVideoFileAsync: {file.Path}");
        mediaProbeCancellation?.Cancel();
        SelectedSourcePath = file.Path;
        SelectedFileName = file.Name;
        PreviewSource = MediaSource.CreateFromStorageFile(file);
        StatusMessage = "Video selected; probing media...";
        MediaSummaryText = "Probing media...";
        CurrentMediaInfo = null;
        PositionSeconds = 0;
        DurationSeconds = 0;
        RangeStartSeconds = 0;
        RangeEndSeconds = 0;
        FrameStepSeconds = 1.0 / 30.0;
        SetRangeTextWithoutParsing(TimeSpan.Zero, TimeSpan.Zero);
        UpdatePlannedOutputPath();

        await ProbeSelectedMediaAsync(file.Path);
    }

    public void SetMediaDuration(TimeSpan duration)
    {
        DurationSeconds = Math.Max(0, duration.TotalSeconds);
        if (RangeEndSeconds <= 0 && DurationSeconds > 0)
        {
            RangeEndSeconds = DurationSeconds;
            SetRangeTextWithoutParsing(TimeSpan.FromSeconds(RangeStartSeconds), duration);
        }
    }

    public TimeSpan GetCurrentPosition() => TimeSpan.FromSeconds(PositionSeconds);

    public TimeSpan GetFrameStep() => TimeSpan.FromSeconds(FrameStepSeconds);

    [RelayCommand]
    private async Task BrowseOutputDirectoryAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
        };
        picker.FileTypeFilter.Add("*");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();

        if (folder is null)
        {
            return;
        }

        OutputDirectory = folder.Path;
        StatusMessage = "Output folder selected";
        UpdatePlannedOutputPath();
    }

    [RelayCommand]
    private async Task BrowseFfmpegPathAsync()
    {
        string? path = await PickExecutableAsync("Select ffmpeg.exe");
        if (path is null)
        {
            return;
        }

        FfmpegPath = path;
        StatusMessage = "ffmpeg selected";
        await DetectEncoderCapabilitiesAsync();
    }

    [RelayCommand]
    private async Task BrowseFfprobePathAsync()
    {
        string? path = await PickExecutableAsync("Select ffprobe.exe");
        if (path is null)
        {
            return;
        }

        FfprobePath = path;
        StatusMessage = "ffprobe selected";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await settingsService.SaveAsync(CreateCurrentSettings());
        StatusMessage = "Settings saved";
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        AppLogger.Info("Export requested");

        if (IsExporting)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedSourcePath) || !File.Exists(SelectedSourcePath))
        {
            StatusMessage = "Open a video before exporting";
            return;
        }

        if (string.IsNullOrWhiteSpace(FfmpegPath) || !File.Exists(FfmpegPath))
        {
            StatusMessage = "Choose a valid ffmpeg.exe before exporting";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory) || !Directory.Exists(OutputDirectory))
        {
            StatusMessage = "Choose a valid output folder before exporting";
            return;
        }

        ClipRange range = new(
            TimeSpan.FromSeconds(RangeStartSeconds),
            TimeSpan.FromSeconds(RangeEndSeconds));

        if (!range.IsValid)
        {
            StatusMessage = "Set an end time later than the start time";
            return;
        }

        if (CurrentExportMode == ExportMode.Reencode && GetVideoBitrateKbps() is null)
        {
            StatusMessage = "Set a valid video bitrate";
            return;
        }

        UpdatePlannedOutputPath();
        if (string.IsNullOrWhiteSpace(PlannedOutputPath))
        {
            StatusMessage = "Could not create an output path";
            return;
        }

        IsExporting = true;
        exportCancellation = new CancellationTokenSource();
        ExportProgressValue = 0;
        IsExportProgressIndeterminate = true;
        ExportLogText = "Starting export...";
        HasExportLog = true;
        StatusMessage = "Exporting...";

        try
        {
            AppSettings settings = CreateCurrentSettings();
            await settingsService.SaveAsync(settings);

            var request = new ExportRequest(SelectedSourcePath, PlannedOutputPath, range, settings);
            IExportPlanner planner = new ExportPlannerFactory(currentCapabilities).CreatePlanner(settings.LastExportMode);
            ExportPlan plan = planner.CreatePlan(request);
            var progress = new Progress<ExportProgress>(exportProgress =>
            {
                AppendExportLog(exportProgress.Status);

                if (exportProgress.Position is { } position)
                {
                    StatusMessage = $"Exporting {FormatTime(position)}";
                    if (range.Duration.TotalSeconds > 0)
                    {
                        IsExportProgressIndeterminate = false;
                        ExportProgressValue = Math.Clamp(position.TotalSeconds / range.Duration.TotalSeconds, 0, 1);
                    }
                }
                else if (exportProgress.Percent is { } percent)
                {
                    IsExportProgressIndeterminate = false;
                    ExportProgressValue = Math.Clamp(percent, 0, 1);
                }
            });

            ExportResult result = await ffmpegRunner.RunAsync(plan, progress, exportCancellation.Token);
            StatusMessage = result.Succeeded
                ? $"Export complete: {Path.GetFileName(plan.FinalOutputPath)}"
                : result.ErrorMessage ?? "Export failed";
            ExportProgressValue = result.Succeeded ? 1 : ExportProgressValue;
            IsExportProgressIndeterminate = false;

            if (result.Succeeded)
            {
                PlannedOutputPath = plan.FinalOutputPath;
            }

            AppLogger.Info($"Export completed. Success={result.Succeeded}, Output={plan.FinalOutputPath}, Error={result.ErrorMessage}");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            AppLogger.Error("Export failed", exception);
        }
        finally
        {
            IsExporting = false;
            exportCancellation.Dispose();
            exportCancellation = null;
        }
    }

    [RelayCommand]
    private void CancelExport()
    {
        if (!IsExporting || exportCancellation is null)
        {
            return;
        }

        StatusMessage = "Canceling export...";
        AppendExportLog("Cancel requested.");
        exportCancellation.Cancel();
    }

    [RelayCommand]
    private void MarkStart()
    {
        RangeStartSeconds = ClampToDuration(PositionSeconds);
        SetRangeTextWithoutParsing(TimeSpan.FromSeconds(RangeStartSeconds), TimeSpan.FromSeconds(RangeEndSeconds));
        StatusMessage = "Start set";
    }

    [RelayCommand]
    private void MarkEnd()
    {
        RangeEndSeconds = ClampToDuration(PositionSeconds);
        SetRangeTextWithoutParsing(TimeSpan.FromSeconds(RangeStartSeconds), TimeSpan.FromSeconds(RangeEndSeconds));
        StatusMessage = "End set";
    }

    partial void OnOutputDirectoryChanged(string? value)
    {
        UpdatePlannedOutputPath();
    }

    partial void OnPreviewSourceChanged(MediaSource? value)
    {
        IsPreviewPlaceholderVisible = value is null;
    }

    partial void OnPositionSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(PositionText));
    }

    partial void OnDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(TimelineMaximum));
        OnPropertyChanged(nameof(DurationText));
    }

    partial void OnRangeStartSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(RangeStartDisplayText));
    }

    partial void OnRangeEndSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(RangeEndDisplayText));
    }

    partial void OnRangeStartTextChanged(string value)
    {
        if (isUpdatingRangeText)
        {
            return;
        }

        if (TryParseTime(value, out TimeSpan time))
        {
            RangeStartSeconds = ClampToDuration(time.TotalSeconds);
        }
    }

    partial void OnRangeEndTextChanged(string value)
    {
        if (isUpdatingRangeText)
        {
            return;
        }

        if (TryParseTime(value, out TimeSpan time))
        {
            RangeEndSeconds = ClampToDuration(time.TotalSeconds);
        }
    }

    partial void OnTimelineZoomChanged(double value)
    {
        OnPropertyChanged(nameof(TimelineZoomText));
        OnPropertyChanged(nameof(TimelineContentWidth));
    }

    partial void OnTimelineViewportWidthChanged(double value)
    {
        OnPropertyChanged(nameof(TimelineContentWidth));
    }

    partial void OnPlaybackRateChanged(double value)
    {
        OnPropertyChanged(nameof(PlaybackRateText));
    }

    partial void OnIsExportingChanged(bool value)
    {
        CancelExportCommand.NotifyCanExecuteChanged();
    }

    private void UpdatePlannedOutputPath()
    {
        if (string.IsNullOrWhiteSpace(SelectedSourcePath) || string.IsNullOrWhiteSpace(OutputDirectory))
        {
            PlannedOutputPath = null;
            return;
        }

        PlannedOutputPath = outputPathService.CreateAvailableCutPath(SelectedSourcePath, OutputDirectory);
    }

    private ExportMode CurrentExportMode => SelectedExportModeIndex == 1
        ? ExportMode.Reencode
        : ExportMode.FastCopy;

    private CodecFamily CurrentCodecFamily => SelectedCodecFamilyIndex switch
    {
        1 => CodecFamily.H265,
        2 => CodecFamily.Av1,
        _ => CodecFamily.H264,
    };

    private EncoderKind CurrentEncoderKind => SelectedEncoderKindIndex switch
    {
        1 => EncoderKind.Nvenc,
        2 => EncoderKind.Software,
        _ => EncoderKind.Auto,
    };

    private AppSettings CreateCurrentSettings() => new()
    {
        FfmpegPath = FfmpegPath,
        FfprobePath = FfprobePath,
        OutputDirectory = OutputDirectory,
        LastExportMode = CurrentExportMode,
        LastCodecFamily = CurrentCodecFamily,
        LastEncoderKind = CurrentEncoderKind,
        LastBitrateMode = BitrateMode.Bitrate,
        LastVideoBitrateKbps = GetVideoBitrateKbps(),
    };

    private void ApplyExportSettings(AppSettings settings)
    {
        SelectedExportModeIndex = settings.LastExportMode == ExportMode.Reencode ? 1 : 0;
        SelectedCodecFamilyIndex = settings.LastCodecFamily switch
        {
            CodecFamily.H265 => 1,
            CodecFamily.Av1 => 2,
            _ => 0,
        };
        SelectedEncoderKindIndex = settings.LastEncoderKind switch
        {
            EncoderKind.Nvenc => 1,
            EncoderKind.Software => 2,
            _ => 0,
        };
        VideoBitrateText = settings.LastVideoBitrateKbps.GetValueOrDefault(2500).ToString();
    }

    private int? GetVideoBitrateKbps()
    {
        return int.TryParse(VideoBitrateText, out int bitrateKbps) && bitrateKbps > 0
            ? bitrateKbps
            : null;
    }

    private static async Task<string?> PickExecutableAsync(string commitButtonText)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            CommitButtonText = commitButtonText,
        };
        picker.FileTypeFilter.Add(".exe");

        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();

        return file?.Path;
    }

    private static string CreateToolDetectionStatus(FfmpegToolPaths paths)
    {
        return (paths.FfmpegPath, paths.FfprobePath) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => "ffmpeg and ffprobe detected",
            ({ Length: > 0 }, _) => "ffmpeg detected; ffprobe missing",
            (_, { Length: > 0 }) => "ffprobe detected; ffmpeg missing",
            _ => "Ready",
        };
    }

    private double ClampToDuration(double seconds)
    {
        if (DurationSeconds <= 0)
        {
            return Math.Max(0, seconds);
        }

        return Math.Clamp(seconds, 0, DurationSeconds);
    }

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

    private void SetRangeTextWithoutParsing(TimeSpan start, TimeSpan end)
    {
        isUpdatingRangeText = true;
        RangeStartText = FormatTime(start);
        RangeEndText = FormatTime(end);
        isUpdatingRangeText = false;
    }

    private static bool TryParseTime(string value, out TimeSpan time)
    {
        time = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 1 && double.TryParse(parts[0], out double seconds))
        {
            time = TimeSpan.FromSeconds(seconds);
            return true;
        }

        if (parts.Length == 2
            && int.TryParse(parts[0], out int minutes)
            && double.TryParse(parts[1], out seconds))
        {
            time = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
            return true;
        }

        if (parts.Length == 3
            && int.TryParse(parts[0], out int hours)
            && int.TryParse(parts[1], out minutes)
            && double.TryParse(parts[2], out seconds))
        {
            time = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
            return true;
        }

        return false;
    }

    private async Task ProbeSelectedMediaAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(FfprobePath) || !File.Exists(FfprobePath))
        {
            MediaSummaryText = "Media info unavailable: ffprobe is missing";
            StatusMessage = "Video selected; ffprobe missing";
            return;
        }

        mediaProbeCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = mediaProbeCancellation.Token;

        try
        {
            MediaInfo info = await mediaProbeService.ProbeAsync(FfprobePath, sourcePath, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            CurrentMediaInfo = info;
            ApplyMediaInfo(info);
            MediaSummaryText = CreateMediaSummary(info);
            StatusMessage = "Video selected";
            AppLogger.Info($"ffprobe metadata loaded: {MediaSummaryText.Replace(Environment.NewLine, " | ")}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            MediaSummaryText = $"Media info unavailable: {exception.Message}";
            StatusMessage = "ffprobe metadata unavailable";
            AppLogger.Error("ffprobe metadata unavailable", exception);
        }
    }

    private void ApplyMediaInfo(MediaInfo info)
    {
        if (info.Duration > TimeSpan.Zero)
        {
            SetMediaDuration(info.Duration);
        }

        MediaStreamInfo? videoStream = info.Streams.FirstOrDefault(stream => stream.CodecType == "video");
        if (videoStream?.FrameRate is > 0)
        {
            FrameStepSeconds = 1.0 / videoStream.FrameRate.Value;
        }
    }

    private static string CreateMediaSummary(MediaInfo info)
    {
        MediaStreamInfo? video = info.Streams.FirstOrDefault(stream => stream.CodecType == "video");
        MediaStreamInfo? audio = info.Streams.FirstOrDefault(stream => stream.CodecType == "audio");

        string duration = FormatTime(info.Duration);
        string videoText = video is null
            ? "Video: none"
            : $"Video: {CreateVideoDescription(video)}";
        string audioText = audio is null
            ? "Audio: none"
            : $"Audio: {CreateAudioDescription(audio)}";
        string bitrateText = info.Bitrate is { } bitrate
            ? $"Bitrate: {FormatBitrate(bitrate)}"
            : "Bitrate: unknown";

        return $"Duration: {duration}{Environment.NewLine}{videoText}{Environment.NewLine}{audioText}{Environment.NewLine}{bitrateText}";
    }

    private static string CreateVideoDescription(MediaStreamInfo stream)
    {
        string codec = stream.CodecName ?? "unknown";
        string size = stream is { Width: > 0, Height: > 0 }
            ? $"{stream.Width}x{stream.Height}"
            : "unknown size";
        string fps = stream.FrameRate is { } frameRate
            ? $"{frameRate:0.###} fps"
            : "unknown fps";

        return $"{codec}, {size}, {fps}";
    }

    private static string CreateAudioDescription(MediaStreamInfo stream)
    {
        string codec = stream.CodecName ?? "unknown";
        string channels = stream.ChannelLayout
            ?? (stream.Channels is { } channelCount ? $"{channelCount} ch" : "unknown channels");
        string sampleRate = stream.SampleRate is { } rate
            ? $"{rate / 1000.0:0.#} kHz"
            : "unknown sample rate";

        return $"{codec}, {channels}, {sampleRate}";
    }

    private static string FormatBitrate(long bitsPerSecond)
    {
        return bitsPerSecond >= 1_000_000
            ? $"{bitsPerSecond / 1_000_000.0:0.##} Mbps"
            : $"{bitsPerSecond / 1000.0:0.#} kbps";
    }

    private async Task DetectEncoderCapabilitiesAsync()
    {
        if (string.IsNullOrWhiteSpace(FfmpegPath) || !File.Exists(FfmpegPath))
        {
            currentCapabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            EncoderSummaryText = "Encoder capabilities unavailable: ffmpeg is missing";
            return;
        }

        try
        {
            FfmpegCapabilities capabilities = await ffmpegCapabilityService.DetectAsync(FfmpegPath);
            currentCapabilities = capabilities;
            EncoderSummaryText = CreateEncoderSummary(capabilities);
            AppLogger.Info($"Encoder capabilities loaded: {EncoderSummaryText.Replace(Environment.NewLine, " | ")}");
        }
        catch (Exception exception)
        {
            currentCapabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            EncoderSummaryText = $"Encoder capabilities unavailable: {exception.Message}";
            AppLogger.Error("Encoder capability detection failed", exception);
        }
    }

    private static string CreateEncoderSummary(FfmpegCapabilities capabilities)
    {
        string h264 = CreateEncoderFamilySummary(capabilities, CodecFamily.H264, "H.264");
        string h265 = CreateEncoderFamilySummary(capabilities, CodecFamily.H265, "H.265");
        string av1 = CreateEncoderFamilySummary(capabilities, CodecFamily.Av1, "AV1");

        return $"{h264}{Environment.NewLine}{h265}{Environment.NewLine}{av1}";
    }

    private static string CreateEncoderFamilySummary(
        FfmpegCapabilities capabilities,
        CodecFamily codecFamily,
        string label)
    {
        string? autoEncoder = capabilities.ChooseVideoEncoder(codecFamily, EncoderKind.Auto);
        string nvenc = capabilities.SupportsNvenc(codecFamily) ? "NVEnc available" : "NVEnc unavailable";
        return autoEncoder is null
            ? $"{label}: no supported encoder detected ({nvenc})"
            : $"{label}: {autoEncoder} ({nvenc})";
    }

    private void AppendExportLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        string timestampedLine = $"[{DateTimeOffset.Now:HH:mm:ss}] {line.Trim()}";
        if (string.IsNullOrWhiteSpace(ExportLogText) || ExportLogText == "No export log yet.")
        {
            ExportLogText = timestampedLine;
        }
        else
        {
            ExportLogText = $"{ExportLogText}{Environment.NewLine}{timestampedLine}";
        }

        HasExportLog = true;
    }
}
