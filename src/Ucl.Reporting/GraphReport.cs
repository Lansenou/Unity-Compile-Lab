using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ucl.Core.Graph;
using Ucl.Core.Model;

namespace Ucl.Reporting;

/// <summary>Renders assembly graphs (<c>ucl graph</c>) as text, Graphviz DOT, or JSON (schema <c>ucl-graph/1</c>).</summary>
public static class GraphReport
{
    /// <summary>The JSON schema identifier.</summary>
    public const string Schema = "ucl-graph/1";

    /// <summary>Text: one block per assembly with its references and defines.</summary>
    public static string Text(IReadOnlyList<AssemblyGraph> graphs, IReadOnlyList<Problem> problems)
    {
        var sb = new StringBuilder();
        foreach (var p in problems)
        {
            sb.Append(TextReport.FormatProblem(p)).Append('\n');
        }

        foreach (var g in graphs)
        {
            sb.Append("== ").Append(g.Cell.Label).Append('\n');
            foreach (var a in g.Assemblies)
            {
                sb.Append(a.Name).Append(" (").Append(Names.Of(a.Kind)).Append(a.DefinitionPath is null ? string.Empty : ", " + a.DefinitionPath)
                  .Append(", ").Append(a.Sources.Count).Append(a.Sources.Count == 1 ? " file" : " files").Append(")\n");
                foreach (var r in a.References)
                {
                    sb.Append("  -> ").Append(r).Append('\n');
                }

                foreach (var r in a.PrecompiledReferences)
                {
                    sb.Append("  -> ").Append(r).Append('\n');
                }

                foreach (var r in a.DroppedReferences)
                {
                    sb.Append("  -x ").Append(r).Append(" (not compiled in this cell)\n");
                }
            }

            foreach (var (name, reason) in g.Excluded)
            {
                sb.Append("excluded ").Append(name).Append(": ").Append(reason).Append('\n');
            }

            foreach (var d in g.Diagnostics)
            {
                sb.Append(TextReport.Format(d)).Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>Graphviz DOT, one cluster per cell.</summary>
    public static string Dot(IReadOnlyList<AssemblyGraph> graphs)
    {
        var sb = new StringBuilder("digraph ucl {\n  rankdir=LR;\n  node [shape=box];\n");
        for (var i = 0; i < graphs.Count; i++)
        {
            var g = graphs[i];
            sb.Append($"  subgraph cluster_{i} {{\n    label=\"{Escape(g.Cell.Label)}\";\n");
            foreach (var a in g.Assemblies)
            {
                sb.Append($"    \"c{i}:{Escape(a.Name)}\" [label=\"{Escape(a.Name)}\"{(a.Kind == AssemblyKind.Predefined ? ", style=rounded" : string.Empty)}];\n");
                foreach (var r in a.References)
                {
                    sb.Append($"    \"c{i}:{Escape(a.Name)}\" -> \"c{i}:{Escape(r)}\";\n");
                }
            }

            sb.Append("  }\n");
        }

        return sb.Append("}\n").ToString();
    }

    /// <summary>JSON, schema <c>ucl-graph/1</c>.</summary>
    public static string Json(IReadOnlyList<AssemblyGraph> graphs, IReadOnlyList<Problem> problems, string toolVersion)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("schema", Schema);
            w.WriteString("toolVersion", toolVersion);
            w.WriteStartArray("problems");
            foreach (var p in problems)
            {
                w.WriteStartObject();
                w.WriteString("id", p.Id);
                w.WriteString("message", p.Message);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("cells");
            foreach (var g in graphs)
            {
                w.WriteStartObject();
                w.WriteString("unityVersion", g.Cell.UnityVersion.ToString());
                w.WriteString("target", Names.Of(g.Cell.Target));
                w.WriteString("platform", g.Cell.Platform.ToString());
                w.WriteStartArray("assemblies");
                foreach (var a in g.Assemblies)
                {
                    w.WriteStartObject();
                    w.WriteString("name", a.Name);
                    w.WriteString("kind", Names.Of(a.Kind));
                    if (a.DefinitionPath is not null) w.WriteString("definition", a.DefinitionPath);
                    Array(w, "sources", a.Sources);
                    Array(w, "references", a.References);
                    Array(w, "droppedReferences", a.DroppedReferences);
                    Array(w, "precompiledReferences", a.PrecompiledReferences);
                    Array(w, "analyzers", a.Analyzers);
                    Array(w, "defines", a.Defines.Symbols);
                    w.WriteBoolean("allowUnsafe", a.AllowUnsafe);
                    w.WriteString("engine", a.Engine.ToString());
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteStartArray("excluded");
                foreach (var (name, reason) in g.Excluded)
                {
                    w.WriteStartObject();
                    w.WriteString("name", name);
                    w.WriteString("reason", reason);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteStartArray("diagnostics");
                foreach (var d in g.Diagnostics)
                {
                    w.WriteStartObject();
                    w.WriteString("id", d.Id);
                    w.WriteString("severity", Names.Of(d.Severity));
                    if (d.File is not null) w.WriteString("file", d.File);
                    w.WriteString("message", d.Message);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteStartArray("problems");
                foreach (var p in g.Problems)
                {
                    w.WriteStartObject();
                    w.WriteString("id", p.Id);
                    w.WriteString("message", p.Message);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static void Array(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name);
        foreach (var v in values)
        {
            w.WriteStringValue(v);
        }

        w.WriteEndArray();
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
