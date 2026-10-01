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
    public CellResult Run(AssemblyGraph graph, ProjectContext project, EditorInstall editor, CompileSettings settings)
    {
        var catalog = new ReferenceCatalog(_fs, editor);
        var hasher = new ContentHasher(_fs);
        var memo = settings.CacheDirectory is null ? null : new HashMemoStore(_fs, settings.CacheDirectory);
        memo?.LoadInto(hasher);
        var cache = settings.CacheDirectory is null ? null : new BuildCache(_fs, settings.CacheDirectory);
        var compiler = new AssemblyCompiler(_fs, project, catalog, hasher, settings, cache);
        var gate = new SemaphoreSlim(Math.Max(1, settings.MaxParallelism));
        var tasks = new Dictionary<string, Task<AssemblyOutcome>>(StringComparer.Ordinal);

        foreach (var plan in graph.Assemblies)
        {
            var deps = plan.References.ToDictionary(r => r, r => tasks[r], StringComparer.Ordinal);
            tasks[plan.Name] = Task.Run(async () =>
            {
                await Task.WhenAll(deps.Values).ConfigureAwait(false);
                var failed = deps.Where(d => d.Value.Result.Reference is null).Select(d => d.Key).Order(StringComparer.Ordinal).ToList();
                if (failed.Count > 0)
                {
                    return Skipped(plan, $"dependency {string.Join(", ", failed.Select(f => $"'{f}'"))} has errors");
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
        var assemblies = graph.Assemblies.Select(p => tasks[p.Name].Result.Result).ToList();
        var diagnostics = DiagnosticOrder.Sort(graph.Diagnostics.Concat(assemblies.SelectMany(a => a.Diagnostics)));
        return new CellResult
        {
            Cell = graph.Cell,
            Assemblies = assemblies,
            Excluded = graph.Excluded,
            Diagnostics = diagnostics,
            Problems = graph.Problems,
            ExitCode = ExitCodes.Compute(graph.Problems, diagnostics),
        };
    }

    private static AssemblyOutcome Skipped(AssemblyPlan plan, string reason) => new(
        new AssemblyResult
        {
            Name = plan.Name,
            Kind = plan.Kind,
            DefinitionPath = plan.DefinitionPath,
            Status = AssemblyStatus.Skipped,
            SkipReason = reason,
            Defines = plan.Defines.Symbols.ToList(),
            SourceCount = plan.Sources.Count,
        },
        null,
        string.Empty);
}
