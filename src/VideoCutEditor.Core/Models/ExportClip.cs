namespace VideoCutEditor.Core.Models;

public sealed record ExportClip(ClipRange Range, string Title)
{
    public string OutputFileName => $"{Title}.mp4";

    public string GetOutputFileName(OutputContainer container) =>
        $"{Title}{container.GetFileExtension()}";

    public string StartText => FormatTime(Range.Start);

    public string EndText => FormatTime(Range.End);

    private static string FormatTime(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";
}
