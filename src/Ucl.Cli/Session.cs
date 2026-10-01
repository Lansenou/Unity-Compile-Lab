using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Discovery;

namespace Ucl.Cli;

/// <summary>What check and graph share: the loaded project, the matrix cells and their editors, and the problems found preparing them.</summary>
internal sealed record Session(
    string ProjectRoot,
    string CacheDir,
    IFileSystem Fs,
    ProjectContext Project,
    IReadOnlyList<(CompileCell Cell, EditorInstall? Editor)> Cells,
    IReadOnlyList<Problem> Problems)
{
    /// <summary>Loads the project and expands the matrix. <paramref name="needEditor"/> is false for graph, which works without an editor.</summary>
    public static Session Open(CliOptions options, IEnvironment env, bool needEditor)
    {
        var root = Path.GetFullPath(options.Project ?? options.Positionals.FirstOrDefault() ?? ".");
        var cacheDir = Path.GetFullPath(options.CacheDir ?? Path.Combine(root, "Library", "ucl"));
        var problems = new List<Problem>();
        foreach (var protectedDir in new[] { "Assets", "Packages", "ProjectSettings" })
        {
            var rel = Path.GetRelativePath(Path.Combine(root, protectedDir), cacheDir);
            if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
            {
                problems.Add(new Problem(ProblemIds.BadArguments, $"--cache-dir must not be inside {protectedDir}/ (ucl never writes there)"));
            }
        }

        var fs = new PhysicalFileSystem([cacheDir]);
        var project = new ProjectLoader(fs, env).Load(root);
        problems.AddRange(project.Problems);
        var cells = new List<(CompileCell, EditorInstall?)>();
        if (project.Inventory is not { } inventory)
        {
            return new Session(root, cacheDir, fs, project, cells, problems);
        }

        var locator = new EditorLocator(fs, env);
        var versions = options.UnityVersions.Count > 0 ? options.UnityVersions : [inventory.ProjectVersion];
        var targets = options.Targets.Count > 0 ? options.Targets : [TargetKind.Editor];
        var platforms = options.Platforms.Count > 0 ? options.Platforms : [BuildPlatform.StandaloneWindows64];
        foreach (var requested in versions)
        {
            var version = requested;
            EditorInstall? editor = null;
            if (!version.IsUnity6 && options.EditorPath is null)
            {
                problems.Add(new Problem(ProblemIds.UnsupportedVersion,
                    $"Unity {version} is not supported: ucl compiles Unity 6 (6000.x) projects. Pass --unity-version 6000.x.y or --editor <path to a Unity 6 editor> to override.",
                    "ProjectSettings/ProjectVersion.txt"));
                continue;
            }

            if (needEditor || options.EditorPath is not null)
            {
                var located = locator.Locate(version, options.EditorPath);
                if (!located.Ok)
                {
                    problems.Add(new Problem(ProblemIds.EditorNotFound, located.Error!));
                    continue;
                }

                editor = located.Value!;
                version = editor.Version;
                if (!version.IsUnity6)
                {
                    problems.Add(new Problem(ProblemIds.UnsupportedVersion, $"editor {editor.Root} is Unity {version}; ucl supports Unity 6 (6000.x) only"));
                    continue;
                }
            }

            foreach (var target in targets)
            {
                foreach (var platform in platforms)
                {
                    cells.Add((new CompileCell(version, target, platform, options.Backend, options.Development, options.EditorOs ?? env.Os, options.IncludeTests), editor));
                }
            }
        }

        return new Session(root, cacheDir, fs, project, cells, problems);
    }

    /// <summary>The graph of one cell.</summary>
    public AssemblyGraph Graph(CompileCell cell) =>
        AssemblyGraphBuilder.Build(Project.Inventory! with { ProjectVersion = cell.UnityVersion }, cell);
}
