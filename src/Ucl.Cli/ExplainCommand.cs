using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Rules;
using Ucl.Discovery;
using Ucl.Reporting;

namespace Ucl.Cli;

/// <summary><c>ucl explain &lt;file.cs&gt;</c>: which assembly owns a file in each cell, why, and with which defines and references.</summary>
internal static class ExplainCommand
{
    public static int Run(CliOptions options, TextWriter stdout, TextWriter stderr, IEnvironment env)
    {
        if (options.Positionals.Count != 1)
        {
            stderr.WriteLine($"error {ProblemIds.BadArguments}: usage: ucl explain <file.cs> [--project <path>] [matrix options]");
            return ExitCodes.Configuration;
        }

        var file = Path.GetFullPath(options.Positionals[0]);
        var root = options.Project ?? FindProjectRoot(Path.GetDirectoryName(file)!);
        if (root is null)
        {
            stderr.WriteLine($"error {ProblemIds.NotAProject}: no Unity project (a folder with Assets/ and ProjectSettings/) above {file}; pass --project");
            return ExitCodes.Configuration;
        }

        var session = Session.Open(options with { Project = root, Positionals = [] }, env, needEditor: false);
        if (session.Project.Inventory is not { } inventory)
        {
            foreach (var p in session.Problems)
            {
                stderr.WriteLine(TextReport.FormatProblem(p));
            }

            return ExitCodes.Configuration;
        }

        var logical = session.Project.ToLogical(file) ?? file;
        var cells = session.Cells.Select(c => Explain(session.Graph(c.Cell), inventory, logical)).ToList();
        var text = options.Format == "json" ? Json(logical, cells) : Text(logical, cells, session.Problems);
        stdout.Write(text);
        return session.Problems.Count > 0 ? ExitCodes.Configuration : cells.All(c => c.Owner is null) ? ExitCodes.Errors : ExitCodes.Clean;
    }

    private static string? FindProjectRoot(string dir)
    {
        for (var d = dir; d is not null; d = Path.GetDirectoryName(d))
        {
            if (Directory.Exists(Path.Combine(d, "Assets")) && Directory.Exists(Path.Combine(d, "ProjectSettings")))
            {
                return d;
            }
        }

        return null;
    }

    private static CellExplanation Explain(AssemblyGraph graph, ProjectInventory inventory, string logical)
    {
        var owner = graph.ScriptOwners.GetValueOrDefault(logical);
        string why;
        if (!inventory.Scripts.Contains(logical))
        {
            why = "not a C# script ucl sees: outside Assets/ and packages, hidden (a '.' or '~' folder), or not a .cs file";
        }
        else if (owner is null)
        {
            why = graph.Diagnostics.FirstOrDefault(d => d.File == logical)?.Message ?? "not owned by any assembly";
        }
        else
        {
            var definition = ProjectPaths.SelfAndAncestors(ProjectPaths.Folder(logical))
                .Select(f => inventory.Asmdefs.Concat(inventory.Asmrefs).FirstOrDefault(d => ProjectPaths.Folder(d.Path) == f))
                .FirstOrDefault(d => d is not null);
            why = definition is not null
                ? $"nearest assembly definition above the file is {definition.Path}"
                : SpecialFolderReason(logical, owner);
        }

        var plan = owner is null ? null : graph.Find(owner);
        return new CellExplanation(graph.Cell, owner, why, plan, owner is not null && plan is null ? graph.Excluded.GetValueOrDefault(owner, "not compiled in this cell") : null);
    }

    private static string SpecialFolderReason(string logical, string owner)
    {
        var parts = new List<string> { "no assembly definition above the file, so Unity's special-folder rules apply" };
        if (SpecialFolders.IsFirstPass(logical)) parts.Add("it is under Assets/Plugins, Assets/Standard Assets or Assets/Pro Standard Assets (first pass)");
        if (SpecialFolders.IsInEditorFolder(logical)) parts.Add("it is inside a folder named Editor");
        parts.Add($"so it belongs to {owner}");
        return string.Join("; ", parts);
    }

    private static string Text(string logical, IReadOnlyList<CellExplanation> cells, IReadOnlyList<Problem> problems)
    {
        var sb = new StringBuilder();
        foreach (var p in problems)
        {
            sb.Append(TextReport.FormatProblem(p)).Append('\n');
        }

        sb.Append(logical).Append('\n');
        foreach (var c in cells)
        {
            sb.Append("== ").Append(c.Cell.Label).Append('\n');
            sb.Append("assembly: ").Append(c.Owner ?? "(none)").Append('\n');
            sb.Append("why: ").Append(c.Why).Append('\n');
            if (c.ExcludedReason is not null)
            {
                sb.Append("not compiled in this cell: ").Append(c.ExcludedReason).Append('\n');
            }

            if (c.Plan is { } plan)
            {
                sb.Append("references: ").Append(plan.References.Count == 0 ? "(none)" : string.Join(", ", plan.References)).Append('\n');
                foreach (var r in plan.PrecompiledReferences) sb.Append("precompiled: ").Append(r).Append('\n');
                foreach (var a in plan.Analyzers) sb.Append("analyzer: ").Append(a).Append('\n');
                foreach (var r in plan.DroppedReferences) sb.Append("dropped reference (not compiled in this cell): ").Append(r).Append('\n');
                if (plan.ResponseFile is not null) sb.Append("response file: ").Append(plan.ResponseFile).Append('\n');
                sb.Append($"options: C# {plan.LangVersion}, nullable {plan.Nullable}, unsafe {(plan.AllowUnsafe ? "on" : "off")}, engine references {plan.Engine}\n");
                sb.Append("defines:\n");
                foreach (var (symbol, reason) in plan.Defines.Reasons)
                {
                    sb.Append("  ").Append(symbol).Append("  (").Append(reason).Append(")\n");
                }
            }
        }

        return sb.ToString();
    }

    private static string Json(string logical, IReadOnlyList<CellExplanation> cells)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("schema", "ucl-explain/1");
            w.WriteString("file", logical);
            w.WriteStartArray("cells");
            foreach (var c in cells)
            {
                w.WriteStartObject();
                w.WriteString("unityVersion", c.Cell.UnityVersion.ToString());
                w.WriteString("target", Names.Of(c.Cell.Target));
                w.WriteString("platform", c.Cell.Platform.ToString());
                if (c.Owner is null) w.WriteNull("assembly"); else w.WriteString("assembly", c.Owner);
                w.WriteString("why", c.Why);
                w.WriteBoolean("compiled", c.Plan is not null);
                if (c.ExcludedReason is not null) w.WriteString("excludedReason", c.ExcludedReason);
                if (c.Plan is { } plan)
                {
                    w.WriteStartArray("references");
                    foreach (var r in plan.References) w.WriteStringValue(r);
                    w.WriteEndArray();
                    w.WriteStartObject("defines");
                    foreach (var (symbol, reason) in plan.Defines.Reasons) w.WriteString(symbol, reason);
                    w.WriteEndObject();
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private sealed record CellExplanation(CompileCell Cell, string? Owner, string Why, AssemblyPlan? Plan, string? ExcludedReason);
}
