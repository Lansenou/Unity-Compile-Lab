using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Parsing;
using Ucl.Core.Rules;

namespace Ucl.Discovery;

/// <summary>Finds each package's folder in Unity's order: embedded, <c>file:</c>, <c>Library/PackageCache</c>, download cache.</summary>
internal sealed class PackageResolver(IFileSystem fs, IEnvironment env, string projectRoot)
{
    private const string BuiltInPrefix = "com.unity.modules.";

    private readonly string packagesDir = Path.Combine(projectRoot, "Packages");
    private readonly string packageCacheDir = Path.Combine(projectRoot, "Library", "PackageCache");

    /// <summary>The download cache filled by <c>ucl fetch</c>.</summary>
    public string DownloadCacheDir =>
        env.GetVariable("UCL_PACKAGE_CACHE") is { Length: > 0 } custom
            ? Path.GetFullPath(custom)
            : Path.Combine(env.HomeDirectory, ".cache", "ucl", "packages");

    /// <summary>Resolves the manifest's packages, plus embedded ones, plus (without a lock file) transitive dependencies.</summary>
    /// <param name="manifest">Parsed manifest and lock file.</param>
    /// <param name="hasLock">True when <c>packages-lock.json</c> was read; it already lists indirect dependencies.</param>
    public PackageResolution Resolve(PackageManifest manifest, bool hasLock)
    {
        var problems = new List<Problem>();
        var embedded = IndexEmbedded(problems);
        var resolved = new SortedDictionary<string, ResolvedPackage>(StringComparer.Ordinal);
        var roots = new SortedDictionary<string, string>(StringComparer.Ordinal);

        // First request wins, so manifest versions take precedence over versions asked for by dependencies.
        var queue = new Queue<(string Name, string Version)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Enqueue(string name, string version)
        {
            if (seen.Add(name))
            {
                queue.Enqueue((name, version));
            }
        }

        foreach (var decl in manifest.Packages)
        {
            Enqueue(decl.Name, decl.Version.Length > 0 ? decl.Version : decl.Requested);
        }

        // Unity loads every folder in Packages/ that has a package.json, listed in the manifest or not.
        foreach (var name in embedded.Keys)
        {
            Enqueue(name, string.Empty);
        }

        while (queue.Count > 0)
        {
            var (name, version) = queue.Dequeue();
            if (name.StartsWith(BuiltInPrefix, StringComparison.Ordinal))
            {
                resolved[name] = new ResolvedPackage(name, version, null, "builtin");
                continue;
            }

            var found = Find(name, version, embedded);
            if (found is null)
            {
                problems.Add(NotFound(name, version));
                continue;
            }

            var (folder, source, json) = found.Value;
            var logical = $"Packages/{name}";
            resolved[name] = new ResolvedPackage(name, json.Version.Length > 0 ? json.Version : version, logical, source);
            roots[logical] = folder;
            if (!hasLock)
            {
                foreach (var (dep, depVersion) in json.Dependencies)
                {
                    Enqueue(dep, depVersion);
                }
            }
        }

        return new PackageResolution(resolved.Values.ToList(), roots, problems);
    }

    private SortedDictionary<string, (string Folder, PackageJson Json)> IndexEmbedded(List<Problem> problems)
    {
        var index = new SortedDictionary<string, (string, PackageJson)>(StringComparer.Ordinal);
        foreach (var dir in fs.ListDirectories(packagesDir))
        {
            if (ProjectPaths.IsHiddenName(dir))
            {
                continue;
            }

            var folder = Path.Combine(packagesDir, dir);
            var read = PackageJson.Read(fs, folder);
            if (read is not { } r)
            {
                continue;
            }

            if (!r.Ok)
            {
                problems.Add(new Problem(ProblemIds.BadProjectFile, $"Packages/{dir}/package.json: {r.Error}", $"Packages/{dir}/package.json"));
                continue;
            }

            // Folder names are listed in ordinal order, so the first folder claiming a name wins deterministically.
            if (r.Value!.Name.Length > 0)
            {
                index.TryAdd(r.Value.Name, (folder, r.Value));
            }
        }

        return index;
    }

    private (string Folder, string Source, PackageJson Json)? Find(
        string name, string version, SortedDictionary<string, (string Folder, PackageJson Json)> embedded)
    {
        if (embedded.TryGetValue(name, out var e))
        {
            return (e.Folder, "embedded", e.Json);
        }

        if (version.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var folder = LocalFolder(version["file:".Length..]);
            return PackageJson.ReadValid(fs, folder) is { } local ? (folder, "local", local) : null;
        }

        if (FindInPackageCache(name, version) is { } cached)
        {
            return (cached.Folder, "cache", cached.Json);
        }

        if (IsPlainVersion(version))
        {
            var folder = Path.Combine(DownloadCacheDir, $"{name}@{version}");
            if (PackageJson.ReadValid(fs, folder) is { } downloaded && (downloaded.Name.Length == 0 || downloaded.Name == name))
            {
                return (folder, "download", downloaded);
            }
        }

        return null;
    }

    private string LocalFolder(string path)
    {
        // "file:///abs/path" and "file:///C:/x" are URI forms; "file:../x" and "file:C:/x" are plain paths.
        if (path.StartsWith("//", StringComparison.Ordinal))
        {
            path = path[2..];
            if (path.Length >= 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':')
            {
                path = path[1..];
            }
        }

        path = Uri.UnescapeDataString(path).Replace('/', Path.DirectorySeparatorChar);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(packagesDir, path)));
    }

    private (string Folder, PackageJson Json)? FindInPackageCache(string name, string lockVersion)
    {
        // Unity 6 names cache folders name@hash, older versions name@version; package.json is the only reliable key.
        var candidates = fs.ListDirectories(packageCacheDir)
            .Where(d => d.StartsWith(name + "@", StringComparison.Ordinal))
            .Select(d => Path.Combine(packageCacheDir, d))
            .Select(f => (Folder: f, Json: PackageJson.ReadValid(fs, f)))
            .Where(c => c.Json?.Name == name)
            .Select(c => (c.Folder, Json: c.Json!))
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var exact = candidates.Where(c => c.Json.Version == lockVersion).ToList();
        return (exact.Count > 0 ? exact : candidates).OrderBy(c => c.Folder, StringComparer.Ordinal).Last();
    }

    private static bool IsPlainVersion(string version) => SemanticVersion.Parse(version).Ok;

    private Problem NotFound(string name, string version)
    {
        var shown = version.Length > 0 ? $"{name}@{version}" : name;
        // Project-relative and symbolic paths only: reports must be identical on every machine.
        var download = IsPlainVersion(version)
            ? $"the download cache ($UCL_PACKAGE_CACHE or ~/.cache/ucl/packages)/{name}@{version}"
            : "the download cache (only for registry versions)";
        var hint = IsPlainVersion(version)
            ? "Run `ucl fetch` to download registry packages, or open the project in Unity once to fill Library/PackageCache."
            : "Open the project in Unity once so it fills Library/PackageCache (git and other non-registry packages resolve only there).";
        return new Problem(
            ProblemIds.UnresolvedPackage,
            $"Package {shown} was not found. Searched: (1) embedded folders in Packages/ whose package.json has name '{name}'; "
            + $"(2) a file: path in the manifest or lock file; (3) Library/PackageCache/{name}@*; (4) {download}. {hint}",
            "Packages/manifest.json");
    }
}
