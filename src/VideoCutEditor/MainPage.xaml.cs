using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using VideoCutEditor.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.System;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VideoCutEditor;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    private const int LeftBracketVirtualKey = 219;
    private const int RightBracketVirtualKey = 221;

    private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mkv",
        ".mov",
        ".avi",
        ".webm",
    };

    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer playbackTimer;
    private CancellationTokenSource? waveformCancellation;

    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        AppLogger.Info("MainPage constructor starting");

        try
        {
            InitializeComponent();
            AppLogger.Info("MainPage InitializeComponent completed");
        }
        catch (Exception exception)
        {
            AppLogger.Error("MainPage InitializeComponent failed", exception);
            throw;
        }

        PreviewPlayer.SetMediaPlayer(new MediaPlayer());
        AppLogger.Info("Preview MediaPlayer assigned");
        ViewModel.PropertyChanged += ViewModelPropertyChanged;
        ConfigureTimelineControls();
        AppLogger.Info("Timeline controls configured");
        PreviewPlayer.MediaPlayer.MediaOpened += PreviewPlayerMediaOpened;
        PreviewPlayer.MediaPlayer.MediaFailed += PreviewPlayerMediaFailed;
        playbackTimer = DispatcherQueue.CreateTimer();
        playbackTimer.Interval = TimeSpan.FromMilliseconds(100);
        playbackTimer.Tick += PlaybackTimerTick;
        Loaded += MainPageLoaded;
        Unloaded += MainPageUnloaded;
        AppLogger.Info("MainPage constructor completed");
    }

    private async void MainPageLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AppLogger.Info("MainPage loaded");
        Loaded -= MainPageLoaded;
        await ViewModel.InitializeAsync();
        AppLogger.Info("MainPage ViewModel initialized");
        EditorRoot.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        playbackTimer.Start();
        AppLogger.Info("Playback timer started");
    }

    private void MainPageUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AppLogger.Info("MainPage unloaded");
        playbackTimer.Stop();
        waveformCancellation?.Cancel();
        ViewModel.PropertyChanged -= ViewModelPropertyChanged;
        PreviewPlayer.MediaPlayer.Pause();
    }

    public static Microsoft.UI.Xaml.Visibility BoolToVisibility(bool value) =>
        value ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public static bool IsNotBusy(bool value) => !value;

    private void EditorRoot_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Open video";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsGlyphVisible = true;
    }

    private async void EditorRoot_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        AppLogger.Info("Drop received");
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            AppLogger.Info("Drop ignored: no storage items");
            return;
        }

        IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
        StorageFile? videoFile = items.OfType<StorageFile>().FirstOrDefault(IsSupportedVideoFile);

        if (videoFile is null)
        {
            AppLogger.Info("Drop ignored: no supported video file");
            ViewModel.StatusMessage = "No supported video file dropped";
            return;
        }

        AppLogger.Info($"Dropped video: {videoFile.Path}");

        if (!string.IsNullOrWhiteSpace(ViewModel.SelectedSourcePath))
        {
            bool shouldReplace = await ConfirmReplaceVideoAsync(videoFile);
            if (!shouldReplace)
            {
                ViewModel.StatusMessage = "Open canceled";
                return;
            }
        }

        await ViewModel.OpenVideoFileAsync(videoFile);
        AppLogger.Info("Dropped video opened");
        EditorRoot.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private void EditorRoot_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Left:
                StepOneFrame(-1);
                e.Handled = true;
                break;
            case VirtualKey.Right:
                StepOneFrame(1);
                e.Handled = true;
                break;
            default:
                if ((int)e.Key == LeftBracketVirtualKey)
                {
                    ViewModel.MarkStartCommand.Execute(null);
                    e.Handled = true;
                }
                else if ((int)e.Key == RightBracketVirtualKey)
                {
                    ViewModel.MarkEndCommand.Execute(null);
                    e.Handled = true;
                }

                break;
        }
    }

    private void PlayPauseButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (PreviewPlayer.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            PreviewPlayer.MediaPlayer.Pause();
        }
        else
        {
            PreviewPlayer.MediaPlayer.Play();
        }

        EditorRoot.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private void TimelineCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.PreviewSource is null || ViewModel.DurationSeconds <= 0)
        {
            return;
        }

        double x = e.GetCurrentPoint(TimelineCanvas).Position.X;
        double seconds = XToSeconds(x);
        SeekTo(TimeSpan.FromSeconds(seconds));
        EditorRoot.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private void TimelineZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        ViewModel.TimelineZoom = e.NewValue;
        UpdateTimelineVisuals();
        EnsurePlayheadVisible();
    }

    private void TimelineScrollViewer_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        ViewModel.TimelineViewportWidth = e.NewSize.Width;
        UpdateTimelineVisuals();
    }

    private void PlaybackRateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PreviewPlayer?.MediaPlayer?.PlaybackSession is null)
        {
            AppLogger.Info("PlaybackRate selection changed before preview playback session was initialized");
            return;
        }

        double rate = PlaybackRateComboBox.SelectedIndex switch
        {
            0 => 0.25,
            1 => 0.5,
            2 => 0.75,
            _ => 1.0,
        };

        ViewModel.PlaybackRate = rate;
        PreviewPlayer.MediaPlayer.PlaybackSession.PlaybackRate = rate;
    }

    private void PreviewPlayerMediaOpened(MediaPlayer sender, object args)
    {
        AppLogger.Info("MediaPlayer MediaOpened");
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            TimeSpan duration = sender.PlaybackSession.NaturalDuration;
            AppLogger.Info($"Media duration: {duration}");
            ViewModel.SetMediaDuration(duration);
            sender.PlaybackSession.PlaybackRate = ViewModel.PlaybackRate;
            SyncPositionFromPlayer();
            UpdateTimelineVisuals();
        });
    }

    private void PreviewPlayerMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        AppLogger.Info($"MediaPlayer MediaFailed: {args.Error} {args.ErrorMessage}");
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.StatusMessage = "Preview unavailable";
        });
    }

    private void PlaybackTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        SyncPositionFromPlayer();
    }

    private void SyncPositionFromPlayer()
    {
        if (ViewModel.PreviewSource is null)
        {
            return;
        }

        ViewModel.PositionSeconds = PreviewPlayer.MediaPlayer.PlaybackSession.Position.TotalSeconds;
        UpdateTimelineVisuals();
        EnsurePlayheadVisible();
    }

    private void StepOneFrame(int direction)
    {
        if (ViewModel.PreviewSource is null)
        {
            return;
        }

        TimeSpan current = PreviewPlayer.MediaPlayer.PlaybackSession.Position;
        TimeSpan target = current + (ViewModel.GetFrameStep() * direction);
        SeekTo(target);
    }

    private void SeekTo(TimeSpan target)
    {
        if (target < TimeSpan.Zero)
        {
            target = TimeSpan.Zero;
        }

        if (ViewModel.DurationSeconds > 0 && target.TotalSeconds > ViewModel.DurationSeconds)
        {
            target = TimeSpan.FromSeconds(ViewModel.DurationSeconds);
        }

        PreviewPlayer.MediaPlayer.PlaybackSession.Position = target;
        ViewModel.PositionSeconds = target.TotalSeconds;
        UpdateTimelineVisuals();
        EnsurePlayheadVisible();
    }

    private async Task<bool> ConfirmReplaceVideoAsync(StorageFile videoFile)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style,
            Title = "Open another video?",
            Content = $"Replace the current video with {videoFile.Name}?",
            PrimaryButtonText = "Open",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private static bool IsSupportedVideoFile(StorageFile file) =>
        SupportedVideoExtensions.Contains(file.FileType);

    private void ConfigureTimelineControls()
    {
        AppLogger.Info("ConfigureTimelineControls starting");
        TimelineZoomSlider.Value = ViewModel.TimelineZoom;
        UpdateTimelineVisuals();
        AppLogger.Info("ConfigureTimelineControls completed");
    }

    private bool IsTextInputFocused()
    {
        object? focusedElement = FocusManager.GetFocusedElement(XamlRoot);
        return focusedElement is TextBox;
    }

    private async void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainPageViewModel.SelectedSourcePath):
                await GenerateWaveformAsync(ViewModel.SelectedSourcePath);
                break;
            case nameof(MainPageViewModel.PositionSeconds):
            case nameof(MainPageViewModel.RangeStartSeconds):
            case nameof(MainPageViewModel.RangeEndSeconds):
            case nameof(MainPageViewModel.DurationSeconds):
            case nameof(MainPageViewModel.TimelineContentWidth):
                UpdateTimelineVisuals();
                break;
        }
    }

    private void UpdateTimelineVisuals()
    {
        double width = ViewModel.TimelineContentWidth;
        double startX = SecondsToX(ViewModel.RangeStartSeconds);
        double endX = SecondsToX(ViewModel.RangeEndSeconds);
        double playheadX = SecondsToX(ViewModel.PositionSeconds);

        Canvas.SetLeft(SelectedRangeFill, Math.Min(startX, endX));
        SelectedRangeFill.Width = Math.Max(0, Math.Abs(endX - startX));
        Canvas.SetLeft(RangeStartMarker, Math.Max(0, startX - (RangeStartMarker.Width / 2)));
        Canvas.SetLeft(RangeEndMarker, Math.Max(0, endX - (RangeEndMarker.Width / 2)));
        Canvas.SetLeft(PlayheadMarker, Math.Clamp(playheadX - (PlayheadMarker.Width / 2), 0, Math.Max(0, width - PlayheadMarker.Width)));

        DrawTimelineTicks(width);
    }

    private void DrawTimelineTicks(double width)
    {
        TimelineTicksCanvas.Children.Clear();

        if (ViewModel.DurationSeconds <= 0 || width <= 0)
        {
            return;
        }

        double targetTickSpacing = 120;
        double secondsPerTick = PickTickInterval(ViewModel.DurationSeconds / Math.Max(1, width / targetTickSpacing));

        for (double second = 0; second <= ViewModel.DurationSeconds + 0.001; second += secondsPerTick)
        {
            double x = SecondsToX(second);
            var tick = new Rectangle
            {
                Width = 1,
                Height = second % (secondsPerTick * 5) < 0.001 ? 20 : 12,
                Fill = Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush,
            };

            Canvas.SetLeft(tick, x);
            Canvas.SetTop(tick, 4);
            TimelineTicksCanvas.Children.Add(tick);

            var label = new TextBlock
            {
                Text = FormatTimelineLabel(TimeSpan.FromSeconds(second)),
                Style = Microsoft.UI.Xaml.Application.Current.Resources["CaptionTextBlockStyle"] as Microsoft.UI.Xaml.Style,
                Foreground = Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush,
            };

            Canvas.SetLeft(label, Math.Min(Math.Max(0, x + 4), Math.Max(0, width - 64)));
            Canvas.SetTop(label, 4);
            TimelineTicksCanvas.Children.Add(label);
        }
    }

    private void EnsurePlayheadVisible()
    {
        if (ViewModel.DurationSeconds <= 0 || TimelineScrollViewer.ViewportWidth <= 0 || TimelineScrollViewer.ScrollableWidth <= 0)
        {
            return;
        }

        double playheadX = SecondsToX(ViewModel.PositionSeconds);
        double left = TimelineScrollViewer.HorizontalOffset;
        double right = left + TimelineScrollViewer.ViewportWidth;
        const double margin = 48;

        if (playheadX < left + margin)
        {
            TimelineScrollViewer.ChangeView(Math.Max(0, playheadX - margin), null, null, disableAnimation: true);
        }
        else if (playheadX > right - margin)
        {
            TimelineScrollViewer.ChangeView(Math.Min(TimelineScrollViewer.ScrollableWidth, playheadX - TimelineScrollViewer.ViewportWidth + margin), null, null, disableAnimation: true);
        }
    }

    private double SecondsToX(double seconds)
    {
        if (ViewModel.DurationSeconds <= 0)
        {
            return 0;
        }

        return Math.Clamp(seconds / ViewModel.DurationSeconds, 0, 1) * ViewModel.TimelineContentWidth;
    }

    private double XToSeconds(double x)
    {
        if (ViewModel.TimelineContentWidth <= 0)
        {
            return 0;
        }

        return Math.Clamp(x / ViewModel.TimelineContentWidth, 0, 1) * ViewModel.DurationSeconds;
    }

    private async Task GenerateWaveformAsync(string? sourcePath)
    {
        waveformCancellation?.Cancel();
        waveformCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = waveformCancellation.Token;
        WaveformImage.Source = null;

        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(ViewModel.FfmpegPath) || !File.Exists(ViewModel.FfmpegPath))
        {
            return;
        }

        try
        {
            string outputPath = CreateWaveformPath(sourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ViewModel.FfmpegPath,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
            };

            string[] arguments =
            [
                "-hide_banner",
                "-loglevel",
                "error",
                "-y",
                "-i",
                sourcePath,
                "-filter_complex",
                "aformat=channel_layouts=mono,showwavespic=s=2400x160:colors=#4cc2ff",
                "-frames:v",
                "1",
                outputPath,
            ];

            foreach (string argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            AppLogger.Info($"Waveform generation starting: {sourcePath}");
            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                AppLogger.Info($"Waveform generation skipped. ExitCode={process.ExitCode}");
                return;
            }

            WaveformImage.Source = new BitmapImage(new Uri(outputPath));
            AppLogger.Info($"Waveform generated: {outputPath}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AppLogger.Error("Waveform generation failed", exception);
        }
    }

    private static string CreateWaveformPath(string sourcePath)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourcePath));
        string name = Convert.ToHexString(hash)[..16].ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), "VideoCutEditor", "waveforms", $"{name}.png");
    }

    private static double PickTickInterval(double desiredSeconds)
    {
        double[] intervals = [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600];
        return intervals.FirstOrDefault(interval => interval >= desiredSeconds, intervals[^1]);
    }

    private static string FormatTimelineLabel(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }
}
