using System.Text.Json;
using System.Text.RegularExpressions;
using Ucl.Compilation;
using Ucl.Core.Graph;
using Ucl.Core.Testing;
using Ucl.Discovery;
using Ucl.Testing;

namespace Ucl.Cli;

/// <summary>Routes the existing managed results and runs eligible engine cases in a project player.</summary>
internal static class ProjectPlayerTest
{
    public static TestRunReport Run(Session session, AssemblyGraph graph, EditorInstall editor,
        CliOptions options, TestRunReport report, TextWriter progress, PhaseTimer? timer)
    {
        var editorCases = options.EditorCases is null ? new HashSet<string>(StringComparer.Ordinal)
            : File.ReadAllLines(options.EditorCases).Where(n => n.Length > 0).ToHashSet(StringComparer.Ordinal);
        bool Audited(TestCaseResult c) => editorCases.Contains(c.FullName) || editorCases.Contains(NUnitHost.Key(c.Assembly, c.FullName));
        var cases = report.Cases.Select(c => c with
        {
            Route = c.Category is TestCategory.NeedsUnity or TestCategory.UnityOnly ? "needs-editor" : "dotnet",
        }).ToList();
        var candidates = cases.Where(c => PlayerCandidate(c) && !Audited(c)).ToList();
        foreach (var c in cases.Where(c => PlayerCandidate(c) && Audited(c)).ToList())
            cases[cases.IndexOf(c)] = c with { Route = "needs-editor", Reason = "Audited Editor ownership (--editor-cases)." };
        if (candidates.Count == 0) return report with { Cases = cases };
        var player = ProjectPlayerCache.Get(session, editor, progress);
        timer?.Mark("player-cache");
        var runDirectory = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(player))!, "runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDirectory);
        var managed = Path.Combine(Path.GetDirectoryName(player)!, "Host_Data", "Managed");
        var entry = Path.GetDirectoryName(Path.GetDirectoryName(player))!;
        var tests = candidates.Select(c => c.Assembly).ToHashSet(StringComparer.Ordinal);
        var compileKey = PlayerTestCompiler.Key(graph, session.Project, tests, options.Analyzers, editor.SourceGenerators);
        var dlls = Path.Combine(entry, "tests-" + compileKey);
        IReadOnlyDictionary<string, string> exclusions;
        using (ProjectPlayerCache.Lock(Path.Combine(entry, compileKey + ".lock")))
        {
            if (!PlayerHostCache.Valid(dlls, compileKey))
            {
                var temporary = Path.Combine(entry, "compile-" + Guid.NewGuid().ToString("N"));
                exclusions = PlayerTestCompiler.Compile(graph, session.Project, managed, temporary, tests, options.Analyzers, editor.SourceGenerators);
                File.WriteAllText(Path.Combine(temporary, "compile-exclusions.json"), JsonSerializer.Serialize(exclusions));
                PlayerHostCache.Seal(temporary, compileKey);
                if (Directory.Exists(dlls)) Directory.Move(dlls, dlls + "-invalid-" + Guid.NewGuid().ToString("N"));
                Directory.Move(temporary, dlls);
            }
            else exclusions = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(dlls, "compile-exclusions.json")))!;
        }
        timer?.Mark("player-compile");
        var reasons = SourceReasons(graph, session.Project, candidates, exclusions);
        var selected = candidates.Where(c => !reasons.ContainsKey((c.Assembly, c.ClassName)) && File.Exists(Path.Combine(dlls, c.Assembly + ".dll"))).ToList();
        var caseFile = Path.Combine(runDirectory, "cases.txt");
        var assembliesFile = Path.Combine(runDirectory, "assemblies.txt");
        var resultsFile = Path.Combine(runDirectory, "results.json");
        File.WriteAllLines(caseFile, selected.Select(c => c.Assembly + "\t" + c.FullName));
        progress.WriteLine($"host run: {selected.Count} selected cases, evidence {runDirectory}");
        timer?.Mark("player-routing");
        var results = new Dictionary<(string Assembly, string Name), JsonElement>();
        if (selected.Count > 0)
        {
            var staged = StageAssemblies(dlls, session.ProjectRoot);
            File.WriteAllLines(assembliesFile, tests.Select(n => Path.Combine(staged, n + ".dll")).Where(File.Exists));
            var arguments = new List<string> { "-batchmode", "-logFile", Path.Combine(runDirectory, "player.log"), "-assemblyDirectory", staged,
                "-assemblies", assembliesFile, "-cases", caseFile, "-results", resultsFile };
            if (options.NoGraphics) arguments.Add("-nographics");
            var launched = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // The Editor's working directory is the project root; relative project paths resolve the same way.
            ProcessResult process;
            try { process = ProjectPlayerCache.Run(player, arguments, session.ProjectRoot, TimeSpan.FromMinutes(60), allowTestFailure: true); }
            finally
            {
                try { Directory.Delete(staged, recursive: true); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { progress.WriteLine("host run: could not remove " + staged); }
            }
            var exited = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (!File.Exists(resultsFile)) throw new IOException($"Player exited {process.ExitCode} without results; see {runDirectory}");
            using var result = JsonDocument.Parse(File.ReadAllText(resultsFile));
            var first = result.RootElement.GetProperty("firstTestUnixMs").GetInt64();
            var finished = result.RootElement.GetProperty("finishedUnixMs").GetInt64();
            if (first > 0)
            {
                // The player's wall clock: start to first case (boot and discovery), cases, results written to exit.
                timer?.Detail("player-boot", TimeSpan.FromMilliseconds(first - launched));
                timer?.Detail("player-cases", TimeSpan.FromMilliseconds(finished - first));
                timer?.Detail("player-exit", TimeSpan.FromMilliseconds(exited - finished));
            }
            if (result.RootElement.GetProperty("fatal").GetString() is { Length: > 0 } fatal) throw new IOException("UTF host failure: " + fatal);
            foreach (var leaf in result.RootElement.GetProperty("tests").EnumerateArray())
                results.Add((leaf.GetProperty("assembly").GetString()!, leaf.GetProperty("name").GetString()!), leaf.Clone());
            if (process.ExitCode != 0 && !results.Values.Any(r => r.GetProperty("outcome").GetString() == "Failed"))
                throw new IOException($"Player infrastructure failure, exit {process.ExitCode}; see {runDirectory}");
        }
        timer?.Mark("player-run");
        foreach (var c in candidates)
        {
            var index = cases.FindIndex(x => x.Assembly == c.Assembly && x.FullName == c.FullName);
            if (reasons.TryGetValue((c.Assembly, c.ClassName), out var reason)) cases[index] = c with { Route = "needs-editor", Reason = reason };
            else if (!selected.Contains(c)) cases[index] = c with { Route = "needs-editor", Reason = "No player-compatible test assembly after whole-source exclusions." };
            else if (!results.TryGetValue((c.Assembly, c.FullName), out var result)) cases[index] = c with { Route = "needs-editor", Reason = "Case absent from player discovery after whole-source exclusions." };
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

    // Engine cases, [UnityTest] (the player runs its coroutines) and Play Mode cases; [UnityPlatform] means the Editor's platform.
    internal static bool PlayerCandidate(TestCaseResult c) => c.Category == TestCategory.NeedsUnity
        || c.Category == TestCategory.UnityOnly && c.Reason != TestClassifier.UnityOnlyAttributes["UnityEngine.TestTools.UnityPlatformAttribute"];

    // NUnit's TestDirectory is the test assembly's folder: Library/ucl/<run> mirrors the Editor's Library/ScriptAssemblies.
    internal static string StageAssemblies(string dlls, string projectRoot)
    {
        var staged = Path.Combine(projectRoot, "Library", "ucl", "player-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staged);
        foreach (var dll in Directory.EnumerateFiles(dlls, "*.dll")) File.Copy(dll, Path.Combine(staged, Path.GetFileName(dll)));
        return staged;
    }

    internal static Dictionary<(string Assembly, string Class), string> SourceReasons(AssemblyGraph graph,
        ProjectContext project, IReadOnlyList<TestCaseResult> candidates, IReadOnlyDictionary<string, string> exclusions,
        Func<string, string>? read = null)
    {
        // The compiler owns whole-file exclusions; routing uses their original source paths.
        read ??= File.ReadAllText;
        var sourceTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        var reasons = new Dictionary<(string Assembly, string Class), string>();
        foreach (var c in candidates.DistinctBy(c => (c.Assembly, c.ClassName)))
        {
            var plan = graph.Find(c.Assembly)!;
            var className = c.ClassName.Split('.', '+').Last();
            foreach (var source in plan.Sources)
            {
                var physical = project.ToPhysical(source);
                if (!exclusions.TryGetValue(physical, out var reason)) continue;
                if (!sourceTexts.TryGetValue(physical, out var text))
                {
                    text = read(physical);
                    sourceTexts.Add(physical, text);
                }
                if (!Regex.IsMatch(text, @"\bclass\s+" + Regex.Escape(className) + @"\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) continue;
                reasons[(c.Assembly, c.ClassName)] = "Editor API or unsupported player source file: " + reason;
            }
        }
        return reasons;
    }
}
