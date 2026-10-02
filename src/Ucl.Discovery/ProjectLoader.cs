using Ucl.Core.Model;
using Ucl.Core.Parsing;

namespace Ucl.Discovery;

/// <summary>
/// Loads a Unity project from disk: checks its layout, reads its version and settings, resolves its packages and
/// scans <c>Assets/</c> and every package root into a <see cref="Core.Graph.ProjectInventory"/>.
/// </summary>
/// <param name="fs">Disk access.</param>
/// <param name="env">Environment (home folder and <c>UCL_PACKAGE_CACHE</c> for the download cache).</param>
public sealed class ProjectLoader(IFileSystem fs, IEnvironment env)
{
    private const string EmptyManifest = "{}";

    /// <summary>Loads the project at <paramref name="projectPath"/>. Never throws for configuration problems; they are in <see cref="ProjectContext.Problems"/>.</summary>
    /// <param name="projectPath">Project root (absolute or relative to the current directory).</param>
    /// <returns>The context; its inventory is null when the folder is not a project or its version cannot be read.</returns>
    public ProjectContext Load(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectPath));
        var problems = new List<Problem>();

        var missing = new[] { "Assets", "Packages", "ProjectSettings" }.Where(d => !fs.DirectoryExists(Path.Combine(root, d))).ToList();
        if (missing.Count > 0)
        {
            problems.Add(new Problem(
                ProblemIds.NotAProject,
                $"'{root}' is not a Unity project: missing {string.Join(", ", missing.Select(m => m + "/"))}. Pass the folder that holds Assets/, Packages/ and ProjectSettings/."));
            return new ProjectContext { Root = root, Problems = problems };
        }

        var version = ReadVersion(root, problems);
        if (version is null)
        {
            return new ProjectContext { Root = root, Problems = problems };
        }

        var settings = ReadSettings(root);
        var manifest = ReadManifest(root, problems, out var hasLock);
        var resolution = new PackageResolver(fs, env, root).Resolve(manifest, hasLock);
        problems.AddRange(resolution.Problems);

        var scanner = new ProjectScanner(fs, root);
        scanner.ScanRootConfigs(root);
        scanner.ScanTree(Path.Combine(root, "Assets"), "Assets");
        foreach (var (logical, physical) in resolution.Roots)
        {
            scanner.ScanTree(physical, logical);
        }

        return new ProjectContext
        {
            Root = root,
            Inventory = scanner.ToInventory(version, settings, resolution.Packages, manifest.Testables),
            PackageRoots = resolution.Roots,
            Problems = problems,
        };
    }

    private UnityVersion? ReadVersion(string root, List<Problem> problems)
    {
        const string logical = "ProjectSettings/ProjectVersion.txt";
        var path = Path.Combine(root, "ProjectSettings", "ProjectVersion.txt");
        if (!fs.FileExists(path))
        {
            problems.Add(new Problem(ProblemIds.UnsupportedVersion, $"{logical} is missing, so the editor version is unknown.", logical));
            return null;
        }

        // Whether a parsed version is supported is the CLI's call: --editor may override it.
        var parsed = ProjectSettingsParser.ParseProjectVersion(fs.ReadAllText(path));
        if (!parsed.Ok)
        {
            problems.Add(new Problem(ProblemIds.UnsupportedVersion, $"{logical}: {parsed.Error}", logical));
            return null;
        }

        return parsed.Value;
    }

    private ProjectSettingsData ReadSettings(string root)
    {
        var path = Path.Combine(root, "ProjectSettings", "ProjectSettings.asset");
        return fs.FileExists(path) ? ProjectSettingsParser.Parse(fs.ReadAllText(path)) : ProjectSettingsData.Default;
    }

    private PackageManifest ReadManifest(string root, List<Problem> problems, out bool hasLock)
    {
        var manifestPath = Path.Combine(root, "Packages", "manifest.json");
        var lockPath = Path.Combine(root, "Packages", "packages-lock.json");
        var manifestJson = fs.FileExists(manifestPath) ? fs.ReadAllText(manifestPath) : EmptyManifest;

        // Parse the manifest alone first so a failure names the right file.
        var alone = PackageManifestParser.Parse(manifestJson, null);
        if (!alone.Ok)
        {
            problems.Add(new Problem(ProblemIds.BadProjectFile, $"Packages/manifest.json: {alone.Error}", "Packages/manifest.json"));
            hasLock = false;
            return PackageManifestParser.Parse(EmptyManifest, null).Value!;
        }

        if (!fs.FileExists(lockPath))
        {
            hasLock = false;
            return alone.Value!;
        }

        var withLock = PackageManifestParser.Parse(manifestJson, fs.ReadAllText(lockPath));
        if (!withLock.Ok)
        {
            // A broken lock file is reported, and resolution falls back to following package.json dependencies.
            problems.Add(new Problem(ProblemIds.BadProjectFile, $"Packages/packages-lock.json: {withLock.Error}", "Packages/packages-lock.json"));
            hasLock = false;
            return alone.Value!;
        }

        hasLock = true;
        return withLock.Value!;
    }
}
