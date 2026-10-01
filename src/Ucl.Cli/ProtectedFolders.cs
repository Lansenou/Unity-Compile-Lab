namespace Ucl.Cli;

/// <summary>The project folders ucl never writes to (docs/architecture.md, "Read-only contract").</summary>
internal static class ProtectedFolders
{
    private static readonly string[] Names = ["Assets", "Packages", "ProjectSettings"];

    /// <summary>The protected folder that contains <paramref name="path"/> (or is it), or null.</summary>
    public static string? Containing(string projectRoot, string path)
    {
        var full = Path.GetFullPath(path);
        foreach (var name in Names)
        {
            var rel = Path.GetRelativePath(Path.Combine(projectRoot, name), full);
            if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
            {
                return name;
            }
        }

        return null;
    }
}
