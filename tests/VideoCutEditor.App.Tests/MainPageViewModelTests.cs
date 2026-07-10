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

    private sealed class StubSettingsService : ISettingsService
    {
        public string SettingsFilePath => "settings.json";

        public ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AppSettings());

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
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
