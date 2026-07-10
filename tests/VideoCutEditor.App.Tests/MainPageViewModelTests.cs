using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;
using VideoCutEditor.ViewModels;

namespace VideoCutEditor.App.Tests;

public sealed class MainPageViewModelTests
{
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
        IFfmpegRunner? ffmpegRunner = null) =>
        new(
            settingsService ?? new StubSettingsService(),
            new OutputPathService(),
            new StubToolPathService(),
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
