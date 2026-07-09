using System.Text.RegularExpressions;

namespace VideoCutEditor.Tests;

public sealed class UserInterfaceSourceTests
{
    [Fact]
    public void Main_page_exposes_output_folder_open_button_and_drag_timeline_handlers()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml"));
        string codeBehind = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml.cs"));

        Assert.Contains("OpenOutputDirectoryButton", xaml);
        Assert.Contains("PointerMoved=\"TimelineCanvas_PointerMoved\"", xaml);
        Assert.Contains("PointerReleased=\"TimelineCanvas_PointerReleased\"", xaml);
        Assert.Contains("PointerCanceled=\"TimelineCanvas_PointerCanceled\"", xaml);
        Assert.Matches(new Regex("x:Name=\"RangeStartMarker\"[\\s\\S]*?Width=\"1\"", RegexOptions.None, TimeSpan.FromSeconds(1)), xaml);
        Assert.Matches(new Regex("x:Name=\"RangeEndMarker\"[\\s\\S]*?Width=\"1\"", RegexOptions.None, TimeSpan.FromSeconds(1)), xaml);
        Assert.Contains("EditorRoot.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent", codeBehind);
        Assert.Contains("handledEventsToo: true", codeBehind);
    }

    [Fact]
    public void View_model_uses_japanese_status_and_information_messages()
    {
        string viewModel = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "ViewModels", "MainPageViewModel.cs"));

        Assert.Contains("動画が選択されていません", viewModel);
        Assert.Contains("準備完了", viewModel);
        Assert.Contains("推定サイズはまだ計算できません", viewModel);
        Assert.Contains("出力フォルダーを開きました", viewModel);
        Assert.Contains("Fast copy は可能な限りストリームを保持します", viewModel);
        Assert.DoesNotContain("No video selected", viewModel);
        Assert.DoesNotContain("Estimated size unavailable", viewModel);
    }

    [Fact]
    public void View_model_recovers_from_picker_com_failures()
    {
        string viewModel = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "ViewModels", "MainPageViewModel.cs"));

        Assert.Contains("using System.Runtime.InteropServices;", viewModel);
        Assert.True(Regex.Matches(viewModel, "catch \\(COMException exception\\)").Count >= 3);
        Assert.Contains("動画ファイルの選択を完了できませんでした", viewModel);
        Assert.Contains("出力フォルダーの選択を完了できませんでした", viewModel);
        Assert.Contains("実行ファイルの選択を完了できませんでした", viewModel);
        Assert.Contains("AppLogger.Error(\"Video picker failed\"", viewModel);
        Assert.Contains("AppLogger.Error(\"Output folder picker failed\"", viewModel);
        Assert.Contains("AppLogger.Error(\"Executable picker failed\"", viewModel);
    }

    [Fact]
    public void Timeline_zoom_uses_hundredth_steps_and_ctrl_wheel()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml"));
        string codeBehind = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml.cs"));
        string viewModel = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "ViewModels", "MainPageViewModel.cs"));

        Assert.Contains("StepFrequency=\"0.01\"", xaml);
        Assert.Contains("SmallChange=\"0.01\"", xaml);
        Assert.Contains("LargeChange=\"0.10\"", xaml);
        Assert.Contains("PointerWheelChanged=\"TimelineCanvas_PointerWheelChanged\"", xaml);
        Assert.Contains("TimelineZoomText => $\"{TimelineZoom:0.00}x\"", viewModel);
        Assert.Contains("private const double TimelineZoomStep = 0.01;", codeBehind);
        Assert.Contains("TimelineCanvas_PointerWheelChanged", codeBehind);
        Assert.Contains("VirtualKey.Control", codeBehind);
        Assert.Contains("MouseWheelDelta", codeBehind);
        Assert.Contains("AdjustTimelineZoom", codeBehind);
        Assert.Contains("Math.Round", codeBehind);
    }

    [Fact]
    public void Timeline_ruler_uses_minor_ticks_and_labels_above_major_ticks()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml"));
        string codeBehind = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml.cs"));

        Assert.Contains("Height=\"188\"", xaml);
        Assert.Contains("Height=\"180\"", xaml);
        Assert.Contains("x:Name=\"TimelineRulerBand\"", xaml);
        Assert.Matches(new Regex("x:Name=\"TimelineRulerBand\"[\\s\\S]*?Height=\"44\"", RegexOptions.None, TimeSpan.FromSeconds(1)), xaml);
        Assert.Matches(new Regex("x:Name=\"TimelineTicksCanvas\"[\\s\\S]*?Height=\"44\"", RegexOptions.None, TimeSpan.FromSeconds(1)), xaml);
        Assert.Contains("TimelineMinorTickDivisions", codeBehind);
        Assert.Contains("TimelineMajorTickTop", codeBehind);
        Assert.Contains("TimelineMinorTickTop", codeBehind);
        Assert.Contains("bool isMajorTick", codeBehind);
        Assert.Contains("if (!isMajorTick)", codeBehind);
        Assert.Contains("continue;", codeBehind);
        Assert.Contains("Canvas.SetTop(label, TimelineLabelTop)", codeBehind);
        Assert.Contains("Height = isMajorTick ? TimelineMajorTickHeight : TimelineMinorTickHeight", codeBehind);
    }

    [Fact]
    public void Timeline_zoom_and_jump_controls_preserve_expected_anchor_positions()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml"));
        string codeBehind = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml.cs"));

        Assert.Contains("AutomationProperties.AutomationId=\"LocatePlayheadButton\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"現在の再生位置へ移動\"", xaml);
        Assert.Contains("Click=\"LocatePlayheadButton_Click\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TimelineZoomOutButton\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"TimelineZoomInButton\"", xaml);
        Assert.Contains("Click=\"TimelineZoomOutButton_Click\"", xaml);
        Assert.Contains("Click=\"TimelineZoomInButton_Click\"", xaml);
        Assert.Contains("LocatePlayheadButton_Click", codeBehind);
        Assert.Contains("TimelineZoomOutButton_Click", codeBehind);
        Assert.Contains("TimelineZoomInButton_Click", codeBehind);
        Assert.Contains("CenterTimelineOnPlayhead", codeBehind);
        Assert.Contains("AdjustTimelineZoomAroundPlayhead", codeBehind);
        Assert.Contains("AdjustTimelineZoomAroundPointer", codeBehind);
        Assert.Contains("double anchorSeconds = XToSeconds(pointerPoint.Position.X);", codeBehind);
        Assert.Contains("double viewportX = pointerPoint.Position.X - TimelineScrollViewer.HorizontalOffset;", codeBehind);
        Assert.Contains("ScrollTimelineToAnchor(anchorSeconds, viewportX);", codeBehind);
    }

    [Fact]
    public void Play_pause_button_icon_tracks_playback_state()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml"));
        string codeBehind = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml.cs"));

        Assert.Contains("x:Name=\"PlayPauseIcon\"", xaml);
        Assert.Contains("Glyph=\"&#xE768;\"", xaml);
        Assert.Contains("PreviewPlayer.MediaPlayer.CurrentStateChanged += PreviewPlayerCurrentStateChanged;", codeBehind);
        Assert.Contains("PreviewPlayer.MediaPlayer.CurrentStateChanged -= PreviewPlayerCurrentStateChanged;", codeBehind);
        Assert.Contains("PreviewPlayerCurrentStateChanged", codeBehind);
        Assert.Contains("UpdatePlayPauseButtonState", codeBehind);
        Assert.Contains("PlayPauseIcon.Glyph = isPlaying ? \"\\uE769\" : \"\\uE768\";", codeBehind);
        Assert.Contains("ToolTipService.SetToolTip(PlayPauseButton, isPlaying ? \"一時停止\" : \"再生\");", codeBehind);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VideoCutEditor.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
