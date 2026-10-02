using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Parsing;
using Ucl.Core.Rules;

namespace Ucl.Discovery;

/// <summary>Walks <c>Assets/</c> and package roots with the Asset Database's hidden-entry rules and collects the files Core needs.</summary>
internal sealed class ProjectScanner
{
    private readonly IFileSystem fs;
    private readonly HashSet<string> excluded;
    private readonly List<string> scripts = [];
    private readonly List<TextFile> asmdefs = [];
    private readonly List<TextFile> asmrefs = [];
    private readonly List<TextFile> plugins = [];
    private readonly Dictionary<string, string> pluginFiles = new(StringComparer.Ordinal);
    private readonly List<string> nativePlugins = [];
    private readonly List<TextFile> responseFiles = [];
    private readonly List<string> ruleSets = [];
    private readonly List<string> analyzerConfigs = [];

    /// <summary>Creates a scanner for the project at <paramref name="projectRoot"/>.</summary>
    public ProjectScanner(IFileSystem fs, string projectRoot)
    {
        this.fs = fs;

        // Build output and caches live here; a local package rooted at the project must not drag them in.
        excluded = new HashSet<string>(
            new[] { "Library", "Temp", "obj" }.Select(n => Path.Combine(projectRoot, n)),
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    /// <summary>Collects <c>.editorconfig</c> and <c>*.globalconfig</c> directly in the project root.</summary>
    public void ScanRootConfigs(string projectRoot)
    {
        foreach (var name in fs.ListFiles(projectRoot))
        {
            if (IsAnalyzerConfig(name))
            {
                analyzerConfigs.Add(name);
            }
        }
    }

    /// <summary>Walks <paramref name="physical"/> recursively, reporting files under <paramref name="logical"/>.</summary>
    public void ScanTree(string physical, string logical)
    {
        foreach (var name in fs.ListFiles(physical))
        {
            // Analyzer configs start with '.', which the Asset Database hides, but Roslyn still reads them.
            if (IsAnalyzerConfig(name))
            {
                analyzerConfigs.Add($"{logical}/{name}");
                continue;
            }

            if (!ProjectPaths.IsHiddenName(name))
            {
                AddFile(Path.Combine(physical, name), $"{logical}/{name}", name);
            }
        }

        foreach (var name in fs.ListDirectories(physical))
        {
            var child = Path.Combine(physical, name);
            if (!ProjectPaths.IsHiddenName(name) && !excluded.Contains(child))
            {
                ScanTree(child, $"{logical}/{name}");
            }
        }
    }

    /// <summary>Builds the inventory from everything scanned so far, every list sorted ordinal.</summary>
    public ProjectInventory ToInventory(UnityVersion version, ProjectSettingsData settings, IReadOnlyList<ResolvedPackage> packages, IReadOnlyList<string> testables) => new()
    {
        PluginVersions = DuplicateNameVersions(),
        Testables = testables,
        ProjectVersion = version,
        Scripts = Sorted(scripts),
        Asmdefs = Sorted(asmdefs),
        Asmrefs = Sorted(asmrefs),
        Plugins = Sorted(plugins),
        NativePlugins = Sorted(nativePlugins),
        ResponseFiles = Sorted(responseFiles),
        RuleSets = Sorted(ruleSets),
        AnalyzerConfigs = Sorted(analyzerConfigs),
        Settings = settings,
        Packages = packages,
    };

    // Unity keeps one precompiled DLL per file name (the highest version), so only DLLs that share a name need a version.
    private SortedDictionary<string, string> DuplicateNameVersions()
    {
        var reader = new AssemblyIdentityReader(fs);
        var versions = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in pluginFiles.GroupBy(p => ProjectPaths.FileName(p.Key), StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            foreach (var (logical, physical) in group)
            {
                versions[logical] = reader.Version(physical);
            }
        }

        return versions;
    }

    private static bool IsAnalyzerConfig(string name) =>
        name.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".globalconfig", StringComparison.OrdinalIgnoreCase);

    private void AddFile(string physical, string logical, string name)
    {
        if (name.Equals("csc.rsp", StringComparison.OrdinalIgnoreCase))
        {
            responseFiles.Add(new TextFile(logical, fs.ReadAllText(physical)));
            return;
        }

        switch (Path.GetExtension(name).ToLowerInvariant())
        {
            case ".cs":
                scripts.Add(logical);
                break;
            case ".asmdef":
                asmdefs.Add(new TextFile(logical, fs.ReadAllText(physical), Meta(physical)));
                break;
            case ".asmref":
                asmrefs.Add(new TextFile(logical, fs.ReadAllText(physical), Meta(physical)));
                break;
            case ".dll":
                // Unity never passes an unmanaged DLL to the compiler; only a PE image with a CLI header is a reference.
                if (PluginBinary.IsManaged(fs.ReadPrefix(physical, PluginBinary.HeaderBytes)))
                {
                    plugins.Add(new TextFile(logical, string.Empty, Meta(physical)));
                    pluginFiles[logical] = physical;
                }
                else
                {
                    nativePlugins.Add(logical);
                }

                break;
            case ".ruleset":
                ruleSets.Add(logical);
                break;
        }
    }

    private string? Meta(string physical)
    {
        var meta = physical + ".meta";
        return fs.FileExists(meta) ? fs.ReadAllText(meta) : null;
    }

    private static List<string> Sorted(List<string> list) => list.Order(StringComparer.Ordinal).ToList();

    private static List<TextFile> Sorted(List<TextFile> list) => list.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
}
