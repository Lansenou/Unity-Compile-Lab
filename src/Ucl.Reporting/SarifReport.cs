using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ucl.Core.Model;
using Ucl.Core.Results;

namespace Ucl.Reporting;

/// <summary>SARIF 2.1.0 output: one run per matrix cell, paths relative to the <c>PROJECTROOT</c> base id.</summary>
public static class SarifReport
{
    /// <summary>Renders a run as SARIF 2.1.0.</summary>
    public static string Render(RunResult run)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("$schema", "https://json.schemastore.org/sarif-2.1.0.json");
            w.WriteString("version", "2.1.0");
            w.WriteStartArray("runs");
            if (run.Cells.Count == 0)
            {
                WriteRun(w, run, null, run.Problems, []);
            }

            foreach (var cell in run.Cells)
            {
                WriteRun(w, run, cell, run.Problems.Concat(cell.Problems).ToList(), cell.Diagnostics);
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static void WriteRun(Utf8JsonWriter w, RunResult run, CellResult? cell, IReadOnlyList<Problem> problems, IReadOnlyList<Diagnostic> diagnostics)
    {
        w.WriteStartObject();
        w.WriteStartObject("tool");
        w.WriteStartObject("driver");
        w.WriteString("name", "ucl");
        w.WriteString("version", run.ToolVersion);
        w.WriteString("informationUri", "https://github.com/Lansenou/unity-compile-lab");
        w.WriteStartArray("rules");
        foreach (var id in diagnostics.Select(d => d.Id).Concat(problems.Select(p => p.Id)).Distinct().Order(StringComparer.Ordinal))
        {
            w.WriteStartObject();
            w.WriteString("id", id);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteStartObject("originalUriBaseIds");
        w.WriteStartObject("PROJECTROOT");
        w.WriteStartObject("description");
        w.WriteString("text", "The Unity project folder passed to ucl.");
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteEndObject();
        if (cell is not null)
        {
            w.WriteStartObject("properties");
            w.WriteString("cell", cell.Cell.Label);
            w.WriteNumber("exitCode", cell.ExitCode);
            w.WriteEndObject();
        }

        w.WriteStartArray("results");
        foreach (var p in problems)
        {
            WriteResult(w, p.Id, "error", p.Message, p.File, 0, 0, "ucl", null);
        }

        foreach (var d in diagnostics)
        {
            var level = d.Severity switch { Severity.Error => "error", Severity.Warning => "warning", _ => "note" };
            WriteResult(w, d.Id, level, d.Message, d.File, d.Line, d.Column, Names.Of(d.Origin), d.Assembly);
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteResult(Utf8JsonWriter w, string id, string level, string message, string? file, int line, int column, string origin, string? assembly)
    {
        w.WriteStartObject();
        w.WriteString("ruleId", id);
        w.WriteString("level", level);
        w.WriteStartObject("message");
        w.WriteString("text", message);
        w.WriteEndObject();
        if (file is not null)
        {
            w.WriteStartArray("locations");
            w.WriteStartObject();
            w.WriteStartObject("physicalLocation");
            w.WriteStartObject("artifactLocation");
            w.WriteString("uri", Uri.EscapeDataString(file).Replace("%2F", "/", StringComparison.Ordinal));
            w.WriteString("uriBaseId", "PROJECTROOT");
            w.WriteEndObject();
            if (line > 0)
            {
                w.WriteStartObject("region");
                w.WriteNumber("startLine", line);
                w.WriteNumber("startColumn", column);
                w.WriteEndObject();
            }

            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndArray();
        }

        w.WriteStartObject("properties");
        w.WriteString("origin", origin);
        if (assembly is not null) w.WriteString("assembly", assembly);
        w.WriteEndObject();
        w.WriteEndObject();
    }

}
