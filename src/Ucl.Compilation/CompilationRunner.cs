using Ucl.Core.Graph;
using Ucl.Core.Results;
using Ucl.Core.Rules;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>Compiles every assembly of a cell in dependency order, in parallel where the graph allows.</summary>
public sealed class CompilationRunner
{
    private readonly IFileSystem _fs;

    /// <summary>Creates a runner that reads through <paramref name="fs"/>.</summary>
    public CompilationRunner(IFileSystem fs)
    {
        _fs = fs;
    }

    /// <summary>Compiles a cell. Assemblies whose dependencies failed are skipped, as Unity does.</summary>
    /// <param name="graph">The cell's graph.</param>
    /// <param name="project">The project.</param>
    /// <param name="editor">The editor install.</param>
    /// <param name="settings">Run-wide switches.</param>
    /// <param name="only">When set (<c>--changed</c>), report only these assemblies; their dependencies are still compiled (or read from the cache) because they are inputs.</param>
    public CellResult Run(AssemblyGraph graph, ProjectContext project, EditorInstall editor, CompileSettings settings, IReadOnlySet<string>? only = null) =>
        RunWithImages(graph, project, editor, settings, only).Result;

    /// <summary>
    /// Compiles a cell like <see cref="Run"/> and also returns the emitted image of every assembly that compiled
    /// (the reported ones and their dependencies); full images when <see cref="CompileSettings.FullImages"/> is set.
    /// </summary>
    public (CellResult Result, IReadOnlyDictionary<string, byte[]> Images) RunWithImages(
        AssemblyGraph graph, ProjectContext project, EditorInstall editor, CompileSettings settings, IReadOnlySet<string>? only = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(settings);
        var needed = Needed(graph, only);
        var catalog = new ReferenceCatalog(_fs, editor);
        var hasher = new ContentHasher(_fs);
        var memo = settings.CacheDirectory is null ? null : new HashMemoStore(_fs, settings.CacheDirectory);
        memo?.LoadInto(hasher);
        var cache = settings.CacheDirectory is null ? null : new BuildCache(_fs, settings.CacheDirectory);
        var compiler = new AssemblyCompiler(_fs, project, catalog, hasher, settings, cache);
        var gate = new SemaphoreSlim(Math.Max(1, settings.MaxParallelism));
        var tasks = new Dictionary<string, Task<AssemblyOutcome>>(StringComparer.Ordinal);

        foreach (var plan in graph.Assemblies.Where(p => needed.Contains(p.Name)))
        {
            var deps = plan.References.ToDictionary(r => r, r => tasks[r], StringComparer.Ordinal);
            tasks[plan.Name] = Task.Run(async () =>
            {
                await Task.WhenAll(deps.Values).ConfigureAwait(false);
                var failed = deps.Where(d => d.Value.Result.Reference is null).Select(d => d.Key).Order(StringComparer.Ordinal).ToList();
                if (failed.Count > 0)
                {
                    // Name the root failures (assemblies that compiled with errors), not only the skipped ones in between.
                    var roots = failed
                        .SelectMany(f => deps[f].Result.Result is { Status: AssemblyStatus.Failed } ? [f] : deps[f].Result.Result.BlockedBy)
                        .Distinct()
                        .Order(StringComparer.Ordinal)
                        .ToList();
                    var through = failed.Except(roots).ToList();
                    var reason = $"{(roots.Count == 1 ? "dependency" : "dependencies")} {Quoted(roots)} failed"
                        + (through.Count > 0 ? $" (through {Quoted(through)}, skipped)" : string.Empty);
                    return Skipped(plan, reason, roots);
                }

                await gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    return compiler.Compile(graph, plan, deps.ToDictionary(d => d.Key, d => d.Value.Result, StringComparer.Ordinal));
                }
                finally
                {
                    gate.Release();
                }
            });
        }

        Task.WaitAll(tasks.Values.ToArray());
        memo?.Save(hasher);
        var reported = graph.Assemblies.Where(p => only is null || only.Contains(p.Name)).ToList();
        var assemblies = reported.Select(p => tasks[p.Name].Result is { PendingDiagnostics: { } pending } outcome
            ? outcome.Result with { Diagnostics = pending.GetAwaiter().GetResult() }
            : tasks[p.Name].Result.Result).ToList();
        var planning = graph.Diagnostics.Where(d => only is null || d.Assembly is null || only.Contains(d.Assembly));
        var diagnostics = DiagnosticOrder.Sort(planning.Concat(assemblies.SelectMany(a => a.Diagnostics)));
        var images = tasks
            .Where(t => t.Value.Result.Image is not null)
            .ToDictionary(t => t.Key, t => t.Value.Result.Image!, StringComparer.Ordinal);
        return (new CellResult
        {
            Cell = graph.Cell,
            Assemblies = assemblies,
            Excluded = graph.Excluded,
            Diagnostics = diagnostics,
            Problems = graph.Problems,
            ExitCode = ExitCodes.Compute(graph.Problems, diagnostics),
        }, images);
    }

    // The reported assemblies and everything they reference, transitively.
    private static HashSet<string> Needed(AssemblyGraph graph, IReadOnlySet<string>? only)
    {
        var needed = new HashSet<string>(only ?? graph.Assemblies.Select(a => a.Name), StringComparer.Ordinal);
        foreach (var plan in graph.Assemblies.Reverse())
        {
            if (needed.Contains(plan.Name))
            {
                needed.UnionWith(plan.References);
            }
        }

        return needed;
    }

    private static string Quoted(IEnumerable<string> names) => string.Join(", ", names.Select(n => $"'{n}'"));

    private static AssemblyOutcome Skipped(AssemblyPlan plan, string reason, IReadOnlyList<string> blockedBy) => new(
        new AssemblyResult
        {
            Name = plan.Name,
            Kind = plan.Kind,
            DefinitionPath = plan.DefinitionPath,
            Status = AssemblyStatus.Skipped,
            SkipReason = reason,
            BlockedBy = blockedBy,
            Defines = plan.Defines.Symbols.ToList(),
            SourceCount = plan.Sources.Count,
        },
        null,
        string.Empty);
}
