using System.Text.Json.Serialization;

namespace VideoCutEditor.Core.Models;

public sealed record FadeSettings
{
    public bool VideoFadeIn { get; init; }

    public bool VideoFadeOut { get; init; }

    public bool AudioFadeIn { get; init; }

    public bool AudioFadeOut { get; init; }

    public double DurationSeconds { get; init; } = 1;

    [JsonIgnore]
    public bool HasAnyFade => VideoFadeIn || VideoFadeOut || AudioFadeIn || AudioFadeOut;
}
