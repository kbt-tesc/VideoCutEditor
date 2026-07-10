using System.Diagnostics.CodeAnalysis;

namespace VideoCutEditor.Tests;

internal static class IntegrationTestRequirements
{
    public static void RequireFile([NotNull] string? path, string reason)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Skip.If(true, reason);
        }
    }

    public static void Require(bool condition, string reason)
    {
        if (!condition)
        {
            Skip.If(true, reason);
        }
    }
}
