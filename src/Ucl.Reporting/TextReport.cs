using System.Text;
using Ucl.Core.Model;
using Ucl.Core.Results;

namespace Ucl.Reporting;

/// <summary>Human output in Unity's console format: <c>Assets/Path/File.cs(12,5): error CS0103: ...</c>.</summary>
public static class TextReport
{
    /// <summary>Renders a run. Compiler and <c>ucl</c> diagnostics come first, analyzer diagnostics in their own section.</summary>
    public static string Render(RunResult run)
    {
        var sb = new StringBuilder();
        foreach (var p in run.Problems)
        {
            sb.Append(FormatProblem(p)).Append('\n');
        }

        foreach (var cell in run.Cells)
        {
            sb.Append("== ").Append(cell.Cell.Label).Append('\n');
            foreach (var p in cell.Problems)
            {
                sb.Append(FormatProblem(p)).Append('\n');
            }

            foreach (var d in cell.Diagnostics.Where(d => d.Origin != DiagnosticOrigin.Analyzer))
            {
                sb.Append(Format(d)).Append('\n');
            }

            var analyzer = cell.Diagnostics.Where(d => d.Origin == DiagnosticOrigin.Analyzer).ToList();
            if (analyzer.Count > 0)
            {
                sb.Append("-- analyzers\n");
                foreach (var d in analyzer)
                {
                    sb.Append(Format(d)).Append('\n');
                }
            }

            foreach (var a in cell.Assemblies.Where(a => a.Status == AssemblyStatus.Skipped))
            {
                sb.Append("skipped ").Append(a.Name).Append(": ").Append(a.SkipReason).Append('\n');
            }

            if (run.Timings)
            {
                foreach (var a in cell.Assemblies)
                {
                    sb.Append("time ").Append(a.Name).Append(": ").Append(a.ElapsedMs).Append(" ms").Append(a.Cached ? " (cached)" : string.Empty).Append('\n');
                }
            }

            var errors = cell.Diagnostics.Count(d => d.Severity == Severity.Error);
            var warnings = cell.Diagnostics.Count(d => d.Severity == Severity.Warning);
            sb.Append($"result: {Plural(errors, "error")}, {Plural(warnings, "warning")}, {Plural(cell.Assemblies.Count, "assembly", "assemblies")} ({cell.Assemblies.Count(a => a.Status == AssemblyStatus.Skipped)} skipped), exit {cell.ExitCode}\n");
        }

        sb.Append("exit ").Append(run.ExitCode).Append('\n');
        return sb.ToString();
    }

    /// <summary>One diagnostic line.</summary>
    public static string Format(Diagnostic d)
    {
        var location = d.File is null ? string.Empty : d.Line > 0 ? $"{d.File}({d.Line},{d.Column}): " : $"{d.File}: ";
        return $"{location}{Names.Of(d.Severity)} {d.Id}: {d.Message}";
    }

    /// <summary>One problem line.</summary>
    public static string FormatProblem(Problem p) => $"{(p.File is null ? string.Empty : p.File + ": ")}error {p.Id}: {p.Message}";

    private static string Plural(int n, string one, string? many = null) => n == 1 ? $"1 {one}" : $"{n} {many ?? one + "s"}";
}
