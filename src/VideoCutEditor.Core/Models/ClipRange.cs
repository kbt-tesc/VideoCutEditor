namespace VideoCutEditor.Core.Models;

public readonly record struct ClipRange(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;

    public bool IsValid => Start >= TimeSpan.Zero && End > Start;

    public void Validate()
    {
        if (Start < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Start), "Start must be zero or later.");
        }

        if (End <= Start)
        {
            throw new ArgumentException("End must be later than start.", nameof(End));
        }
    }
}
