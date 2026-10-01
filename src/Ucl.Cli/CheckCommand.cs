using Ucl.Compilation;
using Ucl.Core.Results;
using Ucl.Core.Rules;
using Ucl.Discovery;
using Ucl.Reporting;

namespace Ucl.Cli;

/// <summary><c>ucl check</c>: compile every cell of the matrix and report.</summary>
internal static class CheckCommand
{
    public static int Run(CliOptions options, TextWriter stdout, TextWriter stderr, IEnvironment env)
    {
        var session = Session.Open(options, env, needEditor: true);
        var settings = new CompileSettings
        {
            Analyzers = options.Analyzers,
            WarnAsError = options.WarnAsError,
            MaxParallelism = options.Jobs ?? Environment.ProcessorCount,
            CacheDirectory = options.NoCache ? null : session.CacheDir,
            ToolVersion = App.Version,
        };

        var runner = new CompilationRunner(session.Fs);
        var cells = new List<CellResult>();
        foreach (var (cell, editor) in session.Cells)
        {
            cells.Add(runner.Run(session.Graph(cell), session.Project, editor!, settings));
        }

        var problemsExit = session.Problems.Count > 0 ? ExitCodes.Configuration : ExitCodes.Clean;
        var run = new RunResult
        {
            ToolVersion = App.Version,
            Cells = cells,
            Problems = session.Problems,
            Timings = options.Timings,
            ExitCode = App.Worst(cells.Select(c => c.ExitCode).Append(problemsExit)),
        };

        var text = options.Format switch
        {
            "json" => JsonReport.Render(run),
            "sarif" => SarifReport.Render(run),
            _ => TextReport.Render(run),
        };
        var problem = OutputSink.Write(options.Output, text, session.ProjectRoot, stdout);
        if (problem is not null)
        {
            stderr.WriteLine(TextReport.FormatProblem(problem));
            return ExitCodes.Configuration;
        }

        return run.ExitCode;
    }
}
