using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
    private readonly BitrateSuggestionService bitrateSuggestionService;
    private readonly Func<string, bool> outputDirectoryLauncher;
    private CancellationTokenSource? mediaProbeCancellation;
    private CancellationTokenSource? exportCancellation;
    private FfmpegCapabilities currentCapabilities = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    private bool isUpdatingRangeText;
    private bool isUpdatingSuggestedBitrate;
    private bool isUpdatingFadeDuration;
    private bool hasManualVideoBitrateOverride;

    [ObservableProperty]
    public partial string? SelectedSourcePath { get; set; }

    [ObservableProperty]
    public partial string SelectedFileName { get; set; } = "動画が選択されていません";

    [ObservableProperty]
    public partial string? PlannedOutputPath { get; set; }

    [ObservableProperty]
    public partial string MediaSummaryText { get; set; } = "メディア情報はまだありません";

    [ObservableProperty]
    public partial string EncoderSummaryText { get; set; } = "エンコーダー情報はまだ検出されていません";

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
    public partial string StatusMessage { get; set; } = "準備完了";

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
    public partial string ExportLogText { get; set; } = "書き出しログはまだありません。";

    [ObservableProperty]
    public partial bool HasExportLog { get; set; }

    [ObservableProperty]
    public partial int SelectedExportModeIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedCodecFamilyIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedEncoderKindIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedBitrateModeIndex { get; set; }

    [ObservableProperty]
    public partial string VideoBitrateText { get; set; } = "2500";

    [ObservableProperty]
    public partial double TargetSizeMegabytes { get; set; } = 100;

    [ObservableProperty]
    public partial double QualityValue { get; set; } = 23;

    [ObservableProperty]
    public partial string PredictedOutputSizeText { get; set; } = "推定サイズはまだ計算できません";

    [ObservableProperty]
    public partial bool NormalizeAudioEnabled { get; set; }

    [ObservableProperty]
    public partial string AdditionalFfmpegArgumentsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool VideoFadeInEnabled { get; set; }

    [ObservableProperty]
    public partial bool VideoFadeOutEnabled { get; set; }

    [ObservableProperty]
    public partial bool AudioFadeInEnabled { get; set; }

    [ObservableProperty]
    public partial bool AudioFadeOutEnabled { get; set; }

    [ObservableProperty]
    public partial double FadeDurationSeconds { get; set; } = 1;

    public double TimelineMaximum => Math.Max(DurationSeconds, 0.001);

    public string PositionText => FormatTime(TimeSpan.FromSeconds(PositionSeconds));

    public string DurationText => FormatTime(TimeSpan.FromSeconds(DurationSeconds));

    public string RangeStartDisplayText => FormatTime(TimeSpan.FromSeconds(RangeStartSeconds));

    public string RangeEndDisplayText => FormatTime(TimeSpan.FromSeconds(RangeEndSeconds));

    public string TimelineZoomText => $"{TimelineZoom:0.0}x";

    public string PlaybackRateText => $"{PlaybackRate:0.##}x";

    public double TimelineContentWidth => Math.Max(TimelineViewportWidth, 1) * TimelineZoom;

    public string ExportNoticeText => HasActiveFadeEnabled
        ? "フェードを使うため、フィルター処理が必要になり Re-encode で書き出します。"
        : NormalizeAudioEnabled
            ? "音量正規化は -14 LUFS を目標にし、音声を再エンコードします。"
            : CurrentExportMode == ExportMode.Reencode
                ? "Re-encode は選択したコーデックとレート制御設定で書き出します。"
                : "Fast copy は可能な限りストリームを保持しますが、カット位置は近いキーフレームに揃う場合があります。";

    public bool IsBitrateMode => CurrentBitrateMode == BitrateMode.Bitrate;

    public bool IsTargetSizeMode => CurrentBitrateMode == BitrateMode.TargetSize;

    public bool IsQualityMode => CurrentBitrateMode == BitrateMode.Quality;

    public bool IsReencodeSettingsVisible => CurrentExportMode == ExportMode.Reencode;

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
        IFfmpegCapabilityService? ffmpegCapabilityService = null,
        BitrateSuggestionService? bitrateSuggestionService = null,
        Func<string, bool>? outputDirectoryLauncher = null)
    {
        this.settingsService = settingsService;
        this.outputPathService = outputPathService;
        this.toolPathService = toolPathService;
        this.ffmpegRunner = ffmpegRunner ?? new FfmpegRunner();
        this.mediaProbeService = mediaProbeService ?? new MediaProbeService();
        this.ffmpegCapabilityService = ffmpegCapabilityService ?? new FfmpegCapabilityService();
        this.bitrateSuggestionService = bitrateSuggestionService ?? new BitrateSuggestionService();
        this.outputDirectoryLauncher = outputDirectoryLauncher ?? OpenDirectoryInShell;
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

        Windows.Storage.StorageFile? file;
        try
        {
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
            file = await picker.PickSingleFileAsync();
        }
        catch (COMException exception)
        {
            StatusMessage = "動画ファイルの選択を完了できませんでした";
            AppLogger.Error("Video picker failed", exception);
            return;
        }

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
        StatusMessage = "動画を選択しました。メディア情報を読み取っています...";
        MediaSummaryText = "メディア情報を読み取っています...";
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

        Windows.Storage.StorageFolder? folder;
        try
        {
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
            folder = await picker.PickSingleFolderAsync();
        }
        catch (COMException exception)
        {
            StatusMessage = "出力フォルダーの選択を完了できませんでした";
            AppLogger.Error("Output folder picker failed", exception);
            return;
        }

        if (folder is null)
        {
            return;
        }

        OutputDirectory = folder.Path;
        StatusMessage = "出力フォルダーを選択しました";
        UpdatePlannedOutputPath();
    }

    [RelayCommand(CanExecute = nameof(CanOpenOutputDirectory))]
    private void OpenOutputDirectory()
    {
        if (!CanOpenOutputDirectory())
        {
            StatusMessage = "有効な出力フォルダーを選択してください";
            return;
        }

        try
        {
            StatusMessage = outputDirectoryLauncher(OutputDirectory!)
                ? "出力フォルダーを開きました"
                : "出力フォルダーを開けませんでした";
        }
        catch (Exception exception)
        {
            StatusMessage = $"出力フォルダーを開けませんでした: {exception.Message}";
        }
    }

    private bool CanOpenOutputDirectory() =>
        !string.IsNullOrWhiteSpace(OutputDirectory) && Directory.Exists(OutputDirectory);

    [RelayCommand]
    private async Task BrowseFfmpegPathAsync()
    {
        string? path = await PickExecutableAsync("ffmpeg.exe を選択");
        if (path is null)
        {
            return;
        }

        FfmpegPath = path;
        StatusMessage = "ffmpeg を選択しました";
        await DetectEncoderCapabilitiesAsync();
    }

    [RelayCommand]
    private async Task BrowseFfprobePathAsync()
    {
        string? path = await PickExecutableAsync("ffprobe.exe を選択");
        if (path is null)
        {
            return;
        }

        FfprobePath = path;
        StatusMessage = "ffprobe を選択しました";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await settingsService.SaveAsync(CreateCurrentSettings());
        StatusMessage = "設定を保存しました";
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
            StatusMessage = "書き出し前に動画を開いてください";
            return;
        }

        if (string.IsNullOrWhiteSpace(FfmpegPath) || !File.Exists(FfmpegPath))
        {
            StatusMessage = "書き出し前に有効な ffmpeg.exe を選択してください";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory) || !Directory.Exists(OutputDirectory))
        {
            StatusMessage = "書き出し前に有効な出力フォルダーを選択してください";
            return;
        }

        ClipRange range = new(
            TimeSpan.FromSeconds(RangeStartSeconds),
            TimeSpan.FromSeconds(RangeEndSeconds));

        if (!range.IsValid)
        {
            StatusMessage = "終了時刻は開始時刻より後にしてください";
            return;
        }

        AppSettings currentSettings = CreateCurrentSettings();
        int? exportVideoBitrateKbps = GetEffectiveVideoBitrateKbps(range);
        if (GetEffectiveExportMode(currentSettings) == ExportMode.Reencode
            && CurrentBitrateMode != BitrateMode.Quality
            && exportVideoBitrateKbps is null)
        {
            StatusMessage = CurrentBitrateMode == BitrateMode.TargetSize
                ? "有効な目標サイズを入力してください"
                : "有効な映像ビットレートを入力してください";
            return;
        }

        UpdatePlannedOutputPath();
        if (string.IsNullOrWhiteSpace(PlannedOutputPath))
        {
            StatusMessage = "出力先パスを作成できませんでした";
            return;
        }

        IsExporting = true;
        exportCancellation = new CancellationTokenSource();
        ExportProgressValue = 0;
        IsExportProgressIndeterminate = true;
        ExportLogText = "書き出しを開始しています...";
        HasExportLog = true;
        StatusMessage = "書き出し中...";

        try
        {
            AppSettings settings = currentSettings with
            {
                LastVideoBitrateKbps = exportVideoBitrateKbps,
            };
            await settingsService.SaveAsync(settings);

            var request = new ExportRequest(SelectedSourcePath, PlannedOutputPath, range, settings, CurrentMediaInfo);
            IExportPlanner planner = new ExportPlannerFactory(currentCapabilities).CreatePlanner(GetEffectiveExportMode(settings));
            ExportPlan plan = planner.CreatePlan(request);
            var progress = new Progress<ExportProgress>(exportProgress =>
            {
                AppendExportLog(exportProgress.Status);

                if (exportProgress.Position is { } position)
                {
                    StatusMessage = $"書き出し中 {FormatTime(position)}";
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
                ? $"書き出しが完了しました: {Path.GetFileName(plan.FinalOutputPath)}"
                : result.ErrorMessage ?? "書き出しに失敗しました";
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

        StatusMessage = "書き出しをキャンセルしています...";
        AppendExportLog("キャンセルを要求しました。");
        exportCancellation.Cancel();
    }

    [RelayCommand]
    private void MarkStart()
    {
        RangeStartSeconds = ClampToDuration(PositionSeconds);
        SetRangeTextWithoutParsing(TimeSpan.FromSeconds(RangeStartSeconds), TimeSpan.FromSeconds(RangeEndSeconds));
        StatusMessage = "開始地点を設定しました";
    }

    [RelayCommand]
    private void MarkEnd()
    {
        RangeEndSeconds = ClampToDuration(PositionSeconds);
        SetRangeTextWithoutParsing(TimeSpan.FromSeconds(RangeStartSeconds), TimeSpan.FromSeconds(RangeEndSeconds));
        StatusMessage = "終了地点を設定しました";
    }

    partial void OnOutputDirectoryChanged(string? value)
    {
        UpdatePlannedOutputPath();
        OpenOutputDirectoryCommand.NotifyCanExecuteChanged();
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
        UpdateTargetSizeDerivedBitrate();
        UpdatePredictedOutputSizeText();
    }

    partial void OnRangeEndSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(RangeEndDisplayText));
        UpdateTargetSizeDerivedBitrate();
        UpdatePredictedOutputSizeText();
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

    partial void OnSelectedCodecFamilyIndexChanged(int value)
    {
        ApplySuggestedVideoBitrate(force: !hasManualVideoBitrateOverride);
    }

    partial void OnSelectedExportModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsReencodeSettingsVisible));
        OnPropertyChanged(nameof(ExportNoticeText));
        UpdatePredictedOutputSizeText();
    }

    partial void OnSelectedBitrateModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsBitrateMode));
        OnPropertyChanged(nameof(IsTargetSizeMode));
        OnPropertyChanged(nameof(IsQualityMode));
        UpdateTargetSizeDerivedBitrate();
        UpdatePredictedOutputSizeText();
    }

    partial void OnNormalizeAudioEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(ExportNoticeText));
    }

    partial void OnTargetSizeMegabytesChanged(double value)
    {
        UpdateTargetSizeDerivedBitrate();
        UpdatePredictedOutputSizeText();
    }

    partial void OnQualityValueChanged(double value)
    {
        if (!double.IsFinite(value))
        {
            QualityValue = 23;
        }
    }

    partial void OnVideoFadeInEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(ExportNoticeText));
    }

    partial void OnVideoFadeOutEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(ExportNoticeText));
    }

    partial void OnAudioFadeInEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(ExportNoticeText));
    }

    partial void OnAudioFadeOutEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(ExportNoticeText));
    }

    partial void OnFadeDurationSecondsChanged(double value)
    {
        if (isUpdatingFadeDuration)
        {
            return;
        }

        double normalizedValue = NormalizeFadeDurationSeconds(value);
        if (Math.Abs(normalizedValue - value) < 0.0001)
        {
            return;
        }

        isUpdatingFadeDuration = true;
        try
        {
            FadeDurationSeconds = normalizedValue;
        }
        finally
        {
            isUpdatingFadeDuration = false;
        }
    }

    partial void OnVideoBitrateTextChanged(string value)
    {
        if (!isUpdatingSuggestedBitrate)
        {
            hasManualVideoBitrateOverride = true;
        }

        UpdatePredictedOutputSizeText();
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

    private ExportMode CurrentExportMode => SelectedExportModeIndex switch
    {
        1 => ExportMode.Reencode,
        _ => ExportMode.FastCopy,
    };

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

    private BitrateMode CurrentBitrateMode => SelectedBitrateModeIndex switch
    {
        1 => BitrateMode.TargetSize,
        2 => BitrateMode.Quality,
        _ => BitrateMode.Bitrate,
    };

    private bool HasAnyFadeEnabled =>
        VideoFadeInEnabled
        || VideoFadeOutEnabled
        || AudioFadeInEnabled
        || AudioFadeOutEnabled;

    private bool HasActiveFadeEnabled =>
        CurrentExportMode == ExportMode.Reencode && HasAnyFadeEnabled;

    private AppSettings CreateCurrentSettings() => new()
    {
        FfmpegPath = FfmpegPath,
        FfprobePath = FfprobePath,
        OutputDirectory = OutputDirectory,
        LastExportMode = CurrentExportMode,
        LastCodecFamily = CurrentCodecFamily,
        LastEncoderKind = CurrentEncoderKind,
        LastBitrateMode = CurrentBitrateMode,
        LastVideoBitrateKbps = GetVideoBitrateKbps(),
        LastTargetSizeMegabytes = GetTargetSizeMegabytes(),
        LastQualityValue = GetQualityValue(),
        NormalizeAudio = NormalizeAudioEnabled,
        AdditionalFfmpegArguments = string.IsNullOrWhiteSpace(AdditionalFfmpegArgumentsText)
            ? null
            : AdditionalFfmpegArgumentsText,
        Fade = CurrentExportMode == ExportMode.Reencode
            ? new FadeSettings
            {
                VideoFadeIn = VideoFadeInEnabled,
                VideoFadeOut = VideoFadeOutEnabled,
                AudioFadeIn = AudioFadeInEnabled,
                AudioFadeOut = AudioFadeOutEnabled,
                DurationSeconds = GetFadeDurationSeconds(),
            }
            : new FadeSettings(),
    };

    private static ExportMode GetEffectiveExportMode(AppSettings settings) =>
        settings.Fade.HasAnyFade ? ExportMode.Reencode : settings.LastExportMode;

    private void ApplyExportSettings(AppSettings settings)
    {
        SelectedExportModeIndex = settings.LastExportMode switch
        {
            ExportMode.Reencode => 1,
            _ => 0,
        };
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
        SelectedBitrateModeIndex = settings.LastBitrateMode switch
        {
            BitrateMode.TargetSize => 1,
            BitrateMode.Quality => 2,
            _ => 0,
        };

        int initialBitrateKbps = settings.LastVideoBitrateKbps.GetValueOrDefault(2500);
        SetVideoBitrateTextFromSuggestion(initialBitrateKbps);
        hasManualVideoBitrateOverride = settings.LastVideoBitrateKbps.HasValue;
        TargetSizeMegabytes = settings.LastTargetSizeMegabytes.GetValueOrDefault(100);
        QualityValue = settings.LastQualityValue.GetValueOrDefault(23);
        NormalizeAudioEnabled = settings.NormalizeAudio || settings.LastExportMode == ExportMode.AudioNormalize;
        AdditionalFfmpegArgumentsText = settings.AdditionalFfmpegArguments ?? string.Empty;
        VideoFadeInEnabled = settings.Fade.VideoFadeIn;
        VideoFadeOutEnabled = settings.Fade.VideoFadeOut;
        AudioFadeInEnabled = settings.Fade.AudioFadeIn;
        AudioFadeOutEnabled = settings.Fade.AudioFadeOut;
        FadeDurationSeconds = settings.Fade.DurationSeconds > 0
            ? NormalizeFadeDurationSeconds(settings.Fade.DurationSeconds)
            : 1;
    }

    private int? GetVideoBitrateKbps()
    {
        return int.TryParse(VideoBitrateText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bitrateKbps)
            && bitrateKbps > 0
            ? bitrateKbps
            : null;
    }

    private void ApplySuggestedVideoBitrate(bool force)
    {
        if (!force || CurrentMediaInfo is null || CurrentBitrateMode != BitrateMode.Bitrate)
        {
            return;
        }

        int suggestedBitrateKbps = bitrateSuggestionService.SuggestVideoBitrateKbps(CurrentMediaInfo, CurrentCodecFamily);
        SetVideoBitrateTextFromSuggestion(suggestedBitrateKbps);
    }

    private void SetVideoBitrateTextFromSuggestion(int bitrateKbps)
    {
        isUpdatingSuggestedBitrate = true;
        try
        {
            VideoBitrateText = bitrateKbps.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            isUpdatingSuggestedBitrate = false;
        }
    }

    private double GetFadeDurationSeconds()
    {
        return double.IsFinite(FadeDurationSeconds) && FadeDurationSeconds > 0
            ? NormalizeFadeDurationSeconds(FadeDurationSeconds)
            : 1;
    }

    private static double NormalizeFadeDurationSeconds(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0)
        {
            return 1;
        }

        return Math.Max(0.25, Math.Truncate(seconds * 100) / 100);
    }

    private double? GetTargetSizeMegabytes()
    {
        return double.IsFinite(TargetSizeMegabytes) && TargetSizeMegabytes > 0
            ? Math.Round(TargetSizeMegabytes, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    private int? GetQualityValue()
    {
        return double.IsFinite(QualityValue)
            ? Math.Clamp((int)Math.Round(QualityValue, MidpointRounding.AwayFromZero), 0, 51)
            : null;
    }

    private long? GetTargetSizeBytes()
    {
        return GetTargetSizeMegabytes() is { } megabytes
            ? (long)Math.Round(megabytes * 1024 * 1024, MidpointRounding.AwayFromZero)
            : null;
    }

    private int? GetEffectiveVideoBitrateKbps(ClipRange range)
    {
        if (CurrentBitrateMode == BitrateMode.Bitrate)
        {
            return GetVideoBitrateKbps();
        }

        if (CurrentBitrateMode == BitrateMode.Quality)
        {
            return null;
        }

        if (!range.IsValid || GetTargetSizeBytes() is not { } targetBytes)
        {
            return null;
        }

        try
        {
            return PredictedOutputSizeCalculator.EstimateVideoBitrateKbps(
                targetBytes,
                GetPrimaryAudioBitrate(),
                range.Duration);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void UpdateTargetSizeDerivedBitrate()
    {
        if (CurrentBitrateMode != BitrateMode.TargetSize)
        {
            return;
        }

        var range = new ClipRange(
            TimeSpan.FromSeconds(RangeStartSeconds),
            TimeSpan.FromSeconds(RangeEndSeconds));
        int? bitrateKbps = GetEffectiveVideoBitrateKbps(range);
        if (bitrateKbps is not { } value)
        {
            return;
        }

        SetVideoBitrateTextFromSuggestion(value);
    }

    private void UpdatePredictedOutputSizeText()
    {
        var range = new ClipRange(
            TimeSpan.FromSeconds(RangeStartSeconds),
            TimeSpan.FromSeconds(RangeEndSeconds));
        int? videoBitrateKbps = GetEffectiveVideoBitrateKbps(range);
        if (CurrentMediaInfo is null || videoBitrateKbps is null)
        {
            PredictedOutputSizeText = "推定サイズはまだ計算できません";
            return;
        }

        if (!range.IsValid)
        {
            PredictedOutputSizeText = "推定サイズはまだ計算できません";
            return;
        }

        long? audioBitrate = GetPrimaryAudioBitrate();
        long bytes = PredictedOutputSizeCalculator.EstimateBytes(
            videoBitrateKbps.Value,
            audioBitrate,
            range.Duration);
        PredictedOutputSizeText = $"推定サイズ: {FormatBytes(bytes)}";
    }

    private long? GetPrimaryAudioBitrate()
    {
        return CurrentMediaInfo?.Streams
            .FirstOrDefault(stream => stream.CodecType == "audio")
            ?.Bitrate;
    }

    private static string FormatBytes(long bytes)
    {
        const double kiloByte = 1024.0;
        const double megaByte = kiloByte * 1024.0;
        const double gigaByte = megaByte * 1024.0;

        return bytes >= gigaByte
            ? $"{bytes / gigaByte:0.##} GB"
            : $"{bytes / megaByte:0.#} MB";
    }

    private async Task<string?> PickExecutableAsync(string commitButtonText)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            CommitButtonText = commitButtonText,
        };
        picker.FileTypeFilter.Add(".exe");

        Windows.Storage.StorageFile? file;
        try
        {
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
            file = await picker.PickSingleFileAsync();
        }
        catch (COMException exception)
        {
            StatusMessage = "実行ファイルの選択を完了できませんでした";
            AppLogger.Error("Executable picker failed", exception);
            return null;
        }

        return file?.Path;
    }

    private static string CreateToolDetectionStatus(FfmpegToolPaths paths)
    {
        return (paths.FfmpegPath, paths.FfprobePath) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => "ffmpeg と ffprobe を検出しました",
            ({ Length: > 0 }, _) => "ffmpeg を検出しました。ffprobe が見つかりません",
            (_, { Length: > 0 }) => "ffprobe を検出しました。ffmpeg が見つかりません",
            _ => "準備完了",
        };
    }

    private static bool OpenDirectoryInShell(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
        return true;
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
            MediaSummaryText = "メディア情報を取得できません: ffprobe が見つかりません";
            StatusMessage = "動画を選択しました。ffprobe が見つかりません";
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
            ApplySuggestedVideoBitrate(force: !hasManualVideoBitrateOverride);
            UpdateTargetSizeDerivedBitrate();
            UpdatePredictedOutputSizeText();
            MediaSummaryText = CreateMediaSummary(info);
            StatusMessage = "動画を選択しました";
            AppLogger.Info($"ffprobe metadata loaded: {MediaSummaryText.Replace(Environment.NewLine, " | ")}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            MediaSummaryText = $"メディア情報を取得できません: {exception.Message}";
            StatusMessage = "ffprobe のメタデータを取得できません";
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
            ? "映像: なし"
            : $"映像: {CreateVideoDescription(video)}";
        string audioText = audio is null
            ? "音声: なし"
            : $"音声: {CreateAudioDescription(audio)}";
        string bitrateText = info.Bitrate is { } bitrate
            ? $"ビットレート: {FormatBitrate(bitrate)}"
            : "ビットレート: 不明";

        return $"長さ: {duration}{Environment.NewLine}{videoText}{Environment.NewLine}{audioText}{Environment.NewLine}{bitrateText}";
    }

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
            EncoderSummaryText = "エンコーダー情報を取得できません: ffmpeg が見つかりません";
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
            EncoderSummaryText = $"エンコーダー情報を取得できません: {exception.Message}";
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
        string nvenc = capabilities.SupportsNvenc(codecFamily) ? "NVEnc 利用可能" : "NVEnc 利用不可";
        return autoEncoder is null
            ? $"{label}: 対応エンコーダーが見つかりません ({nvenc})"
            : $"{label}: {autoEncoder} ({nvenc})";
    }

    private void AppendExportLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        string timestampedLine = $"[{DateTimeOffset.Now:HH:mm:ss}] {line.Trim()}";
        if (string.IsNullOrWhiteSpace(ExportLogText) || ExportLogText == "書き出しログはまだありません。")
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
