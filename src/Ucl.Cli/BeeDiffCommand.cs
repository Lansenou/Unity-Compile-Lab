using Ucl.Compilation;
using Ucl.Core.Bee;
using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Discovery;
using Ucl.Reporting;

namespace Ucl.Cli;

/// <summary>
/// <c>ucl bee-diff</c>: compares every compiler command line the Editor wrote (<c>Library/Bee/artifacts/&lt;dag&gt;/*.rsp</c>)
/// with the one <c>ucl</c> computes for the same assembly and cell (docs/oracle.md, "Bee oracle"). Read-only; compiles nothing.
/// </summary>
internal static class BeeDiffCommand
{
    public static int Run(CliOptions options, TextWriter stdout, TextWriter stderr, IEnvironment env)
    {
        var root = Path.GetFullPath(options.Project ?? options.Positionals.FirstOrDefault() ?? ".");
        var fs = new PhysicalFileSystem([]);
        var result = Compare(root, fs, env, options);
        var text = options.Format switch
        {
            "json" => BeeDiffReport.Json(result),
            "sarif" => BeeDiffReport.Sarif(result),
            _ => BeeDiffReport.Text(result),
        };
        var problem = OutputSink.Write(options.Output, text, root, stdout);
        if (problem is not null)
        {
            stderr.WriteLine(TextReport.FormatProblem(problem));
            return Core.Rules.ExitCodes.Configuration;
        }

        return result.ExitCode;
    }

    private static BeeDiffResult Compare(string root, IFileSystem fs, IEnvironment env, CliOptions options)
    {
        var artifacts = Path.Combine(root, "Library", "Bee", "artifacts");
        var dags = fs.ListDirectories(artifacts)
            .Where(d => d.EndsWith(".dag", StringComparison.OrdinalIgnoreCase))
            .Select(d => (Name: d, Files: fs.ListFiles(Path.Combine(artifacts, d))
                .Where(f => f.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".mvfrm.rsp", StringComparison.OrdinalIgnoreCase))
                .ToList()))
            .Where(d => d.Files.Count > 0)
            .ToList();
        if (dags.Count == 0)
        {
            return Failed(new Problem(ProblemIds.NoBeeArtifacts,
                "no Library/Bee/artifacts/*.dag/*.rsp: open the project in the Unity Editor once (and build the player for player dags), then run ucl bee-diff again"));
        }

        var project = new ProjectLoader(fs, env).Load(root);
        if (project.Inventory is not { } inventory)
        {
            return Failed([.. project.Problems]);
        }

        var version = options.UnityVersions.FirstOrDefault() ?? inventory.ProjectVersion;
        var located = new EditorLocator(fs, env).Locate(version, options.EditorPath);
        if (!located.Ok)
        {
            return Failed(new Problem(ProblemIds.EditorNotFound, located.Error!));
        }

        var editor = located.Value!;
        var normalizer = new BeeNormalizer(root, project, editor, new AssemblyIdentityReader(fs));
        var ucl = new UclCommandLine(fs, editor, project);
        var results = new List<BeeDagResult>();
        foreach (var (dag, files) in dags)
        {
            var rsps = files
                .Select(f => (File: $"Library/Bee/artifacts/{dag}/{f}", Rsp: BeeResponseFile.Parse(fs.ReadAllText(Path.Combine(artifacts, dag, f))), Name: f))
                .Select(r => (r.File, r.Rsp, Name: r.Rsp.AssemblyName(r.Name)))
                .Where(r => r.Rsp.Sources.Count > 0)
                .OrderBy(r => r.Name, StringComparer.Ordinal)
                .ToList();
            if (rsps.Count == 0)
            {
                results.Add(new BeeDagResult(dag, null, "no response file with source files", []));
                continue;
            }

            var cell = BeeDag.CellOf(rsps[0].Rsp.Defines.ToHashSet(StringComparer.Ordinal), editor.Version);
            if (!cell.Ok)
            {
                results.Add(new BeeDagResult(dag, null, cell.Error, []));
                continue;
            }

            var graph = AssemblyGraphBuilder.Build(inventory with { ProjectVersion = cell.Value!.UnityVersion }, cell.Value);
            var assemblies = new List<BeeAssemblyResult>();
            foreach (var (file, rsp, name) in rsps)
            {
                var plan = graph.Find(name);
                if (plan is null)
                {
                    var why = graph.Excluded.TryGetValue(name, out var reason) ? $" ({reason})" : string.Empty;
                    assemblies.Add(new BeeAssemblyResult(name, null, file, [new BeeDifference(BeeCategory.Assembly, BeeChange.Missing, name + why)]));
                    continue;
                }

                assemblies.Add(new BeeAssemblyResult(name, plan.DefinitionPath, file, BeeDiff.Compare(normalizer.FromBee(rsp), Mine(graph, plan, ucl, normalizer))));
            }

            foreach (var plan in graph.Assemblies.Where(p => rsps.All(r => r.Name != p.Name)))
            {
                assemblies.Add(new BeeAssemblyResult(plan.Name, plan.DefinitionPath, null, [new BeeDifference(BeeCategory.Assembly, BeeChange.Extra, plan.Name)]));
            }

            results.Add(new BeeDagResult(dag, cell.Value, null, [.. assemblies.OrderBy(a => a.Name, StringComparer.Ordinal)]));
        }

        return new BeeDiffResult { ToolVersion = App.Version, Dags = [.. results.OrderBy(d => d.Name, StringComparer.Ordinal)] };
    }

    private static CommandLine Mine(AssemblyGraph graph, AssemblyPlan plan, UclCommandLine ucl, BeeNormalizer normalizer) => new()
    {
        Sources = plan.Sources,
        References = [.. ucl.ReferencePaths(graph, plan).Select(normalizer.Reference), .. plan.References.Select(ReferenceEntry.ForAssembly)],
        Defines = [.. plan.Defines.Symbols],
        NoWarn = plan.NoWarn,
        Analyzers = [.. ucl.AnalyzerPaths(plan).Select(normalizer.Location)],
        AdditionalFiles = plan.AdditionalFiles,
        LangVersion = plan.LangVersion,
        Unsafe = plan.AllowUnsafe,
    };

    private static BeeDiffResult Failed(params Problem[] problems) => new() { ToolVersion = App.Version, Problems = problems };
}
