using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ucl.Core.Model;
using Ucl.Core.Results;

namespace Ucl.Reporting;

/// <summary>JSON output, schema <c>ucl-result/1</c> (<c>schema/result.schema.json</c>). Field order is fixed; no timestamps.</summary>
public static class JsonReport
{
    /// <summary>The schema identifier.</summary>
    public const string Schema = "ucl-result/1";

    /// <summary>Renders a run as indented JSON with a trailing newline.</summary>
    public static string Render(RunResult run)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("schema", Schema);
            w.WriteStartObject("tool");
            w.WriteString("name", "ucl");
            w.WriteString("version", run.ToolVersion);
            w.WriteEndObject();
            w.WriteNumber("exitCode", run.ExitCode);
            WriteSummary(w, run.Cells.SelectMany(c => c.Diagnostics).ToList(), run.Cells.Count,
                run.Timings ? run.Cells.SelectMany(c => c.Assemblies).SelectMany(a => a.AnalyzerTimings) : null);
            WriteProblems(w, run.Problems);
            w.WriteStartArray("cells");
            foreach (var cell in run.Cells)
            {
                WriteCell(w, cell, run.Timings);
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static void WriteSummary(Utf8JsonWriter w, IReadOnlyList<Diagnostic> diagnostics, int cells, IEnumerable<AnalyzerTiming>? timings)
    {
        w.WriteStartObject("summary");
        w.WriteNumber("cells", cells);
        w.WriteNumber("errors", diagnostics.Count(d => d.Severity == Severity.Error));
        w.WriteNumber("warnings", diagnostics.Count(d => d.Severity == Severity.Warning));
        w.WriteNumber("analyzerDiagnostics", diagnostics.Count(d => d.Origin == DiagnosticOrigin.Analyzer));
        if (timings is not null)
        {
            WriteAnalyzerTimings(w, timings.GroupBy(t => (t.Path, t.Analyzer))
                .Select(g => new AnalyzerTiming(g.Key.Path, g.Key.Analyzer, g.Sum(t => t.TimeMs))));
        }
        w.WriteEndObject();
    }

    private static void WriteProblems(Utf8JsonWriter w, IReadOnlyList<Problem> problems)
    {
        w.WriteStartArray("problems");
        foreach (var p in problems)
        {
            w.WriteStartObject();
            w.WriteString("id", p.Id);
            w.WriteString("message", p.Message);
            if (p.File is not null) w.WriteString("file", p.File);
            w.WriteEndObject();
        }

        w.WriteEndArray();
    }

    private static void WriteCell(Utf8JsonWriter w, CellResult cell, bool timings)
    {
        var c = cell.Cell;
        w.WriteStartObject();
        w.WriteString("unityVersion", c.UnityVersion.ToString());
        w.WriteString("target", Names.Of(c.Target));
        w.WriteString("platform", c.Platform.ToString());
        if (c.Backend is { } backend) w.WriteString("backend", Names.Of(backend));
        w.WriteBoolean("development", c.Development);
        if (c.IsEditor) w.WriteString("editorOs", Names.Of(c.EditorOs));
        w.WriteNumber("exitCode", cell.ExitCode);
        WriteSummary(w, cell.Diagnostics, 1, timings ? cell.Assemblies.SelectMany(a => a.AnalyzerTimings) : null);
        WriteProblems(w, cell.Problems);
        w.WriteStartArray("assemblies");
        foreach (var a in cell.Assemblies)
        {
            w.WriteStartObject();
            w.WriteString("name", a.Name);
            w.WriteString("kind", Names.Of(a.Kind));
            if (a.DefinitionPath is not null) w.WriteString("definition", a.DefinitionPath);
            w.WriteString("status", Names.Of(a.Status));
            if (a.SkipReason is not null) w.WriteString("skipReason", a.SkipReason);
            if (a.Status == AssemblyStatus.Skipped)
            {
                w.WriteStartArray("blockedBy");
                foreach (var b in a.BlockedBy)
                {
                    w.WriteStringValue(b);
                }

                w.WriteEndArray();
            }

            w.WriteString("inputsHash", a.InputsHash);
            w.WriteNumber("sourceFiles", a.SourceCount);
            Strings(w, "defines", a.Defines);
            Strings(w, "references", a.References);
            Strings(w, "analyzers", a.Analyzers);
            w.WriteStartObject("diagnosticCounts");
            w.WriteNumber("error", a.Diagnostics.Count(d => d.Severity == Severity.Error));
            w.WriteNumber("warning", a.Diagnostics.Count(d => d.Severity == Severity.Warning));
            w.WriteNumber("info", a.Diagnostics.Count(d => d.Severity == Severity.Info));
            w.WriteEndObject();
            if (timings)
            {
                w.WriteNumber("timeMs", a.ElapsedMs);
                w.WriteBoolean("cached", a.Cached);
                WriteAnalyzerTimings(w, a.AnalyzerTimings);
            }

            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteStartArray("excluded");
        foreach (var (name, reason) in cell.Excluded)
        {
            w.WriteStartObject();
            w.WriteString("name", name);
            w.WriteString("reason", reason);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteStartArray("diagnostics");
        foreach (var d in cell.Diagnostics)
        {
            w.WriteStartObject();
            w.WriteString("id", d.Id);
            w.WriteString("severity", Names.Of(d.Severity));
            w.WriteString("origin", Names.Of(d.Origin));
            if (d.Assembly is not null) w.WriteString("assembly", d.Assembly);
            if (d.File is not null) w.WriteString("file", d.File);
            w.WriteNumber("line", d.Line);
            w.WriteNumber("column", d.Column);
            w.WriteString("message", d.Message);
            if (d.WarningAsError) w.WriteBoolean("warningAsError", true);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteAnalyzerTimings(Utf8JsonWriter w, IEnumerable<AnalyzerTiming> timings)
    {
        w.WriteStartArray("analyzerTimings");
        foreach (var timing in timings.OrderBy(t => t.Path, StringComparer.Ordinal).ThenBy(t => t.Analyzer, StringComparer.Ordinal))
        {
            w.WriteStartObject();
            w.WriteString("path", timing.Path);
            w.WriteString("analyzer", timing.Analyzer);
            w.WriteNumber("timeMs", timing.TimeMs);
            w.WriteEndObject();
        }

        w.WriteEndArray();
    }

    private static void Strings(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name);
        foreach (var v in values)
        {
            w.WriteStringValue(v);
        }

        w.WriteEndArray();
    }
}
