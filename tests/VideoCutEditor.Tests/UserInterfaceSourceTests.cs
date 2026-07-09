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
