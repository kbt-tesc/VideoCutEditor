using Microsoft.UI;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using VideoCutEditor.ViewModels;
using Windows.Graphics;

namespace VideoCutEditor;

public sealed partial class InfoWindow : Window
{
    private const int WindowOwnerIndex = -8;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr64(nint hWnd, int index, nint newValue);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(nint hWnd, int index, int newValue);

    public MainPageViewModel ViewModel { get; }

    public static Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public InfoWindow(MainPageViewModel viewModel, nint ownerWindowHandle)
    {
        ViewModel = viewModel;
        InitializeComponent();
        SetOwner(ownerWindowHandle);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        ResizeToDefault();
    }

    private void SetOwner(nint ownerWindowHandle)
    {
        nint hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        if (Environment.Is64BitProcess)
        {
            SetWindowLongPtr64(hwnd, WindowOwnerIndex, ownerWindowHandle);
        }
        else
        {
            SetWindowLong32(hwnd, WindowOwnerIndex, ownerWindowHandle.ToInt32());
        }
    }

    private void ResizeToDefault()
    {
        nint hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        double scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(720 * scale), (int)(680 * scale)));
    }
}
