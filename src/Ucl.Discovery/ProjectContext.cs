using Ucl.Core.Graph;
using Ucl.Core.Model;

namespace Ucl.Discovery;

/// <summary>A loaded project: its inventory, how logical paths map to disk, and the problems found.</summary>
public sealed record ProjectContext
{
    /// <summary>Absolute project root.</summary>
    public required string Root { get; init; }

    /// <summary>The inventory, or null when loading failed (see <see cref="Problems"/>).</summary>
    public ProjectInventory? Inventory { get; init; }

    /// <summary>Logical package root (<c>Packages/&lt;name&gt;</c>) to absolute folder.</summary>
    public IReadOnlyDictionary<string, string> PackageRoots { get; init; } = new Dictionary<string, string>();

    /// <summary>Configuration problems (exit 3).</summary>
    public IReadOnlyList<Problem> Problems { get; init; } = [];

    /// <summary>Maps a logical path (<c>Assets/...</c> or <c>Packages/&lt;name&gt;/...</c>) to an absolute path.</summary>
    public string ToPhysical(string logical)
    {
        if (logical.StartsWith("Packages/", StringComparison.Ordinal))
        {
            var second = logical.IndexOf('/', "Packages/".Length);
            var root = second < 0 ? logical : logical[..second];
            if (PackageRoots.TryGetValue(root, out var physical))
            {
                return second < 0 ? physical : Path.Combine(physical, logical[(second + 1)..]);
            }
        }

        return Path.Combine(Root, logical);
    }

    /// <summary>Maps an absolute path back to its logical path, or null when it is outside the project and its packages.</summary>
    public string? ToLogical(string physical)
    {
        var full = Path.GetFullPath(physical);
        foreach (var (logical, root) in PackageRoots.OrderByDescending(p => p.Value.Length))
        {
            var rel = Path.GetRelativePath(root, full);
            if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
            {
                return rel == "." ? logical : $"{logical}/{rel.Replace('\\', '/')}";
            }
        }

        var r = Path.GetRelativePath(Root, full);
        return r.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(r) ? null : r.Replace('\\', '/');
    }
}
