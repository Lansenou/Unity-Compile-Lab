using System.Globalization;
using System.Text;
using Ucl.Core.Model;
using Ucl.Core.Results;

namespace Ucl.Reporting;

/// <summary>Human output in Unity's console format: <c>Assets/Path/File.cs(12,5): error CS0103: ...</c>.</summary>
public static class TextReport
{
    /// <summary>
    /// Renders a run. Each cell leads with its root failures (assemblies that failed to compile, ranked by how many
    /// assemblies they block), then compiler and <c>ucl</c> diagnostics, analyzer diagnostics in their own section,
    /// and the skipped assemblies with the root failure that blocked each. <paramref name="summary"/> keeps only
    /// the root failures and the result line.
    /// </summary>
    public static string Render(RunResult run, bool summary = false)
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

            RootFailures(sb, cell);
            if (summary)
            {
                AppendResult(sb, cell);
                continue;
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
                var rows = cell.Assemblies.SelectMany(a => a.AnalyzerTimings.Select(t => (Assembly: a.Name, Timing: t)))
                    .OrderByDescending(r => r.Timing.TimeMs).ThenBy(r => r.Assembly, StringComparer.Ordinal)
                    .ThenBy(r => r.Timing.Analyzer, StringComparer.Ordinal).ThenBy(r => r.Timing.Path, StringComparer.Ordinal).ToArray();
                if (rows.Length > 0)
                {
                    sb.Append("analyzer callback times (cumulative; each rule list shares one total):\n")
                        .Append("assembly\tanalyzer\trule IDs (shared time)\ttimeMs\n");
                    foreach (var row in rows)
                    {
                        sb.Append(row.Assembly).Append('\t').Append(row.Timing.Analyzer).Append('\t')
                            .Append(string.Join(",", row.Timing.RuleIds)).Append('\t')
                            .Append(row.Timing.TimeMs.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
                    }
                }
            }

            AppendResult(sb, cell);
        }

        sb.Append("exit ").Append(run.ExitCode).Append('\n');
        return sb.ToString();
    }

    private static void AppendResult(StringBuilder sb, CellResult cell)
    {
        var errors = cell.Diagnostics.Count(d => d.Severity == Severity.Error);
        var warnings = cell.Diagnostics.Count(d => d.Severity == Severity.Warning);
        sb.Append($"result: {Plural(errors, "error")}, {Plural(warnings, "warning")}, {Plural(cell.Assemblies.Count, "assembly", "assemblies")} ({cell.Assemblies.Count(a => a.Status == AssemblyStatus.Skipped)} skipped), exit {cell.ExitCode}\n");
    }

    // A failed assembly is always a root failure: an assembly whose dependency failed is skipped, never compiled.
    private static void RootFailures(StringBuilder sb, CellResult cell)
    {
        var failed = cell.Assemblies.Where(a => a.Status == AssemblyStatus.Failed).ToList();
        if (failed.Count == 0)
        {
            return;
        }

        var skipped = cell.Assemblies.Where(a => a.Status == AssemblyStatus.Skipped).ToList();
        var blocks = failed.ToDictionary(f => f.Name, f => skipped.Count(s => s.BlockedBy.Contains(f.Name)), StringComparer.Ordinal);
        sb.Append($"root failures: {Plural(failed.Count, "assembly", "assemblies")} failed to compile; {skipped.Count} skipped because of them\n");
        foreach (var f in failed.OrderByDescending(f => blocks[f.Name]).ThenBy(f => f.Name, StringComparer.Ordinal))
        {
            var errors = f.Diagnostics.Where(d => d.Severity == Severity.Error).ToList();
            sb.Append("  ").Append(f.Name).Append(": ").Append(Plural(errors.Count, "error"))
                .Append(", blocks ").Append(Plural(blocks[f.Name], "assembly", "assemblies")).Append('\n');
            foreach (var group in errors.GroupBy(d => d.Id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(3))
            {
                sb.Append("    ").Append(group.Count()).Append(" x ").Append(group.Key).Append(", first: ").Append(Format(group.First())).Append('\n');
            }
        }
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
