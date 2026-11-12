namespace VideoCutEditor.Core.Models;

public readonly record struct VideoCrop(int Left, int Top, int Right, int Bottom)
{
    public const int MinimumSize = 2;

    public bool IsEmpty => this == default;

    public int GetWidth(VideoCropFrame frame) => frame.Width - Left - Right;

    public int GetHeight(VideoCropFrame frame) => frame.Height - Top - Bottom;

    public static int GetPaddedSize(int size) => size + (size % 2);

    public bool Fits(VideoCropFrame frame) =>
        Left >= 0 && Top >= 0 && Right >= 0 && Bottom >= 0
        && (long)Left + Right <= frame.Width - MinimumSize
        && (long)Top + Bottom <= frame.Height - MinimumSize;

    public VideoCrop Resize(VideoCropFrame frame, VideoCrop change)
    {
        int left = Math.Clamp(Left + change.Left, 0, frame.Width - Right - MinimumSize);
        int top = Math.Clamp(Top + change.Top, 0, frame.Height - Bottom - MinimumSize);
        return new VideoCrop(
            left,
            top,
            Math.Clamp(Right + change.Right, 0, frame.Width - left - MinimumSize),
            Math.Clamp(Bottom + change.Bottom, 0, frame.Height - top - MinimumSize));
    }

    public VideoCrop Move(int x, int y)
    {
        x = Math.Clamp(x, -Left, Right);
        y = Math.Clamp(y, -Top, Bottom);
        return new VideoCrop(Left + x, Top + y, Right - x, Bottom - y);
    }
}

public readonly record struct VideoCropFrame(int Width, int Height, double DisplayAspectRatio)
{
    public double ResolveDisplayAspectRatio(uint previewWidth, uint previewHeight)
    {
        // Playback dimensions are integral. Keep the probed ratio when rounding explains the difference.
        if (previewWidth == 0 || previewHeight == 0
            || Math.Abs(previewWidth - previewHeight * DisplayAspectRatio) <= 1
            || Math.Abs(previewHeight - previewWidth / DisplayAspectRatio) <= 1)
        {
            return DisplayAspectRatio;
        }

        return (double)previewWidth / previewHeight;
    }

    public static VideoCropFrame? FromMedia(MediaInfo? mediaInfo)
    {
        MediaStreamInfo[]? videoStreams = mediaInfo?.Streams
            .Where(stream => string.Equals(stream.CodecType, "video", StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (videoStreams is not [{ Width: >= VideoCrop.MinimumSize and int width,
            Height: >= VideoCrop.MinimumSize and int height, IsAttachedPicture: false } video])
        {
            return null;
        }

        double rotation = video.RotationDegrees % 360;
        double quarterTurns = Math.Round(rotation / 90);
        if (Math.Abs(rotation - (quarterTurns * 90)) > 0.01 || !double.IsFinite(rotation))
        {
            return null;
        }

        double aspect = width * video.SampleAspectRatio / height;
        if (!double.IsFinite(aspect) || aspect <= 0)
        {
            return null;
        }

        return Math.Abs(quarterTurns % 2) == 1
            ? new VideoCropFrame(height, width, 1 / aspect)
            : new VideoCropFrame(width, height, aspect);
    }
}
