using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using VideoCutEditor.Core.Services;
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
    private const double TimelineZoomStep = 0.01;
    private const double TimelineZoomMinimum = 1.0;
    private const double TimelineZoomMaximum = 8.0;
    private const int TimelineMinorTickDivisions = 5;
    private const double TimelineLabelTop = 4;
    private const double TimelineLabelWidth = 64;
    private const double TimelineMajorTickTop = 24;
    private const double TimelineMajorTickHeight = 20;
    private const double TimelineMinorTickTop = 34;
    private const double TimelineMinorTickHeight = 10;

    private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mkv",
        ".mov",
        ".avi",
        ".webm",
    };

    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer playbackTimer;
    private readonly WaveformPlanner waveformPlanner = new();
    private readonly IWaveformGenerator waveformGenerator = new WaveformGenerator();
    private CancellationTokenSource? waveformCancellation;
    private bool isDraggingTimeline;

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

        EditorRoot.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, new KeyEventHandler(EditorRoot_KeyDown), handledEventsToo: true);
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
        e.DragUIOverride.Caption = "動画を開く";
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
            ViewModel.StatusMessage = "対応している動画ファイルがドロップされていません";
            return;
        }

        AppLogger.Info($"Dropped video: {videoFile.Path}");

        if (!string.IsNullOrWhiteSpace(ViewModel.SelectedSourcePath))
        {
            bool shouldReplace = await ConfirmReplaceVideoAsync(videoFile);
            if (!shouldReplace)
            {
                ViewModel.StatusMessage = "動画の読み込みをキャンセルしました";
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

    private void LocatePlayheadButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CenterTimelineOnPlayhead();
        EditorRoot.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private void TimelineZoomOutButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AdjustTimelineZoomAroundPlayhead(-TimelineZoomStep);
        EditorRoot.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private void TimelineZoomInButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AdjustTimelineZoomAroundPlayhead(TimelineZoomStep);
        EditorRoot.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private void TimelineCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!TrySeekTimelineToPointer(e))
        {
            return;
        }

        isDraggingTimeline = true;
        TimelineCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
        EditorRoot.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private void TimelineCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!isDraggingTimeline)
        {
            return;
        }

        TrySeekTimelineToPointer(e);
        e.Handled = true;
    }

    private void TimelineCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!isDraggingTimeline)
        {
            return;
        }

        TrySeekTimelineToPointer(e);
        EndTimelineDrag(e);
    }

    private void TimelineCanvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (!isDraggingTimeline)
        {
            return;
        }

        EndTimelineDrag(e);
    }

    private void TimelineCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            & Windows.UI.Core.CoreVirtualKeyStates.Down) == 0)
        {
            return;
        }

        var pointerPoint = e.GetCurrentPoint(TimelineCanvas);
        int delta = pointerPoint.Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        double anchorSeconds = XToSeconds(pointerPoint.Position.X);
        double viewportX = pointerPoint.Position.X - TimelineScrollViewer.HorizontalOffset;
        AdjustTimelineZoomAroundPointer(delta > 0 ? TimelineZoomStep : -TimelineZoomStep, anchorSeconds, viewportX);
        e.Handled = true;
    }

    private bool TrySeekTimelineToPointer(PointerRoutedEventArgs e)
    {
        if (ViewModel.PreviewSource is null || ViewModel.DurationSeconds <= 0)
        {
            return false;
        }

        double x = e.GetCurrentPoint(TimelineCanvas).Position.X;
        double seconds = XToSeconds(x);
        SeekTo(TimeSpan.FromSeconds(seconds));
        return true;
    }

    private void EndTimelineDrag(PointerRoutedEventArgs e)
    {
        isDraggingTimeline = false;
        TimelineCanvas.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
        EditorRoot.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private void TimelineZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        SetTimelineZoom(e.NewValue);
        UpdateTimelineVisuals();
        EnsurePlayheadVisible();
    }

    private void AdjustTimelineZoom(double delta)
    {
        AdjustTimelineZoomAroundPlayhead(delta);
    }

    private void AdjustTimelineZoomAroundPlayhead(double delta)
    {
        SetTimelineZoom(ViewModel.TimelineZoom + delta);
        if (Math.Abs(TimelineZoomSlider.Value - ViewModel.TimelineZoom) > double.Epsilon)
        {
            TimelineZoomSlider.Value = ViewModel.TimelineZoom;
        }

        UpdateTimelineVisuals();
        CenterTimelineOnPlayhead();
    }

    private void AdjustTimelineZoomAroundPointer(double delta, double anchorSeconds, double viewportX)
    {
        SetTimelineZoom(ViewModel.TimelineZoom + delta);
        if (Math.Abs(TimelineZoomSlider.Value - ViewModel.TimelineZoom) > double.Epsilon)
        {
            TimelineZoomSlider.Value = ViewModel.TimelineZoom;
        }

        UpdateTimelineVisuals();
        ScrollTimelineToAnchor(anchorSeconds, viewportX);
    }

    private void SetTimelineZoom(double value)
    {
        ViewModel.TimelineZoom = Math.Round(
            Math.Clamp(value, TimelineZoomMinimum, TimelineZoomMaximum),
            2,
            MidpointRounding.AwayFromZero);
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
            ViewModel.StatusMessage = "プレビューを利用できません";
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
            Title = "別の動画を開きますか？",
            Content = $"現在の動画を「{videoFile.Name}」に置き換えますか？",
            PrimaryButtonText = "開く",
            CloseButtonText = "キャンセル",
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

        double targetMajorTickSpacing = 120;
        double secondsPerMajorTick = PickTickInterval(ViewModel.DurationSeconds / Math.Max(1, width / targetMajorTickSpacing));
        double secondsPerMinorTick = secondsPerMajorTick / TimelineMinorTickDivisions;
        int tickIndex = 0;

        for (double second = 0; second <= ViewModel.DurationSeconds + 0.001; second += secondsPerMinorTick, tickIndex++)
        {
            double x = SecondsToX(second);
            bool isMajorTick = tickIndex % TimelineMinorTickDivisions == 0;
            var tick = new Rectangle
            {
                Width = 1,
                Height = isMajorTick ? TimelineMajorTickHeight : TimelineMinorTickHeight,
                Fill = Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush,
            };

            Canvas.SetLeft(tick, x);
            Canvas.SetTop(tick, isMajorTick ? TimelineMajorTickTop : TimelineMinorTickTop);
            TimelineTicksCanvas.Children.Add(tick);

            if (!isMajorTick)
            {
                continue;
            }

            var label = new TextBlock
            {
                Text = FormatTimelineLabel(TimeSpan.FromSeconds(second)),
                Width = TimelineLabelWidth,
                Style = Microsoft.UI.Xaml.Application.Current.Resources["CaptionTextBlockStyle"] as Microsoft.UI.Xaml.Style,
                Foreground = Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush,
                TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            };

            Canvas.SetLeft(label, Math.Min(Math.Max(0, x - (TimelineLabelWidth / 2)), Math.Max(0, width - TimelineLabelWidth)));
            Canvas.SetTop(label, TimelineLabelTop);
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

    private void CenterTimelineOnPlayhead()
    {
        if (ViewModel.DurationSeconds <= 0 || TimelineScrollViewer.ViewportWidth <= 0)
        {
            return;
        }

        ScrollTimelineToAnchor(ViewModel.PositionSeconds, TimelineScrollViewer.ViewportWidth / 2);
    }

    private void ScrollTimelineToAnchor(double anchorSeconds, double viewportX)
    {
        if (ViewModel.DurationSeconds <= 0 || TimelineScrollViewer.ViewportWidth <= 0)
        {
            return;
        }

        TimelineScrollViewer.UpdateLayout();
        double targetX = SecondsToX(anchorSeconds);
        double maxOffset = Math.Max(0, TimelineScrollViewer.ScrollableWidth);
        double targetOffset = Math.Clamp(targetX - viewportX, 0, maxOffset);
        TimelineScrollViewer.ChangeView(targetOffset, null, null, disableAnimation: true);
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
            string outputPath = WaveformCachePathService.CreateCachePath(sourcePath);
            WaveformPlan plan = waveformPlanner.CreatePlan(ViewModel.FfmpegPath, sourcePath, outputPath, 2400, 160);
            AppLogger.Info($"Waveform generation starting: {sourcePath}");
            WaveformResult result = await waveformGenerator.GenerateAsync(plan, cancellationToken);

            if (!result.Succeeded)
            {
                AppLogger.Info($"Waveform generation skipped: {result.ErrorMessage}");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
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
