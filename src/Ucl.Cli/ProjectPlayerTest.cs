using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ucl.Compilation;
using Ucl.Core.Graph;
using Ucl.Core.Testing;
using Ucl.Discovery;

namespace Ucl.Cli;

/// <summary>Routes the existing managed results and runs eligible engine cases in a project player.</summary>
internal static class ProjectPlayerTest
{
    public static TestRunReport Run(Session session, AssemblyGraph graph, EditorInstall editor,
        CliOptions options, TestRunReport report, TextWriter progress)
    {
        var editorCases = options.EditorCases is null ? new HashSet<string>(StringComparer.Ordinal)
            : File.ReadAllLines(options.EditorCases).Where(n => n.Length > 0).ToHashSet(StringComparer.Ordinal);
        var cases = report.Cases.Select(c => c with
        {
            Route = c.Category is TestCategory.NeedsUnity or TestCategory.UnityOnly ? "needs-editor" : "dotnet",
        }).ToList();
        var candidates = cases.Where(c => c.Category == TestCategory.NeedsUnity && !editorCases.Contains(c.FullName)).ToList();
        foreach (var c in cases.Where(c => editorCases.Contains(c.FullName)).ToList())
            cases[cases.IndexOf(c)] = c with { Route = "needs-editor", Category = TestCategory.NeedsUnity, Reason = "Audited Editor ownership (--editor-cases)." };
        if (candidates.Count == 0) return report with { Cases = cases };
        var player = ProjectPlayerCache.Get(session, editor, progress);
        var runDirectory = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(player))!, "runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDirectory);
        var managed = Path.Combine(Path.GetDirectoryName(player)!, "Host_Data", "Managed");
        var entry = Path.GetDirectoryName(Path.GetDirectoryName(player))!;
        var tests = candidates.Select(c => c.Assembly).ToHashSet(StringComparer.Ordinal);
        var compileKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", tests.Order(StringComparer.Ordinal)))));
        var dlls = Path.Combine(entry, "tests-" + compileKey);
        IReadOnlyDictionary<string, string> exclusions;
        using (ProjectPlayerCache.Lock(Path.Combine(entry, compileKey + ".lock")))
        {
            if (!PlayerHostCache.Valid(dlls, compileKey))
            {
                var temporary = Path.Combine(entry, "compile-" + Guid.NewGuid().ToString("N"));
                exclusions = PlayerTestCompiler.Compile(graph, session.Project, managed, temporary, tests);
                File.WriteAllText(Path.Combine(temporary, "compile-exclusions.json"), JsonSerializer.Serialize(exclusions));
                PlayerHostCache.Seal(temporary, compileKey);
                if (Directory.Exists(dlls)) Directory.Move(dlls, dlls + "-invalid-" + Guid.NewGuid().ToString("N"));
                Directory.Move(temporary, dlls);
            }
            else exclusions = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(dlls, "compile-exclusions.json")))!;
        }
        // dataPath describes the player's data tree. Source-layout assertions retain Editor ownership.
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in candidates.DistinctBy(c => (c.Assembly, c.ClassName)))
        {
            var plan = graph.Find(c.Assembly)!;
            var className = c.ClassName.Split('.', '+').Last();
            foreach (var source in plan.Sources)
            {
                var physical = session.Project.ToPhysical(source);
                var text = File.ReadAllText(physical);
                if (!Regex.IsMatch(text, @"\bclass\s+" + Regex.Escape(className) + @"\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) continue;
                if (text.Contains("Application.dataPath", StringComparison.Ordinal)) reasons[c.ClassName] = "Application.dataPath source-file expectations require the Editor.";
                else if (exclusions.TryGetValue(physical, out var reason)) reasons[c.ClassName] = "Editor API or unsupported player source file: " + reason;
            }
        }
        var selected = candidates.Where(c => !reasons.ContainsKey(c.ClassName) && File.Exists(Path.Combine(dlls, c.Assembly + ".dll"))).ToList();
        var caseFile = Path.Combine(runDirectory, "cases.txt");
        var assembliesFile = Path.Combine(runDirectory, "assemblies.txt");
        var resultsFile = Path.Combine(runDirectory, "results.json");
        File.WriteAllLines(caseFile, selected.Select(c => c.FullName));
        File.WriteAllLines(assembliesFile, tests.Select(n => Path.Combine(dlls, n + ".dll")).Where(File.Exists));
        progress.WriteLine($"host run: {selected.Count} selected cases, evidence {runDirectory}");
        var results = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (selected.Count > 0)
        {
            var arguments = new List<string> { "-batchmode", "-logFile", Path.Combine(runDirectory, "player.log"), "-assemblyDirectory", dlls,
                "-assemblies", assembliesFile, "-cases", caseFile, "-results", resultsFile };
            if (options.NoGraphics) arguments.Add("-nographics");
            var process = ProjectPlayerCache.Run(player, arguments, runDirectory, TimeSpan.FromMinutes(15), allowTestFailure: true);
            if (!File.Exists(resultsFile)) throw new IOException($"Player exited {process.ExitCode} without results; see {runDirectory}");
            using var result = JsonDocument.Parse(File.ReadAllText(resultsFile));
            if (result.RootElement.GetProperty("fatal").GetString() is { Length: > 0 } fatal) throw new IOException("UTF host failure: " + fatal);
            foreach (var leaf in result.RootElement.GetProperty("tests").EnumerateArray())
                results.Add(leaf.GetProperty("name").GetString()!, leaf.Clone());
            if (process.ExitCode != 0 && !results.Values.Any(r => r.GetProperty("outcome").GetString() == "Failed"))
                throw new IOException($"Player infrastructure failure, exit {process.ExitCode}; see {runDirectory}");
        }
        foreach (var c in candidates)
        {
            var index = cases.FindIndex(x => x.Assembly == c.Assembly && x.FullName == c.FullName);
            if (reasons.TryGetValue(c.ClassName, out var reason)) cases[index] = c with { Route = "needs-editor", Reason = reason };
            else if (!selected.Contains(c)) cases[index] = c with { Route = "needs-editor", Reason = "No player-compatible test assembly after whole-source exclusions." };
            else if (!results.TryGetValue(c.FullName, out var result)) cases[index] = c with { Route = "needs-editor", Reason = "Case absent from player discovery after whole-source exclusions." };
            else cases[index] = c with
            {
                Route = "host",
                Category = result.GetProperty("outcome").GetString() switch
                {
                    "Passed" => TestCategory.Passed,
                    "Skipped" or "Inconclusive" => TestCategory.Skipped,
                    _ => TestCategory.Failed,
                },
                Reason = result.GetProperty("message").GetString() ?? string.Empty,
                DurationMs = (long)(result.GetProperty("seconds").GetDouble() * 1000),
            };
        }
        return report with { Cases = cases };
    }
}
