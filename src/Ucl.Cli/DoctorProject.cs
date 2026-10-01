namespace Ucl.Cli;

/// <summary>The project part of <c>ucl doctor</c>.</summary>
/// <param name="Root">Absolute project root.</param>
/// <param name="Version">Editor version from ProjectVersion.txt, or null when unreadable.</param>
/// <param name="Editor">The matching editor install root, or null when none is installed.</param>
/// <param name="PackageSources">Resolved package count per source (embedded, local, cache, download, builtin), sorted.</param>
/// <param name="Unresolved">Packages that do not resolve, as <c>name@version</c> or the problem message.</param>
internal sealed record DoctorProject(
    string Root,
    string? Version,
    string? Editor,
    IReadOnlyList<(string Source, int Count)> PackageSources,
    IReadOnlyList<string> Unresolved);
