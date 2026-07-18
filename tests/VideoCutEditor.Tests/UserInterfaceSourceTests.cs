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
    public void Winrt_picker_commands_keep_explicit_com_exception_boundaries()
    {
        string viewModel = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "ViewModels", "MainPageViewModel.cs"));

        Assert.Contains("using System.Runtime.InteropServices;", viewModel);
        Assert.True(Regex.Matches(viewModel, "catch \\(COMException exception\\)").Count >= 3);
        Assert.Contains("AppLogger.Error(\"Video picker failed\"", viewModel);
        Assert.Contains("AppLogger.Error(\"Output folder picker failed\"", viewModel);
        Assert.Contains("AppLogger.Error(\"Executable picker failed\"", viewModel);
    }

    [Fact]
    public void Timeline_zoom_uses_hundredth_steps_and_tenth_step_ctrl_wheel()
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
        Assert.Contains("private const double TimelineWheelZoomStep = 0.10;", codeBehind);
        Assert.Contains("TimelineCanvas_PointerWheelChanged", codeBehind);
        Assert.Contains("VirtualKey.Control", codeBehind);
        Assert.Contains("MouseWheelDelta", codeBehind);
        Assert.Contains("delta > 0 ? TimelineWheelZoomStep : -TimelineWheelZoomStep", codeBehind);
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
        Assert.Contains("Math.Round(second, 6, MidpointRounding.AwayFromZero)", codeBehind);
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

    [Fact]
    public void Settings_output_filename_and_info_surfaces_are_separated()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml"));
        string codeBehind = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml.cs"));
        string infoWindowXaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "InfoWindow.xaml"));
        string infoWindowCodeBehind = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "InfoWindow.xaml.cs"));
        Assert.Contains("AutomationProperties.AutomationId=\"OutputFileNameTextBox\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"OutputFileNameCollisionWarningText\"", xaml);
        Assert.Contains("ViewModel.IsManualOutputFileNameCollision", xaml);
        Assert.Contains("Text=\"{x:Bind ViewModel.PlannedOutputFileName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"OpenOutputDirectoryButton\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"OpenSettingsButton\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ShowInfoButton\"", xaml);
        Assert.Contains("x:Name=\"SettingsDialog\"", xaml);
        Assert.DoesNotContain("x:Name=\"InfoDialog\"", xaml);
        Assert.Matches(new Regex("x:Name=\"SettingsDialog\"[\\s\\S]*?AutomationProperties.AutomationId=\"ToolDirectoryPathTextBox\"", RegexOptions.None, TimeSpan.FromSeconds(1)), xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"BrowseToolDirectoryButton\"", xaml);
        Assert.Contains("ViewModel.IsIndividualToolPathSelectionVisible", xaml);
        Assert.Matches(new Regex("x:Name=\"SettingsDialog\"[\\s\\S]*?AutomationProperties.AutomationId=\"FfmpegPathTextBox\"", RegexOptions.None, TimeSpan.FromSeconds(1)), xaml);
        Assert.Matches(new Regex("x:Name=\"SettingsDialog\"[\\s\\S]*?AutomationProperties.AutomationId=\"FfprobePathTextBox\"", RegexOptions.None, TimeSpan.FromSeconds(1)), xaml);
        Assert.Matches(new Regex("x:Name=\"SettingsDialog\"[\\s\\S]*?AutomationProperties.AutomationId=\"OutputDirectoryTextBox\"", RegexOptions.None, TimeSpan.FromSeconds(1)), xaml);
        Assert.Contains("x:Class=\"VideoCutEditor.InfoWindow\"", infoWindowXaml);
        Assert.Contains("<ScrollViewer", infoWindowXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"EncoderSummaryTextBox\"", infoWindowXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ExportLogTextBox\"", infoWindowXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"MediaInfoTextBox\"", infoWindowXaml);
        Assert.Contains("OpenSettingsButton_Click", codeBehind);
        Assert.Contains("ShowInfoButton_Click", codeBehind);
        Assert.Contains("private InfoWindow? infoWindow;", codeBehind);
        Assert.Contains("infoWindow ??= new InfoWindow(ViewModel, App.WindowHandle);", codeBehind);
        Assert.Contains("infoWindow.Activate();", codeBehind);
        Assert.DoesNotContain("await InfoDialog.ShowAsync();", codeBehind);
        Assert.Contains("public InfoWindow(MainPageViewModel viewModel, nint ownerWindowHandle)", infoWindowCodeBehind);
        Assert.Contains("SetOwner(ownerWindowHandle);", infoWindowCodeBehind);
    }

    [Fact]
    public void Hdr_to_sdr_option_is_contextual_and_defaulted_for_hdr_media()
    {
        string xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "MainPage.xaml"));
        string viewModel = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "ViewModels", "MainPageViewModel.cs"));

        Assert.Contains("AutomationProperties.AutomationId=\"ConvertHdrToSdrCheckBox\"", xaml);
        Assert.Contains("HDRをSDRに変換", xaml);
        Assert.Contains("IsHdrToSdrOptionVisible", viewModel);
        Assert.Contains("ConvertHdrToSdrEnabled", viewModel);
        Assert.Contains("private bool convertHdrToSdrEnabled;", viewModel);
        Assert.Contains("public bool ConvertHdrToSdrEnabled", viewModel);
        Assert.Contains("SetProperty(ref convertHdrToSdrEnabled, value)", viewModel);
        Assert.DoesNotContain("public partial bool ConvertHdrToSdrEnabled", viewModel);
        Assert.Contains("HasHdrVideoStream", viewModel);
        Assert.Contains("ConvertHdrToSdrEnabled = HasHdrVideoStream(info);", viewModel);
        Assert.Contains("HDR動画です。Fast copyではHDRのまま書き出します", viewModel);
        Assert.Contains("ConvertHdrToSdr = CurrentExportMode == ExportMode.Reencode && IsHdrToSdrOptionVisible && ConvertHdrToSdrEnabled", viewModel);
    }

    [Fact]
    public void Multi_clip_registration_uses_a_modeless_responsive_list_window()
    {
        string root = FindRepositoryRoot();
        string mainXaml = File.ReadAllText(Path.Combine(root, "src", "VideoCutEditor", "MainPage.xaml"));
        string mainCodeBehind = File.ReadAllText(Path.Combine(root, "src", "VideoCutEditor", "MainPage.xaml.cs"));
        string listXaml = File.ReadAllText(Path.Combine(root, "src", "VideoCutEditor", "ExportListWindow.xaml"));
        string listCodeBehind = File.ReadAllText(Path.Combine(root, "src", "VideoCutEditor", "ExportListWindow.xaml.cs"));

        Assert.Contains("AutomationProperties.AutomationId=\"ClipTitleTextBox\"", mainXaml);
        Assert.Contains("ViewModel.ClipTitleText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged", mainXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"AddClipButton\"", mainXaml);
        Assert.Contains("Command=\"{x:Bind ViewModel.AddClipCommand}\"", mainXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ShowExportListButton\"", mainXaml);
        Assert.Contains("ViewModel.ExportListRequested += ViewModelExportListRequested;", mainCodeBehind);
        Assert.Contains("exportListWindow ??= new ExportListWindow(ViewModel, App.WindowHandle);", mainCodeBehind);
        Assert.Contains("exportListWindow.Activate();", mainCodeBehind);

        Assert.Contains("x:Class=\"VideoCutEditor.ExportListWindow\"", listXaml);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.RegisteredClips, Mode=OneWay}\"", listXaml);
        Assert.True(Regex.Matches(listXaml, "<ColumnDefinition Width=\"120\" />").Count >= 4);
        Assert.Contains("<ColumnDefinition Width=\"*\" />", listXaml);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Stretch\" />", listXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"RemoveClipButton\"", listXaml);
        Assert.Contains("public ExportListWindow(MainPageViewModel viewModel, nint ownerWindowHandle)", listCodeBehind);
        Assert.Contains("SetOwner(ownerWindowHandle);", listCodeBehind);
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
