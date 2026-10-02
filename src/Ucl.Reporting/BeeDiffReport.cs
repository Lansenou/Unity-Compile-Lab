using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ucl.Core.Bee;
using Ucl.Core.Model;

namespace Ucl.Reporting;

/// <summary><c>ucl bee-diff</c> output: text, JSON (schema <c>ucl-beediff/1</c>) and SARIF 2.1.0. Deterministic.</summary>
public static class BeeDiffReport
{
    /// <summary>The JSON schema identifier.</summary>
    public const string Schema = "ucl-beediff/1";

    /// <summary>Lowercase category name (JSON contract).</summary>
    public static string Name(BeeCategory c) => c switch
    {
        BeeCategory.Assembly => "assembly",
        BeeCategory.Sources => "sources",
        BeeCategory.References => "references",
        BeeCategory.Defines => "defines",
        BeeCategory.Options => "options",
        BeeCategory.NoWarn => "nowarn",
        BeeCategory.Analyzers => "analyzers",
        _ => "additionalfiles",
    };

    /// <summary>Lowercase change name (JSON contract).</summary>
    public static string Name(BeeChange c) => c switch { BeeChange.Missing => "missing", BeeChange.Extra => "extra", _ => "changed" };

    /// <summary>The <c>UCL5xxx</c> id of a category (SARIF rule id).</summary>
    public static string RuleId(BeeCategory c) => c switch
    {
        BeeCategory.Assembly => ProblemIds.BeeAssembly,
        BeeCategory.Sources => ProblemIds.BeeSources,
        BeeCategory.References => ProblemIds.BeeReferences,
        BeeCategory.Defines => ProblemIds.BeeDefines,
        BeeCategory.Options => ProblemIds.BeeOptions,
        BeeCategory.NoWarn => ProblemIds.BeeNoWarn,
        BeeCategory.Analyzers => ProblemIds.BeeAnalyzers,
        _ => ProblemIds.BeeAdditionalFiles,
    };

    /// <summary>One line describing a difference.</summary>
    public static string Describe(BeeDifference d) => d.Change switch
    {
        BeeChange.Missing => $"{Name(d.Category)} missing in ucl: {d.Value}",
        BeeChange.Extra => $"{Name(d.Category)} extra in ucl: {d.Value}",
        _ => $"{Name(d.Category)} differ for {d.Value}: Editor {d.Expected}, ucl {d.Actual}",
    };

    /// <summary>Human-readable report.</summary>
    public static string Text(BeeDiffResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var sb = new StringBuilder();
        foreach (var p in result.Problems)
        {
            sb.Append(TextReport.FormatProblem(p)).Append('\n');
        }

        foreach (var dag in result.Dags)
        {
            sb.Append("== ").Append(dag.Name);
            if (dag.Cell is { } cell)
            {
                sb.Append(" (").Append(cell.Label).Append(cell.IsEditor ? $" {Names.Of(cell.EditorOs)}" : string.Empty).Append(')');
            }

            sb.Append('\n');
            if (dag.Note is not null)
            {
                sb.Append("not compared: ").Append(dag.Note).Append('\n');
            }

            foreach (var a in dag.Assemblies)
            {
                sb.Append(a.Name).Append(": ").Append(a.Agrees ? "agrees" : Plural(a.Differences.Count, "difference")).Append('\n');
                foreach (var d in a.Differences)
                {
                    sb.Append("  ").Append(Describe(d)).Append('\n');
                }
            }
        }

        var assemblies = result.Dags.SelectMany(d => d.Assemblies).ToList();
        if (result.DifferenceCount > 0)
        {
            sb.Append("by category: ").Append(string.Join(", ", ByCategory(result).Select(c => $"{Name(c.Key)} {c.Value}"))).Append('\n');
        }

        sb.Append($"result: {Plural(result.Dags.Count, "dag")}, {Plural(assemblies.Count, "assembly", "assemblies")} ({assemblies.Count(a => a.Agrees)} agree), {Plural(result.DifferenceCount, "difference")}, exit {result.ExitCode}\n");
        return sb.ToString();
    }

    /// <summary>Difference counts per category, in category order, categories with differences only.</summary>
    public static IReadOnlyList<KeyValuePair<BeeCategory, int>> ByCategory(BeeDiffResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return [.. result.Dags.SelectMany(d => d.Assemblies).SelectMany(a => a.Differences)
            .GroupBy(d => d.Category).OrderBy(g => g.Key).Select(g => new KeyValuePair<BeeCategory, int>(g.Key, g.Count()))];
    }

    /// <summary>JSON report, schema <c>ucl-beediff/1</c>.</summary>
    public static string Json(BeeDiffResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Write(w =>
        {
            w.WriteStartObject();
            w.WriteString("schema", Schema);
            w.WriteStartObject("tool");
            w.WriteString("name", "ucl");
            w.WriteString("version", result.ToolVersion);
            w.WriteEndObject();
            w.WriteNumber("exitCode", result.ExitCode);
            var assemblies = result.Dags.SelectMany(d => d.Assemblies).ToList();
            w.WriteStartObject("summary");
            w.WriteNumber("dags", result.Dags.Count);
            w.WriteNumber("assemblies", assemblies.Count);
            w.WriteNumber("agree", assemblies.Count(a => a.Agrees));
            w.WriteNumber("differences", result.DifferenceCount);
            w.WriteStartObject("byCategory");
            foreach (var (category, count) in ByCategory(result))
            {
                w.WriteNumber(Name(category), count);
            }

            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteStartArray("problems");
            foreach (var p in result.Problems)
            {
                w.WriteStartObject();
                w.WriteString("id", p.Id);
                w.WriteString("message", p.Message);
                if (p.File is not null)
                {
                    w.WriteString("file", p.File);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("dags");
            foreach (var dag in result.Dags)
            {
                w.WriteStartObject();
                w.WriteString("name", dag.Name);
                if (dag.Cell is { } cell)
                {
                    w.WriteStartObject("cell");
                    w.WriteString("unityVersion", cell.UnityVersion.ToString());
                    w.WriteString("target", Names.Of(cell.Target));
                    w.WriteString("platform", cell.Platform.ToString());
                    w.WriteBoolean("development", cell.Development);
                    if (cell.IsEditor)
                    {
                        w.WriteString("editorOs", Names.Of(cell.EditorOs));
                    }

                    w.WriteEndObject();
                }

                if (dag.Note is not null)
                {
                    w.WriteString("note", dag.Note);
                }

                w.WriteStartArray("assemblies");
                foreach (var a in dag.Assemblies)
                {
                    w.WriteStartObject();
                    w.WriteString("name", a.Name);
                    w.WriteString("status", a.Agrees ? "agrees" : "differs");
                    if (a.ResponseFile is not null)
                    {
                        w.WriteString("responseFile", a.ResponseFile);
                    }

                    if (a.DefinitionPath is not null)
                    {
                        w.WriteString("definition", a.DefinitionPath);
                    }

                    w.WriteStartArray("differences");
                    foreach (var d in a.Differences)
                    {
                        w.WriteStartObject();
                        w.WriteString("category", Name(d.Category));
                        w.WriteString("change", Name(d.Change));
                        w.WriteString("value", d.Value);
                        if (d.Expected is not null)
                        {
                            w.WriteString("editor", d.Expected);
                        }

                        if (d.Actual is not null)
                        {
                            w.WriteString("ucl", d.Actual);
                        }

                        w.WriteEndObject();
                    }

                    w.WriteEndArray();
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    /// <summary>SARIF 2.1.0: one result per difference (rule <c>UCL5xxx</c>), located at the asmdef or the response file.</summary>
    public static string Sarif(BeeDiffResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var findings = result.Dags
            .SelectMany(dag => dag.Assemblies.SelectMany(a => a.Differences.Select(d => (Dag: dag, Assembly: a, Difference: d))))
            .ToList();
        return Write(w =>
        {
            w.WriteStartObject();
            w.WriteString("$schema", "https://json.schemastore.org/sarif-2.1.0.json");
            w.WriteString("version", "2.1.0");
            w.WriteStartArray("runs");
            w.WriteStartObject();
            w.WriteStartObject("tool");
            w.WriteStartObject("driver");
            w.WriteString("name", "ucl bee-diff");
            w.WriteString("version", result.ToolVersion);
            w.WriteString("informationUri", "https://github.com/Lansenou/unity-compile-lab");
            w.WriteStartArray("rules");
            foreach (var id in findings.Select(f => RuleId(f.Difference.Category)).Concat(result.Problems.Select(p => p.Id)).Distinct().Order(StringComparer.Ordinal))
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
            w.WriteString("text", "The Unity project root.");
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteStartArray("results");
            foreach (var p in result.Problems)
            {
                w.WriteStartObject();
                w.WriteString("ruleId", p.Id);
                w.WriteString("level", "error");
                w.WriteStartObject("message");
                w.WriteString("text", p.Message);
                w.WriteEndObject();
                w.WriteEndObject();
            }

            foreach (var (dag, assembly, d) in findings)
            {
                w.WriteStartObject();
                w.WriteString("ruleId", RuleId(d.Category));
                w.WriteString("level", "error");
                w.WriteStartObject("message");
                w.WriteString("text", $"{dag.Name} {assembly.Name}: {Describe(d)}");
                w.WriteEndObject();
                if ((assembly.DefinitionPath ?? assembly.ResponseFile) is { } file)
                {
                    w.WriteStartArray("locations");
                    w.WriteStartObject();
                    w.WriteStartObject("physicalLocation");
                    w.WriteStartObject("artifactLocation");
                    w.WriteString("uri", Uri.EscapeDataString(file).Replace("%2F", "/", StringComparison.Ordinal));
                    w.WriteString("uriBaseId", "PROJECTROOT");
                    w.WriteEndObject();
                    w.WriteEndObject();
                    w.WriteEndObject();
                    w.WriteEndArray();
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            body(w);
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static string Plural(int n, string one, string? many = null) => n == 1 ? $"1 {one}" : $"{n} {many ?? one + "s"}";
}
