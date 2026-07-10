using Microsoft.UI;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using VideoCutEditor.ViewModels;
using Windows.Graphics;

namespace VideoCutEditor;

public sealed partial class InfoWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    public MainPageViewModel ViewModel { get; }

    public InfoWindow(MainPageViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        AppWindow.SetIcon("Assets/AppIcon.ico");
        ResizeToDefault();
    }

    private void ResizeToDefault()
    {
        nint hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        double scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(720 * scale), (int)(680 * scale)));
    }
}
