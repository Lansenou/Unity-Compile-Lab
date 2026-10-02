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
        using var gate = new SemaphoreSlim(Math.Max(1, settings.MaxParallelism));
        using var analyzerGate = new SemaphoreSlim(Math.Max(1, settings.MaxParallelism));
        var compiler = new AssemblyCompiler(_fs, project, catalog, hasher, settings, cache, analyzerGate);
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

        try
        {
            Task.WaitAll(tasks.Values.ToArray());
        }
        catch
        {
            // A source task may fail after earlier assemblies launched analysis/cache work. Join that work before the
            // caller tears down its file system, retaining the original source exception if a finalization also fails.
            try
            {
                Task.WaitAll(tasks.Values.Where(t => t.IsCompletedSuccessfully).Select(t => CompleteResult(t.Result)).ToArray());
            }
            catch
            {
                // The original source-task failure is rethrown below.
            }

            throw;
        }
        var finalizations = tasks.ToDictionary(t => t.Key, t => CompleteResult(t.Value.Result), StringComparer.Ordinal);
        Task.WaitAll(finalizations.Values.ToArray());
        // Compilation can speculate past an emitted image while its analysis runs. Apply the same final failure cascade
        // as the old serial pipeline, so analyzer errors still block the reported dependents and ucl test's returned images.
        var completed = new Dictionary<string, AssemblyResult>(StringComparer.Ordinal);
        foreach (var plan in graph.Assemblies.Where(p => needed.Contains(p.Name)))
        {
            var failed = plan.References.Where(r => completed[r].Status != AssemblyStatus.Compiled).Order(StringComparer.Ordinal).ToList();
            if (failed.Count == 0)
            {
                completed[plan.Name] = finalizations[plan.Name].Result;
                continue;
            }

            var roots = failed.SelectMany(f => completed[f].Status == AssemblyStatus.Failed ? [f] : completed[f].BlockedBy)
                .Distinct().Order(StringComparer.Ordinal).ToList();
            var through = failed.Except(roots).ToList();
            var reason = $"{(roots.Count == 1 ? "dependency" : "dependencies")} {Quoted(roots)} failed"
                + (through.Count > 0 ? $" (through {Quoted(through)}, skipped)" : string.Empty);
            var speculative = finalizations[plan.Name].Result;
            completed[plan.Name] = Skipped(plan, reason, roots).Result with
            {
                AnalyzerTimings = speculative.AnalyzerTimings,
                ElapsedMs = speculative.ElapsedMs,
                Cached = speculative.Cached,
            };
        }

        memo?.Save(hasher);
        var reported = graph.Assemblies.Where(p => only is null || only.Contains(p.Name)).ToList();
        var assemblies = reported.Select(p => completed[p.Name]).ToList();
        var planning = graph.Diagnostics.Where(d => only is null || d.Assembly is null || only.Contains(d.Assembly));
        var diagnostics = DiagnosticOrder.Sort(planning.Concat(assemblies.SelectMany(a => a.Diagnostics)));
        var images = tasks
            .Where(t => t.Value.Result.Image is not null && completed[t.Key].Status == AssemblyStatus.Compiled)
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

    private static async Task<AssemblyResult> CompleteResult(AssemblyOutcome outcome)
    {
        if (outcome.PendingResult is { } pending)
        {
            return await pending.ConfigureAwait(false);
        }

        return outcome.PendingDiagnostics is { } diagnostics
            ? outcome.Result with { Diagnostics = await diagnostics.ConfigureAwait(false) }
            : outcome.Result;
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
