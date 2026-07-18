using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;
using VideoCutEditor.ViewModels;

namespace VideoCutEditor.App.Tests;

public sealed class MainPageViewModelTests
{
    [Fact]
    public void Media_summary_identifies_hdr10_pq_and_color_metadata()
    {
        var info = new MediaInfo(
            "hdr10.mp4",
            TimeSpan.FromSeconds(10),
            "mov,mp4",
            12_000_000,
            [new MediaStreamInfo(0, "video", "hevc", 10_000_000, 3840, 2160, 60, ColorSpace: "bt2020nc", ColorTransfer: "smpte2084", ColorPrimaries: "bt2020")]);

        string summary = MediaSummaryFormatter.Create(info);

        Assert.Contains("ダイナミックレンジ: HDR10 (PQ)", summary);
        Assert.Contains("色空間: bt2020nc", summary);
        Assert.Contains("伝達特性: smpte2084", summary);
        Assert.Contains("色域: bt2020", summary);
    }

    [Fact]
    public void Media_summary_identifies_hlg()
    {
        var info = new MediaInfo(
            "hlg.mp4",
            TimeSpan.FromSeconds(10),
            "mov,mp4",
            null,
            [new MediaStreamInfo(0, "video", "hevc", null, 1920, 1080, 30, ColorTransfer: "arib-std-b67")]);

        string summary = MediaSummaryFormatter.Create(info);

        Assert.Contains("ダイナミックレンジ: HLG", summary);
    }

    [Fact]
    public void Tool_directory_selection_sets_both_paths_and_keeps_individual_fields_hidden()
    {
        string directory = CreateTempDirectory();
        string ffmpeg = CreateFile(directory, "ffmpeg.exe");
        string ffprobe = CreateFile(directory, "ffprobe.exe");
        MainPageViewModel viewModel = CreateViewModel(toolPathService: new FfmpegToolPathService());

        viewModel.ApplyToolDirectorySelection(directory);

        Assert.Equal(directory, viewModel.ToolDirectoryPath);
        Assert.Equal(ffmpeg, viewModel.FfmpegPath);
        Assert.Equal(ffprobe, viewModel.FfprobePath);
        Assert.False(viewModel.IsIndividualToolPathSelectionVisible);
    }

    [Fact]
    public void Tool_directory_selection_reveals_individual_fields_when_ffprobe_is_missing()
    {
        string directory = CreateTempDirectory();
        string ffmpeg = CreateFile(directory, "ffmpeg.exe");
        MainPageViewModel viewModel = CreateViewModel(toolPathService: new FfmpegToolPathService());

        viewModel.ApplyToolDirectorySelection(directory);

        Assert.Equal(ffmpeg, viewModel.FfmpegPath);
        Assert.Null(viewModel.FfprobePath);
        Assert.True(viewModel.IsIndividualToolPathSelectionVisible);
        Assert.Contains("ffprobe.exe を個別に指定", viewModel.StatusMessage);
    }

    [Fact]
    public void Time_inputs_update_and_clamp_the_selected_range()
    {
        MainPageViewModel viewModel = CreateViewModel();
        viewModel.SetMediaDuration(TimeSpan.FromSeconds(90));

        viewModel.RangeStartText = "1:02.5";
        viewModel.RangeEndText = "2:00";

        Assert.Equal(62.5, viewModel.RangeStartSeconds);
        Assert.Equal(90, viewModel.RangeEndSeconds);
    }

    [Fact]
    public void Manual_output_file_name_updates_planned_path_and_inherits_source_extension()
    {
        string directory = CreateTempDirectory();
        MainPageViewModel viewModel = CreateViewModel();
        viewModel.SelectedSourcePath = Path.Combine(directory, "source.mp4");
        viewModel.OutputDirectory = directory;

        viewModel.PlannedOutputFileName = "chosen-name";

        Assert.Equal("chosen-name.mp4", viewModel.PlannedOutputFileName);
        Assert.Equal(Path.Combine(directory, "chosen-name.mp4"), viewModel.PlannedOutputPath);
    }

    [Fact]
    public void Manual_output_file_name_warns_when_the_destination_file_already_exists()
    {
        string directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "existing.mp4"), string.Empty);
        MainPageViewModel viewModel = CreateViewModel();
        viewModel.SelectedSourcePath = Path.Combine(directory, "source.mp4");
        viewModel.OutputDirectory = directory;

        viewModel.PlannedOutputFileName = "existing";

        Assert.Equal(Path.Combine(directory, "existing.mp4"), viewModel.PlannedOutputPath);
        Assert.True(viewModel.IsManualOutputFileNameCollision);
    }

    [Fact]
    public void AddClip_captures_range_assigns_placeholder_and_requests_list_once()
    {
        string directory = CreateTempDirectory();
        MainPageViewModel viewModel = CreateViewModel();
        viewModel.SelectedSourcePath = Path.Combine(directory, "source.mp4");
        viewModel.OutputDirectory = directory;
        viewModel.RangeStartSeconds = 2.5;
        viewModel.RangeEndSeconds = 8.75;
        int listRequestCount = 0;
        viewModel.ExportListRequested += (_, _) => listRequestCount++;

        viewModel.AddClipCommand.Execute(null);
        viewModel.RangeStartSeconds = 10;
        viewModel.RangeEndSeconds = 12;
        viewModel.AddClipCommand.Execute(null);

        Assert.Collection(
            viewModel.RegisteredClips,
            first =>
            {
                Assert.Equal("クリップ_1", first.Title);
                Assert.Equal(TimeSpan.FromSeconds(2.5), first.Range.Start);
                Assert.Equal(TimeSpan.FromSeconds(8.75), first.Range.End);
            },
            second => Assert.Equal("クリップ_2", second.Title));
        Assert.Equal(1, listRequestCount);
        Assert.Equal(string.Empty, viewModel.ClipTitleText);
        Assert.True(viewModel.HasRegisteredClips);
        Assert.Equal("登録クリップ: 2件", viewModel.RegisteredClipCountText);
    }

    [Fact]
    public void AddClip_suffixes_duplicate_title_and_remove_clip_updates_state()
    {
        string directory = CreateTempDirectory();
        MainPageViewModel viewModel = CreateViewModel();
        viewModel.SelectedSourcePath = Path.Combine(directory, "source.mp4");
        viewModel.OutputDirectory = directory;
        viewModel.RangeEndSeconds = 1;
        viewModel.ClipTitleText = "見どころ";
        viewModel.AddClipCommand.Execute(null);
        viewModel.ClipTitleText = "見どころ";
        viewModel.AddClipCommand.Execute(null);

        Assert.Equal(["見どころ", "見どころ_1"], viewModel.RegisteredClips.Select(clip => clip.Title));

        viewModel.RemoveClipCommand.Execute(viewModel.RegisteredClips[0]);
        viewModel.RemoveClipCommand.Execute(viewModel.RegisteredClips[0]);

        Assert.Empty(viewModel.RegisteredClips);
        Assert.False(viewModel.HasRegisteredClips);
        Assert.Equal("登録クリップはありません", viewModel.RegisteredClipCountText);
    }

    [Fact]
    public void AddClip_rejects_invalid_range()
    {
        MainPageViewModel viewModel = CreateViewModel();
        viewModel.SelectedSourcePath = "source.mp4";
        viewModel.RangeStartSeconds = 5;
        viewModel.RangeEndSeconds = 5;

        viewModel.AddClipCommand.Execute(null);

        Assert.Empty(viewModel.RegisteredClips);
        Assert.Equal("終了時刻は開始時刻より後にしてください", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Export_validation_reports_missing_source_before_starting_runner()
    {
        var runner = new RecordingFfmpegRunner();
        MainPageViewModel viewModel = CreateViewModel(ffmpegRunner: runner);

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal("書き出し前に動画を開いてください", viewModel.StatusMessage);
        Assert.False(viewModel.IsExporting);
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task Export_validation_reports_missing_ffmpeg_after_source_is_selected()
    {
        string directory = CreateTempDirectory();
        string sourcePath = Path.Combine(directory, "source.mp4");
        await File.WriteAllTextAsync(sourcePath, "source");
        var runner = new RecordingFfmpegRunner();
        MainPageViewModel viewModel = CreateViewModel(ffmpegRunner: runner);
        viewModel.SelectedSourcePath = sourcePath;
        viewModel.OutputDirectory = directory;
        viewModel.RangeEndSeconds = 1;

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal("書き出し前に有効な ffmpeg.exe を選択してください", viewModel.StatusMessage);
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task ExportAsync_creates_fast_copy_plan_and_completes_view_model_state()
    {
        string directory = CreateTempDirectory();
        string sourcePath = await CreateFileAsync(directory, "source.mp4");
        string ffmpegPath = await CreateFileAsync(directory, "ffmpeg.exe");
        var runner = new SuccessfulFfmpegRunner();
        var settings = new RecordingSettingsService();
        MainPageViewModel viewModel = CreateViewModel(settingsService: settings, ffmpegRunner: runner);
        viewModel.SelectedSourcePath = sourcePath;
        viewModel.FfmpegPath = ffmpegPath;
        viewModel.OutputDirectory = directory;
        viewModel.RangeEndSeconds = 2;

        await viewModel.ExportCommand.ExecuteAsync(null);

        ExportPlan plan = Assert.IsType<ExportPlan>(runner.Plan);
        Assert.Equal(sourcePath, plan.SourcePath);
        Assert.Equal(Path.Combine(directory, "source_cut.mp4"), plan.FinalOutputPath);
        Assert.Contains("copy", plan.Arguments);
        Assert.Equal(plan.FinalOutputPath, viewModel.PlannedOutputPath);
        Assert.True(viewModel.HasExportLog);
        Assert.Equal(1, viewModel.ExportProgressValue);
        Assert.False(viewModel.IsExportProgressIndeterminate);
        Assert.False(viewModel.IsExporting);
        Assert.Contains("書き出しが完了しました", viewModel.StatusMessage);
        Assert.NotNull(settings.SavedSettings);
    }

    [Fact]
    public async Task CancelExportAsync_cancels_runner_and_keeps_cancellation_log()
    {
        string directory = CreateTempDirectory();
        string sourcePath = await CreateFileAsync(directory, "source.mp4");
        string ffmpegPath = await CreateFileAsync(directory, "ffmpeg.exe");
        var runner = new BlockingFfmpegRunner();
        MainPageViewModel viewModel = CreateViewModel(ffmpegRunner: runner);
        viewModel.SelectedSourcePath = sourcePath;
        viewModel.FfmpegPath = ffmpegPath;
        viewModel.OutputDirectory = directory;
        viewModel.RangeEndSeconds = 2;

        Task exportTask = viewModel.ExportCommand.ExecuteAsync(null);
        await runner.Started.Task;

        Assert.True(viewModel.IsExporting);
        viewModel.CancelExportCommand.Execute(null);
        await exportTask;

        Assert.True(runner.CancellationRequested);
        Assert.False(viewModel.IsExporting);
        Assert.False(viewModel.IsExportProgressIndeterminate);
        Assert.Contains("キャンセルを要求しました", viewModel.ExportLogText);
        Assert.Equal("書き出しをキャンセルしました", viewModel.StatusMessage);
    }

    [Fact]
    public async Task InitializeAsync_uses_defaults_and_reports_recoverable_settings_load_failure()
    {
        MainPageViewModel viewModel = CreateViewModel(settingsService: new ThrowingSettingsService());

        await viewModel.InitializeAsync();

        Assert.Null(viewModel.FfmpegPath);
        Assert.Null(viewModel.FfprobePath);
        Assert.Null(viewModel.OutputDirectory);
        Assert.Contains("既定値", viewModel.StatusMessage);
    }

    private static MainPageViewModel CreateViewModel(
        ISettingsService? settingsService = null,
        IFfmpegRunner? ffmpegRunner = null,
        IFfmpegToolPathService? toolPathService = null) =>
        new(
            settingsService ?? new StubSettingsService(),
            new OutputPathService(),
            toolPathService ?? new StubToolPathService(),
            ffmpegRunner ?? new RecordingFfmpegRunner(),
            new StubMediaProbeService(),
            new StubCapabilityService());

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.App.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task<string> CreateFileAsync(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, string.Empty);
        return path;
    }

    private static string CreateFile(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private sealed class StubSettingsService : ISettingsService
    {
        public string SettingsFilePath => "settings.json";

        public ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AppSettings());

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class RecordingSettingsService : ISettingsService
    {
        public AppSettings? SavedSettings { get; private set; }

        public string SettingsFilePath => "settings.json";

        public ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AppSettings());

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SavedSettings = settings;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingSettingsService : ISettingsService
    {
        public string SettingsFilePath => "settings.json";

        public ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromException<AppSettings>(new IOException("settings unavailable"));

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class StubToolPathService : IFfmpegToolPathService
    {
        public FfmpegToolPaths Resolve(AppSettings settings) => new(null, null);

        public FfmpegToolPaths ResolveDirectory(string directoryPath) => new(null, null);
    }

    private sealed class RecordingFfmpegRunner : IFfmpegRunner
    {
        public bool WasCalled { get; private set; }

        public Task<ExportResult> RunAsync(
            ExportPlan plan,
            IProgress<ExportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new ExportResult(true, null));
        }
    }

    private sealed class SuccessfulFfmpegRunner : IFfmpegRunner
    {
        public ExportPlan? Plan { get; private set; }

        public Task<ExportResult> RunAsync(
            ExportPlan plan,
            IProgress<ExportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Plan = plan;
            progress?.Report(new ExportProgress(TimeSpan.FromSeconds(1), null, "書き出し進捗"));
            return Task.FromResult(new ExportResult(true, null));
        }
    }

    private sealed class BlockingFfmpegRunner : IFfmpegRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CancellationRequested { get; private set; }

        public async Task<ExportResult> RunAsync(
            ExportPlan plan,
            IProgress<ExportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationRequested = true;
                return new ExportResult(false, "書き出しをキャンセルしました");
            }

            throw new InvalidOperationException("The blocking export runner should be canceled.");
        }
    }

    private sealed class StubMediaProbeService : IMediaProbeService
    {
        public Task<MediaInfo> ProbeAsync(
            string ffprobePath,
            string sourcePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubCapabilityService : IFfmpegCapabilityService
    {
        public Task<FfmpegCapabilities> DetectAsync(
            string ffmpegPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
    }
}
