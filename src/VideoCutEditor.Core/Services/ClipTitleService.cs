namespace VideoCutEditor.Core.Services;

public sealed class ClipTitleService
{
    public string CreateAvailableTitle(
        string? requestedTitle,
        string outputDirectory,
        IEnumerable<string> registeredTitles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(registeredTitles);

        var unavailableTitles = new HashSet<string>(registeredTitles, StringComparer.OrdinalIgnoreCase);
        string normalizedTitle = Normalize(requestedTitle);
        bool isPlaceholder = string.IsNullOrWhiteSpace(normalizedTitle);
        string baseTitle = isPlaceholder ? "クリップ" : normalizedTitle;
        int suffix = isPlaceholder ? 1 : 0;

        while (true)
        {
            string candidate = suffix == 0 ? baseTitle : $"{baseTitle}_{suffix}";
            string outputPath = Path.Combine(outputDirectory, $"{candidate}.mp4");
            if (!unavailableTitles.Contains(candidate) && !File.Exists(outputPath))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static string Normalize(string? requestedTitle)
    {
        string title = requestedTitle?.Trim() ?? string.Empty;
        if (title.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            title = title[..^4].TrimEnd();
        }

        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        title = string.Concat(title.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character));
        return title.Trim().TrimEnd('.');
    }
}
