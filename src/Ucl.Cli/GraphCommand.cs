using Ucl.Core.Rules;
using Ucl.Discovery;
using Ucl.Reporting;

namespace Ucl.Cli;

/// <summary><c>ucl graph</c>: print the assembly graph of each cell. Needs no editor.</summary>
internal static class GraphCommand
{
    public static int Run(CliOptions options, TextWriter stdout, TextWriter stderr, IEnvironment env)
    {
        var session = Session.Open(options, env, needEditor: false);
        var graphs = session.Cells.Select(c => session.Graph(c.Cell)).ToList();
        var problems = session.Problems.Concat(graphs.SelectMany(g => g.Problems)).Distinct().ToList();
        var text = options.Format switch
        {
            "json" => GraphReport.Json(graphs, session.Problems, App.Version),
            "dot" => GraphReport.Dot(graphs),
            _ => GraphReport.Text(graphs, session.Problems),
        };
        var problem = OutputSink.Write(options.Output, text, session.ProjectRoot, stdout);
        if (problem is not null)
        {
            stderr.WriteLine(TextReport.FormatProblem(problem));
            return ExitCodes.Configuration;
        }

        if (problems.Count > 0)
        {
            return ExitCodes.Configuration;
        }

        return graphs.SelectMany(g => g.Diagnostics).Any(d => d.Severity == Core.Model.Severity.Error) ? ExitCodes.Errors : ExitCodes.Clean;
    }
}
