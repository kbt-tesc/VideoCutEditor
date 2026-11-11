using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VideoCutEditor.Core.Models;
using Windows.Foundation;

namespace VideoCutEditor;

public sealed partial class MainPage
{
    private const double CropHandleHitRadius = 9;
    private bool cropPreviewReady;
    private Rect cropPreviewBounds;
    private Rect cropSelectionBounds;
    private CropDrag? cropDrag;

    private void PreviewSurface_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCropOverlay();

    private void UpdateCropOverlay()
    {
        if (CropOverlay is null)
        {
            return;
        }

        if (!ViewModel.IsCropEditorOpen || !cropPreviewReady || ViewModel.CropFrame is not { } frame
            || PreviewSurface.ActualWidth <= 0 || PreviewSurface.ActualHeight <= 0)
        {
            cropDrag = null;
            CropOverlay.ReleasePointerCaptures();
            CropOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        double width = Math.Min(PreviewSurface.ActualWidth, PreviewSurface.ActualHeight * frame.DisplayAspectRatio);
        double height = width / frame.DisplayAspectRatio;
        cropPreviewBounds = new Rect(
            (PreviewSurface.ActualWidth - width) / 2,
            (PreviewSurface.ActualHeight - height) / 2,
            width,
            height);

        VideoCrop crop = ViewModel.Crop;
        double scaleX = width / frame.Width;
        double scaleY = height / frame.Height;
        cropSelectionBounds = new Rect(
            cropPreviewBounds.X + crop.Left * scaleX,
            cropPreviewBounds.Y + crop.Top * scaleY,
            crop.GetWidth(frame) * scaleX,
            crop.GetHeight(frame) * scaleY);

        CropMaskFrame.Rect = cropPreviewBounds;
        CropMaskSelection.Rect = cropSelectionBounds;
        Canvas.SetLeft(CropGuide, cropSelectionBounds.X);
        Canvas.SetTop(CropGuide, cropSelectionBounds.Y);
        CropGuide.Width = cropSelectionBounds.Width;
        CropGuide.Height = cropSelectionBounds.Height;
        CropOverlay.IsHitTestVisible = ViewModel.CanEditCrop;
        if (!CropOverlay.IsHitTestVisible)
        {
            cropDrag = null;
            CropOverlay.ReleasePointerCaptures();
        }

        CropOverlay.Visibility = Visibility.Visible;
    }

    private void CropOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(CropOverlay);
        Point point = pointer.Position;
        Rect selection = cropSelectionBounds;
        if (cropDrag is not null || !pointer.Properties.IsLeftButtonPressed
            || point.X < selection.Left - CropHandleHitRadius || point.X > selection.Right + CropHandleHitRadius
            || point.Y < selection.Top - CropHandleHitRadius || point.Y > selection.Bottom + CropHandleHitRadius
            || !CropOverlay.CapturePointer(e.Pointer))
        {
            return;
        }

        cropDrag = new CropDrag(
            e.Pointer.PointerId,
            point,
            ViewModel.Crop,
            HitCropEdge(point.X, selection.Left, selection.Right),
            HitCropEdge(point.Y, selection.Top, selection.Bottom));
        e.Handled = true;
    }

    private static int HitCropEdge(double position, double start, double end)
    {
        double startDistance = Math.Abs(position - start);
        double endDistance = Math.Abs(position - end);
        if (Math.Min(startDistance, endDistance) > CropHandleHitRadius)
        {
            return 0;
        }

        return startDistance <= endDistance ? -1 : 1;
    }

    private void CropOverlay_PointerMoved(object sender, PointerRoutedEventArgs e) => ApplyCropDrag(e);

    private void CropOverlay_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ApplyCropDrag(e);
        CropOverlay_PointerCanceled(sender, e);
    }

    private void CropOverlay_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (cropDrag?.PointerId == e.Pointer.PointerId)
        {
            cropDrag = null;
            CropOverlay.ReleasePointerCapture(e.Pointer);
        }
    }

    private void ApplyCropDrag(PointerRoutedEventArgs e)
    {
        if (cropDrag is not { } drag || drag.PointerId != e.Pointer.PointerId || ViewModel.CropFrame is not { } frame)
        {
            return;
        }

        Point point = e.GetCurrentPoint(CropOverlay).Position;
        int dx = (int)Math.Round((point.X - drag.Start.X) * frame.Width / cropPreviewBounds.Width);
        int dy = (int)Math.Round((point.Y - drag.Start.Y) * frame.Height / cropPreviewBounds.Height);
        VideoCrop crop = drag.HorizontalEdge == 0 && drag.VerticalEdge == 0
            ? drag.InitialCrop.Move(dx, dy)
            : drag.InitialCrop.Resize(frame, new VideoCrop(
                drag.HorizontalEdge < 0 ? dx : 0,
                drag.VerticalEdge < 0 ? dy : 0,
                drag.HorizontalEdge > 0 ? -dx : 0,
                drag.VerticalEdge > 0 ? -dy : 0));

        ViewModel.UpdateCrop(crop);
        e.Handled = true;
    }

    private sealed record CropDrag(uint PointerId, Point Start, VideoCrop InitialCrop, int HorizontalEdge, int VerticalEdge);
}

public sealed class CropOverlayCanvas : Canvas
{
    protected override AutomationPeer OnCreateAutomationPeer() => new CropOverlayPeer(this);

    private sealed class CropOverlayPeer(CropOverlayCanvas owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(CropOverlayCanvas);
    }
}
