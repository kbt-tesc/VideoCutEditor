using System.Text.Json;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace VideoCutEditor;

public sealed partial class InfoWindow
{
    private readonly string placementPath = Path.Combine(App.SettingsDirectory, "info-window.json");
    private WindowPlacement? savedPlacement;
    private WindowPlacement? lastRestoredPlacement;

    private void InitializePlacement()
    {
        savedPlacement = LoadPlacement();
        if (savedPlacement is { } placement
            && DisplayArea.GetFromPoint(new PointInt32(placement.X, placement.Y), DisplayAreaFallback.Nearest) is { } display
            && display.WorkArea is { Width: > 0, Height: > 0 } workArea)
        {
            int width = Math.Min(placement.Width, workArea.Width);
            int height = Math.Min(placement.Height, workArea.Height);
            int left = display.OuterBounds.X + workArea.X;
            int top = display.OuterBounds.Y + workArea.Y;
            AppWindow.MoveAndResize(new RectInt32(
                Math.Clamp(placement.X, left, left + workArea.Width - width),
                Math.Clamp(placement.Y, top, top + workArea.Height - height),
                width,
                height));
        }
        else
        {
            ResizeToDefault();
        }

        RememberPlacement(AppWindow);
        AppWindow.Changed += PlacementChanged;
        AppWindow.Destroying += SavePlacement;
    }

    private WindowPlacement? LoadPlacement()
    {
        try
        {
            if (File.Exists(placementPath)
                && JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(placementPath)) is { Width: > 0, Height: > 0 } placement)
            {
                return placement;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            AppLogger.Error("Could not load the INFO window placement.", exception);
        }

        return null;
    }

    private void PlacementChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange || args.DidSizeChange || args.DidVisibilityChange)
        {
            RememberPlacement(sender);
        }
    }

    private void RememberPlacement(AppWindow window)
    {
        if (window.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored }
            && window.Size is { Width: > 0, Height: > 0 } size)
        {
            lastRestoredPlacement = new WindowPlacement(window.Position.X, window.Position.Y, size.Width, size.Height);
        }
    }

    private void SavePlacement(AppWindow sender, object args)
    {
        sender.Changed -= PlacementChanged;
        sender.Destroying -= SavePlacement;
        if (lastRestoredPlacement is not { } placement || placement == savedPlacement)
        {
            return;
        }

        string temporaryPath = $"{placementPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(placementPath)!);
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(placement));
                File.Move(temporaryPath, placementPath, overwrite: true);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLogger.Error("Could not save the INFO window placement.", exception);
        }
    }

    private readonly record struct WindowPlacement(int X, int Y, int Width, int Height);
}
