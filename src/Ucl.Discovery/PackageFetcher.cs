using System.Security.Cryptography;
using System.Text.Json;
using Ucl.Core.Model;
using Ucl.Core.Parsing;

namespace Ucl.Discovery;

/// <summary>
/// Fills the download cache (<see cref="DownloadCache"/>) with the registry packages a project needs and cannot find
/// locally, so the project resolves offline afterwards. Speaks the npm registry protocol that Unity registries use.
/// </summary>
/// <param name="fs">Disk access; the download cache root must be one of its writable roots.</param>
/// <param name="http">Network access.</param>
/// <param name="env">Home folder and <c>UCL_PACKAGE_CACHE</c>.</param>
public sealed class PackageFetcher(IFileSystem fs, IHttpClient http, IEnvironment env)
{
    /// <summary>Unity's registry, used for packages that no scoped registry serves and the lock file does not place.</summary>
    public const string DefaultRegistry = "https://packages.unity.com";

    private const string BuiltInPrefix = "com.unity.modules.";
    private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>Downloads every unresolved registry package of the project at <paramref name="projectPath"/>.</summary>
    /// <param name="projectPath">Project root.</param>
    /// <returns>One outcome per package, and the problems left after fetching.</returns>
    public async Task<PackageFetchReport> FetchAsync(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectPath));
        var problems = new List<Problem>();
        var manifest = ReadManifest(root, problems, out var hasLock);
        if (manifest is null)
        {
            return new PackageFetchReport([], problems);
        }

        var before = new PackageResolver(fs, env, root).Resolve(manifest, hasLock);
        var outcomes = before.Packages
            .Where(p => !p.IsBuiltInModule)
            .Select(p => new PackageFetchOutcome(p.Name, p.Version, PackageFetchStatus.Cached, p.Source))
            .ToList();

        var queue = new Queue<(string Name, string Version, string LockUrl)>();
        var seen = before.Packages.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var declared = manifest.Packages.ToDictionary(d => d.Name, StringComparer.Ordinal);
        void Enqueue(string name, string version, string lockUrl)
        {
            if (!name.StartsWith(BuiltInPrefix, StringComparison.Ordinal) && seen.Add(name))
            {
                queue.Enqueue((name, version, lockUrl));
            }
        }

        // Manifest (and lock) entries first, so their versions win over versions that dependencies ask for, as in resolution.
        foreach (var d in manifest.Packages)
        {
            Enqueue(d.Name, d.Version.Length > 0 ? d.Version : d.Requested, d.Url);
        }

        if (!hasLock)
        {
            // Resolution followed these dependencies too; the ones it could not find are what is left to fetch.
            foreach (var folder in before.Roots.Values)
            {
                EnqueueDependencies(PackageJson.ReadValid(fs, folder), declared, Enqueue);
            }
        }

        while (queue.Count > 0)
        {
            var (name, version, lockUrl) = queue.Dequeue();
            var (outcome, json) = await FetchOneAsync(name, version, RegistryFor(name, lockUrl, manifest.ScopedRegistries)).ConfigureAwait(false);
            outcomes.Add(outcome);
            if (!hasLock)
            {
                EnqueueDependencies(json, declared, Enqueue);
            }
        }

        var after = new PackageResolver(fs, env, root).Resolve(manifest, hasLock);
        problems.AddRange(after.Problems);
        return new PackageFetchReport(outcomes.OrderBy(o => o.Name, StringComparer.Ordinal).ToList(), problems);
    }

    /// <summary>The registry for a package: the scoped registry with the longest matching scope, else the lock-file URL, else Unity's.</summary>
    public static string RegistryFor(string name, string lockUrl, IReadOnlyList<ScopedRegistry> scopedRegistries)
    {
        ArgumentNullException.ThrowIfNull(scopedRegistries);
        var scoped = scopedRegistries
            .Where(r => r.Url.Length > 0 && r.Serves(name))
            .OrderByDescending(r => r.Scopes.Where(s => name == s || name.StartsWith(s + ".", StringComparison.Ordinal)).Max(s => s.Length))
            .FirstOrDefault();
        var url = scoped?.Url ?? (lockUrl is { Length: > 0 } ? lockUrl : DefaultRegistry);
        return url.TrimEnd('/');
    }

    private static void EnqueueDependencies(PackageJson? json, Dictionary<string, PackageDeclaration> declared, Action<string, string, string> enqueue)
    {
        foreach (var (dep, depVersion) in json?.Dependencies ?? [])
        {
            enqueue(dep, declared.TryGetValue(dep, out var d) && d.Version.Length > 0 ? d.Version : depVersion, string.Empty);
        }
    }

    private async Task<(PackageFetchOutcome Outcome, PackageJson? Json)> FetchOneAsync(string name, string version, string registry)
    {
        PackageFetchOutcome Failed(string reason) => new(name, version, PackageFetchStatus.Failed, reason);
        if (!SemanticVersion.Parse(version).Ok)
        {
            return (Failed(version.Length == 0
                ? "no version is requested for it"
                : "not a registry version (git and file: packages are not fetchable; open the project in Unity once so it fills Library/PackageCache)"), null);
        }

        // The name becomes a folder name; anything that could step out of the cache root is refused before any I/O.
        if (name.Length == 0 || name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal) || name.Contains("..", StringComparison.Ordinal))
        {
            return (Failed("invalid package name"), null);
        }

        var folder = DownloadCache.Folder(env, name, version);
        if (PackageJson.ReadValid(fs, folder) is { } existing && (existing.Name.Length == 0 || existing.Name == name))
        {
            return (new PackageFetchOutcome(name, version, PackageFetchStatus.Cached, "download"), existing);
        }

        try
        {
            var docUri = new Uri($"{registry}/{Uri.EscapeDataString(name)}");
            var tarball = FindTarball(await http.GetBytesAsync(docUri).ConfigureAwait(false), version);
            if (!tarball.Ok)
            {
                return (Failed($"{docUri}: {tarball.Error}"), null);
            }

            var (tarballUrl, shasum) = tarball.Value;
            var tarballUri = new Uri(docUri, tarballUrl);
            var bytes = await http.GetBytesAsync(tarballUri).ConfigureAwait(false);

            // SHA-1 is what the npm protocol publishes; it guards against truncated or swapped downloads, not attackers.
            if (shasum is { Length: > 0 } && !Convert.ToHexString(SHA1.HashData(bytes)).Equals(shasum, StringComparison.OrdinalIgnoreCase))
            {
                return (Failed($"{tarballUri}: SHA-1 mismatch (expected {shasum})"), null);
            }

            var files = TarballExtractor.Extract(bytes);
            if (!files.Ok)
            {
                return (Failed($"{tarballUri}: {files.Error}"), null);
            }

            var written = Install(folder, files.Value!);
            if (!written.Ok)
            {
                return (Failed($"{tarballUri}: {written.Error}"), null);
            }

            if (written.Value!.Name.Length > 0 && written.Value.Name != name)
            {
                return (Failed($"{tarballUri}: the tarball holds package '{written.Value.Name}'"), null);
            }

            return (new PackageFetchOutcome(name, version, PackageFetchStatus.Fetched, registry), written.Value);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UriFormatException)
        {
            return (Failed(e is TaskCanceledException ? "timed out" : e.Message), null);
        }
    }

    private static Result<(string Url, string? Shasum)> FindTarball(byte[] document, string version)
    {
        try
        {
            using var doc = JsonDocument.Parse(document, JsonOptions);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("versions", out var versions) || versions.ValueKind != JsonValueKind.Object
                || !versions.TryGetProperty(version, out var v) || v.ValueKind != JsonValueKind.Object)
            {
                return Result<(string, string?)>.Failure($"the registry has no version {version}");
            }

            if (!v.TryGetProperty("dist", out var dist) || dist.ValueKind != JsonValueKind.Object
                || !dist.TryGetProperty("tarball", out var t) || t.ValueKind != JsonValueKind.String || t.GetString() is not { Length: > 0 } url)
            {
                return Result<(string, string?)>.Failure($"version {version} has no dist.tarball");
            }

            var shasum = dist.TryGetProperty("shasum", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
            return Result<(string, string?)>.Success((url, shasum));
        }
        catch (JsonException e)
        {
            return Result<(string, string?)>.Failure($"invalid registry JSON: {e.Message}");
        }
    }

    private Result<PackageJson> Install(string folder, IReadOnlyList<(string Path, byte[] Bytes)> files)
    {
        if (!files.Any(f => f.Path == "package.json"))
        {
            return Result<PackageJson>.Failure("the tarball has no package/package.json");
        }

        var full = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        var targets = files.Select(f => (Target: Path.GetFullPath(Path.Combine(folder, f.Path.Replace('/', Path.DirectorySeparatorChar))), f.Path, f.Bytes)).ToList();
        if (targets.FirstOrDefault(t => !t.Target.StartsWith(full, StringComparison.Ordinal)) is { Target: not null } escaping)
        {
            return Result<PackageJson>.Failure($"tarball entry '{escaping.Path}' escapes the package folder");
        }

        // IFileSystem has no rename, so the folder cannot be swapped in atomically. Instead package.json, which is
        // what resolution looks for, is written last: an interrupted fetch leaves a folder that does not resolve and
        // is cleaned and rewritten by the next fetch.
        DeleteTree(folder);
        foreach (var t in targets.Where(t => t.Path != "package.json"))
        {
            fs.WriteAllBytes(t.Target, t.Bytes);
        }

        fs.WriteAllBytes(targets.First(t => t.Path == "package.json").Target, targets.First(t => t.Path == "package.json").Bytes);
        return PackageJson.Read(fs, folder) ?? Result<PackageJson>.Failure("package.json was not written");
    }

    private void DeleteTree(string folder)
    {
        foreach (var file in fs.ListFiles(folder))
        {
            fs.DeleteFile(Path.Combine(folder, file));
        }

        foreach (var dir in fs.ListDirectories(folder))
        {
            DeleteTree(Path.Combine(folder, dir));
        }
    }

    private PackageManifest? ReadManifest(string root, List<Problem> problems, out bool hasLock)
    {
        hasLock = false;
        var manifestPath = Path.Combine(root, "Packages", "manifest.json");
        var lockPath = Path.Combine(root, "Packages", "packages-lock.json");
        if (!fs.DirectoryExists(Path.Combine(root, "Packages")))
        {
            problems.Add(new Problem(ProblemIds.NotAProject, $"'{root}' is not a Unity project: missing Packages/. Pass the folder that holds Assets/, Packages/ and ProjectSettings/."));
            return null;
        }

        // Like loading, a missing manifest is an empty one: only embedded packages are left to resolve.
        var manifestJson = fs.FileExists(manifestPath) ? fs.ReadAllText(manifestPath) : "{}";
        var alone = PackageManifestParser.Parse(manifestJson, null);
        if (!alone.Ok)
        {
            problems.Add(new Problem(ProblemIds.BadProjectFile, $"Packages/manifest.json: {alone.Error}", "Packages/manifest.json"));
            return null;
        }

        if (!fs.FileExists(lockPath))
        {
            return alone.Value;
        }

        // Same fallback as loading: a broken lock file means following package.json dependencies instead.
        var withLock = PackageManifestParser.Parse(manifestJson, fs.ReadAllText(lockPath));
        if (!withLock.Ok)
        {
            problems.Add(new Problem(ProblemIds.BadProjectFile, $"Packages/packages-lock.json: {withLock.Error}", "Packages/packages-lock.json"));
            return alone.Value;
        }

        hasLock = true;
        return withLock.Value;
    }
}
