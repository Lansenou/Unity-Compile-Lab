using Ucl.Compilation;
using Ucl.Core.Model;
using Ucl.Core.Rules;
using Ucl.Discovery;
using Ucl.Reporting;

namespace Ucl.Cli;

/// <summary>
/// <c>ucl export-csproj</c>: write an SDK-style <c>.csproj</c> per assembly of the first matrix cell, plus a
/// <c>.slnx</c>, for IDEs. ucl never reads them back; checking always plans from the Unity project itself.
/// </summary>
internal static class ExportCsprojCommand
{
    public static int Run(CliOptions options, TextWriter stdout, TextWriter stderr, IEnvironment env)
    {
        if (options.OutDir is null)
        {
            stderr.WriteLine($"error {ProblemIds.BadArguments}: export-csproj needs --out <dir> (a folder outside Assets/, Packages/ and ProjectSettings/)");
            return ExitCodes.Configuration;
        }

        var root = Path.GetFullPath(options.Project ?? options.Positionals.FirstOrDefault() ?? ".");
        var outDir = Path.GetFullPath(options.OutDir);
        if (ProtectedFolders.Containing(root, outDir) is { } protectedDir)
        {
            stderr.WriteLine($"error {ProblemIds.BadArguments}: --out must not be inside {protectedDir}/ (ucl never writes there)");
            return ExitCodes.Configuration;
        }

        var session = Session.Open(options, env, needEditor: true);
        var problems = new List<Problem>(session.Problems);
        if (session.Cells.Count > 0 && session.Cells[0] is (var cell, { } editor))
        {
            var graph = session.Graph(cell);
            problems.AddRange(graph.Problems);
            var fs = new PhysicalFileSystem([outDir]);
            var editorReferences = new EditorReferenceList(session.Fs, editor);
            stdout.WriteLine($"cell {cell.Label}");
            foreach (var plan in graph.Assemblies)
            {
                var path = Path.Combine(outDir, $"{plan.Name}.csproj");
                fs.WriteAllBytes(path, CsprojWriter.Project(plan, graph, session.Project, editorReferences.For(graph, plan)));
                stdout.WriteLine($"wrote {path}");
            }

            var solution = Path.Combine(outDir, $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(root))}.slnx");
            fs.WriteAllBytes(solution, CsprojWriter.Solution(graph.Assemblies.Select(a => a.Name)));
            stdout.WriteLine($"wrote {solution}");
        }

        foreach (var p in problems.Distinct())
        {
            stderr.WriteLine(TextReport.FormatProblem(p));
        }

        return problems.Count > 0 ? ExitCodes.Configuration : ExitCodes.Clean;
    }
}
