using System;
using System.Reflection;

namespace ResonanceHunter.Core.Persistence;

/// <summary>
/// Which build this binary is — the version plus the git commit the SDK stamped into the assembly.
/// </summary>
/// <remarks>
/// The .NET 8 SDK appends the current commit hash to <c>AssemblyInformationalVersion</c> on any
/// build made inside a git checkout ("1.0.0+c80496439…"), with no project configuration needed.
/// That stamp is the one identity a feedback report can carry that maps a complaint to the exact
/// code that produced it — "the reveal freezes" means nothing without knowing WHICH reveal shipped.
/// Read once and cached: the attribute cannot change while the process runs.
/// </remarks>
public static class BuildStamp
{
    /// <summary>The full stamp, e.g. "1.0.0+c804964390f3e811bbc4db21adfc200b62d7c221". Never empty.</summary>
    public static string Full { get; } =
        typeof(BuildStamp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            is { Length: > 0 } v ? v : "unknown";

    /// <summary>The stamp fit for a screen corner: the version plus the first 8 commit characters.</summary>
    public static string Short { get; } = Shorten(Full);

    private static string Shorten(string full)
    {
        var plus = full.IndexOf('+');
        if (plus < 0) return full;
        var sha = full[(plus + 1)..];
        return $"{full[..plus]}+{sha[..Math.Min(8, sha.Length)]}";
    }
}
