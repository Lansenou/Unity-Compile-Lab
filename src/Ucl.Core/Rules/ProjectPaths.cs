namespace Ucl.Core.Rules;

/// <summary>Helpers for project-relative paths with <c>/</c> separators.</summary>
public static class ProjectPaths
{
    /// <summary>The parent folder of a path, or empty for a top-level entry.</summary>
    public static string Folder(string path)
    {
        var i = path.LastIndexOf('/');
        return i < 0 ? string.Empty : path[..i];
    }

    /// <summary>The file name of a path.</summary>
    public static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    /// <summary>Folder chain from the folder itself up to the top: <c>a/b/c</c>, <c>a/b</c>, <c>a</c>.</summary>
    public static IEnumerable<string> SelfAndAncestors(string folder)
    {
        var f = folder;
        while (f.Length > 0)
        {
            yield return f;
            f = Folder(f);
        }
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="folder"/> or inside it.</summary>
    public static bool IsUnder(string path, string folder) =>
        path == folder || path.StartsWith(folder + "/", StringComparison.Ordinal);

    /// <summary>
    /// True when the Asset Database ignores the entry: a name starting with <c>.</c>, ending with <c>~</c>,
    /// equal to <c>cvs</c>, or with the <c>.tmp</c> extension.
    /// </summary>
    public static bool IsHiddenName(string name) =>
        name.StartsWith('.') || name.EndsWith('~') || name.Equals("cvs", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
}
