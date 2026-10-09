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
        TestHostCrash? crash = null;
        if (selected.Count > 0)
        {
            var staged = StageAssemblies(dlls, session.ProjectRoot);
            File.WriteAllLines(assembliesFile, AssemblyList(graph, tests, staged));
            var arguments = new List<string> { "-batchmode", "-logFile", Path.Combine(runDirectory, "player.log"), "-assemblyDirectory", staged,
                "-assemblies", assembliesFile, "-cases", caseFile, "-results", resultsFile };
            if (options.NoGraphics) arguments.Add("-nographics");
            var launched = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // The Editor's working directory is the project root; relative project paths resolve the same way.
            var exitCode = -1;
            var ending = "timed out after 60 minutes";
            try
            {
                exitCode = ProjectPlayerCache.Run(player, arguments, session.ProjectRoot, TimeSpan.FromMinutes(60), allowTestFailure: true).ExitCode;
                ending = "exited " + exitCode;
            }
            catch (OperationCanceledException) when (File.Exists(resultsFile + ".jsonl")) { }
            finally
            {
                try { Directory.Delete(staged, recursive: true); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { progress.WriteLine("host run: could not remove " + staged); }
            }
            var exited = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            crash = Collect(resultsFile, exitCode, ending, launched, exited, timer, selected, results);
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
        return report with { Cases = cases, HostCrashes = crash is null ? report.HostCrashes : [.. report.HostCrashes, crash] };
    }

    // The player streams each result as it ends; a player that dies, times out or leaves a torn results.json keeps them,
    // as the .NET host does.
    internal static TestHostCrash? Collect(string resultsFile, int exitCode, string ending, long launched, long exited, PhaseTimer? timer,
        IReadOnlyList<TestCaseResult> selected, Dictionary<(string Assembly, string Name), JsonElement> results)
    {
        if (File.Exists(resultsFile))
        {
            try
            {
                ReadResults(resultsFile, exitCode, launched, exited, timer, results);
                return null;
            }
            catch (JsonException) when (File.Exists(resultsFile + ".jsonl")) { results.Clear(); }
        }
        if (File.Exists(resultsFile + ".jsonl")) return Salvage(resultsFile, ending, selected, results);
        throw new IOException($"Player {ending} without results; see {Path.GetDirectoryName(resultsFile)}");
    }

    private static void ReadResults(string resultsFile, int exitCode, long launched, long exited, PhaseTimer? timer,
        Dictionary<(string Assembly, string Name), JsonElement> results)
    {
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
        if (exitCode != 0 && !results.Values.Any(r => r.GetProperty("outcome").GetString() == "Failed"))
            throw new IOException($"Player infrastructure failure, exit {exitCode}; see {Path.GetDirectoryName(resultsFile)}");
    }

    // Completed cases keep their results, a record torn by the kill is dropped; the case in flight (results.json.started:
    // assembly, then name) fails, later cases are not run.
    internal static TestHostCrash Salvage(string resultsFile, string ending, IReadOnlyList<TestCaseResult> selected,
        Dictionary<(string Assembly, string Name), JsonElement> results)
    {
        string? after = null;
        foreach (var line in File.ReadLines(resultsFile + ".jsonl").Where(l => l.Trim().Length > 0))
        {
            JsonDocument leaf;
            try { leaf = JsonDocument.Parse(line); }
            catch (JsonException) { continue; }
            using (leaf)
            {
                after = leaf.RootElement.GetProperty("name").GetString()!;
                results[(leaf.RootElement.GetProperty("assembly").GetString()!, after)] = leaf.RootElement.Clone();
            }
        }
        var started = resultsFile + ".started";
        var marker = File.Exists(started) ? File.ReadAllLines(started) : [];
        (string Assembly, string Name)? during = marker.Length == 2 ? (marker[0], marker[1]) : null;
        if (during is { } key && results.ContainsKey(key)) during = null;
        foreach (var c in selected.Where(c => !results.ContainsKey((c.Assembly, c.FullName))))
        {
            var message = (c.Assembly, c.FullName) == during ? $"player {ending} during this case" : $"not run: player {ending} before this case";
            results[(c.Assembly, c.FullName)] = JsonSerializer.SerializeToElement(new { assembly = c.Assembly, name = c.FullName, outcome = "Failed", message, seconds = 0.0 });
        }
        return new TestHostCrash(after, during?.Name, $"Player {ending} without a complete results.json; see {Path.GetDirectoryName(resultsFile)}");
    }

    // Engine cases, [UnityTest] (the player runs its coroutines) and Play Mode cases; [UnityPlatform] on the method, class
    // or assembly means the Editor's platform and wins over every other reason.
    internal static bool PlayerCandidate(TestCaseResult c) => !c.EditorPlatform
        && c.Category is TestCategory.NeedsUnity or TestCategory.UnityOnly;

    // <platform>, a tab, <path> per staged test assembly, EditMode first: the Editor runs Editor-only assemblies as
    // EditMode and the others as Play Mode, and UTF's TestMode (which rejects Edit Mode yields in Play Mode) is per run.
    internal static IEnumerable<string> AssemblyList(AssemblyGraph graph, IEnumerable<string> tests, string staged) => tests
        .Select(n => (Plan: graph.Find(n)!, Path: Path.Combine(staged, n + ".dll"))).Where(t => File.Exists(t.Path))
        .OrderBy(t => t.Plan.IsEditorOnly ? 0 : 1).ThenBy(t => t.Plan.Name, StringComparer.Ordinal)
        .Select(t => (t.Plan.IsEditorOnly ? "EditMode" : "PlayMode") + "\t" + t.Path);

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
