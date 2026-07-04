using Microsoft.UI.Xaml;
using Microsoft.UI;
using System.Runtime.InteropServices;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VideoCutEditor;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    public MainWindow()
    {
        AppLogger.Info("MainWindow constructor starting");

        try
        {
            InitializeComponent();
            AppLogger.Info("MainWindow InitializeComponent completed");
        }
        catch (Exception exception)
        {
            AppLogger.Error("MainWindow InitializeComponent failed", exception);
            throw;
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        ResizeToEditorDefault();

        try
        {
            AppLogger.Info("Navigating RootFrame to MainPage");
            RootFrame.Navigate(typeof(MainPage));
            AppLogger.Info("RootFrame navigation completed");
        }
        catch (Exception exception)
        {
            AppLogger.Error("RootFrame navigation failed", exception);
            throw;
        }
    }

    private void ResizeToEditorDefault()
    {
        AppLogger.Info("Resizing main window");
        nint hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        double scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1280 * scale), (int)(800 * scale)));
    }
}
