using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.ViewModels;

public partial class MainPageViewModel
{
    private VideoCrop crop;
    private (uint Width, uint Height) cropPreviewSize;

    [ObservableProperty]
    public partial bool IsCropEditorOpen { get; set; }

    [ObservableProperty]
    public partial bool ShowCroppedPreview { get; set; }

    public VideoCrop Crop => crop;

    public VideoCropFrame? CropFrame { get; private set; }

    public double? CropDisplayAspectRatio => CropFrame?.ResolveDisplayAspectRatio(
        cropPreviewSize.Width, cropPreviewSize.Height);

    public bool CanEditCrop => CropFrame is not null && !IsExporting;

    public bool CanFastCopy => Crop.IsEmpty;

    public void SetCropPreviewSize(uint width, uint height) => cropPreviewSize = (width, height);

    public IReadOnlyList<CropAdjustment> CropAdjustments { get; }

    public string CropSizeText
    {
        get
        {
            if (CropFrame is not { } frame)
            {
                return string.Empty;
            }

            int width = Crop.GetWidth(frame);
            int height = Crop.GetHeight(frame);
            int outputWidth = VideoCrop.GetPaddedSize(width);
            int outputHeight = VideoCrop.GetPaddedSize(height);
            return Crop.IsEmpty || (width == outputWidth && height == outputHeight)
                ? $"切り抜き {width} × {height} px"
                : $"切り抜き {width} × {height} px / 出力 {outputWidth} × {outputHeight} px";
        }
    }

    public void UpdateCrop(VideoCrop value)
    {
        if (CanEditCrop && CropFrame is { } frame && value.Fits(frame))
        {
            SetCrop(value);
        }
    }

    [RelayCommand]
    private void ResetCrop() => UpdateCrop(default);

    private void SetCrop(VideoCrop value)
    {
        if (value == crop)
        {
            return;
        }

        bool availabilityChanged = crop.IsEmpty != value.IsEmpty;
        crop = value;
        if (!value.IsEmpty && CurrentExportMode == ExportMode.FastCopy)
        {
            ConvertHdrToSdrEnabled = false;
            SelectedExportModeIndex = 1;
            EnsureSelectedOutputContainerIsAvailable();
        }

        CropAdjustments[0].Pixels = crop.Left;
        CropAdjustments[1].Pixels = crop.Top;
        CropAdjustments[2].Pixels = crop.Right;
        CropAdjustments[3].Pixels = crop.Bottom;
        OnPropertyChanged(nameof(Crop));
        OnPropertyChanged(nameof(CropSizeText));
        if (availabilityChanged)
        {
            OnPropertyChanged(nameof(CanFastCopy));
            OnPropertyChanged(nameof(ExportNoticeText));
        }
    }

    private void RefreshCropFrame(MediaInfo? mediaInfo)
    {
        CropFrame = VideoCropFrame.FromMedia(mediaInfo);
        IsCropEditorOpen = false;
        ShowCroppedPreview = false;
        SetCrop(default);
        OnPropertyChanged(nameof(CanEditCrop));
        OnPropertyChanged(nameof(CropSizeText));
    }

    private CropAdjustment CreateCropAdjustment(string name, string id, Func<int, VideoCrop> change) =>
        new(name, id, step =>
        {
            if (CropFrame is { } frame)
            {
                UpdateCrop(Crop.Resize(frame, change(step)));
            }
        });
}

public partial class CropAdjustment(string name, string id, Action<int> adjust) : ObservableObject
{
    public string Name => name;

    public string PixelsId => $"Crop{id}PixelsText";

    [ObservableProperty]
    public partial int Pixels { get; set; }

    public string GetStepId(string step) => $"Crop{id}{step}Button";

    public string DescribeStep(string step) => $"{name}の切り取り {step} px";

    [RelayCommand]
    private void Adjust(string? step)
    {
        if (int.TryParse(step, out int delta))
        {
            adjust(delta);
        }
    }
}
