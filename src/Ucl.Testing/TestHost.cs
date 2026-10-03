using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ucl.Core.Testing;

namespace Ucl.Testing;

/// <summary>
/// Runs the tests in a child process, the test host (docs/test.md, "Test host"), so that a test that ends the process
/// (an engine type's finalizer throwing on the GC thread, a stack overflow) cannot take the run with it. The host
/// writes every discovered case, each case as it starts and each result to a results file, one JSON line each,
/// flushed as written. When the host dies, the completed cases are kept, the case in flight is classified from the
/// host's error output, the crash is recorded, and a new host runs the rest.
/// </summary>
public static class TestHost
{
    /// <summary>The hidden command that runs a test host: <c>ucl __test-host &lt;request.json&gt;</c>.</summary>
    public const string Command = "__test-host";

    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Runs <paramref name="tests"/> in test hosts started by <paramref name="launch"/>.</summary>
    /// <param name="tests">The test assemblies.</param>
    /// <param name="images">Every project assembly image by name; written to a temporary folder for the host to load.</param>
    /// <param name="files">Plugin and editor DLLs by assembly simple name.</param>
    /// <param name="filter">The <c>--filter</c> pattern, or null.</param>
    /// <param name="editorCases">Audited Editor-owned names or assembly-qualified keys, classified before test execution.</param>
    /// <param name="launch">Starts a host with the given arguments (after the command name), waits, and returns its exit code and error output.</param>
    public static TestRun Run(
        IReadOnlyList<TestAssemblyImage> tests,
        IReadOnlyDictionary<string, byte[]> images,
        IReadOnlyDictionary<string, string> files,
        string? filter,
        Func<IReadOnlyList<string>, (int Exit, string Error)> launch,
        IReadOnlySet<string>? editorCases = null)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(launch);

        // Assemblies are loaded from files, never from memory: NUnit asks for an assembly's path. The folder is
        // private to this run and outside the project (the read-only contract), and is deleted afterwards.
        var folder = Path.Combine(Path.GetTempPath(), "ucl-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var paths = new Dictionary<string, string>(files, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, image) in images)
        {
            var path = Path.Combine(folder, name + ".dll");
            File.WriteAllBytes(path, image);
            paths[name] = path;
        }

        var requestPath = Path.Combine(folder, "request.json");
        var resultsPath = Path.Combine(folder, "results.jsonl");
        var done = new Dictionary<string, TestCaseResult>(StringComparer.Ordinal);
        var crashes = new List<TestHostCrash>();
        var discovered = new List<TestCaseResult>();
        string? last = null;
        try
        {
            while (true)
            {
                File.WriteAllText(requestPath, JsonSerializer.Serialize(new Request(tests, paths, filter, [.. done.Keys], resultsPath, editorCases is null ? [] : [.. editorCases.Order(StringComparer.Ordinal)]), Json));
                File.Delete(resultsPath);
                var (exit, error) = launch([requestPath]);

                var runDiscovered = new List<TestCaseResult>();
                (string Assembly, string FullName)? inFlight = null;
                var progress = false;
                int? total = null;
                foreach (var line in File.Exists(resultsPath) ? File.ReadAllLines(resultsPath) : [])
                {
                    if (TryParse(line) is not { } entry)
                    {
                        continue; // a line cut short by the crash
                    }

                    switch (entry.Event)
                    {
                        case "discovered":
                            runDiscovered.Add(entry.Case!);
                            break;
                        case "scanning":
                        case "started":
                            inFlight = (entry.Case!.Assembly, entry.Case.FullName);
                            break;
                        case "scanned":
                            inFlight = null;
                            break;
                        case "finished":
                            done[NUnitHost.Key(entry.Case!.Assembly, entry.Case.FullName)] = entry.Case;
                            last = entry.Case.FullName;
                            inFlight = null;
                            progress = true;
                            break;
                        case "done":
                            total = entry.Discovered;
                            break;
                        default:
                            break;
                    }
                }

                if (runDiscovered.Count > 0 || total is not null)
                {
                    discovered = runDiscovered;
                }

                if (exit == 0 && total is not null)
                {
                    break;
                }

                var text = error.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
                if (text.Length == 0)
                {
                    text = $"the test host exited with code {exit} and no error output";
                }

                crashes.Add(new TestHostCrash(last, inFlight?.FullName, text));
                if (inFlight is { } running && discovered.FirstOrDefault(c => c.Assembly == running.Assembly && c.FullName == running.FullName) is { } crashed)
                {
                    var (category, reason) = TestClassifier.FromHostCrash(text);
                    done[NUnitHost.Key(crashed.Assembly, crashed.FullName)] = crashed with { Category = category, Reason = reason };
                    last = crashed.FullName;
                    continue;
                }

                if (!progress)
                {
                    // The host dies before it reaches a case: what it never ran is reported, not lost.
                    foreach (var c in discovered.Where(c => !done.ContainsKey(NUnitHost.Key(c.Assembly, c.FullName))))
                    {
                        done[NUnitHost.Key(c.Assembly, c.FullName)] = c with { Category = TestCategory.Failed, Reason = "not run: the test host crashed before this case" };
                    }

                    break;
                }
            }
        }
        finally
        {
            NUnitHost.TryDelete(folder);
        }

        if (discovered.Any(c => !done.ContainsKey(NUnitHost.Key(c.Assembly, c.FullName))) || done.Count != discovered.Count)
        {
            throw new InvalidOperationException($"zero-loss accounting failed: {discovered.Count} cases discovered, {done.Count} classified");
        }

        return new TestRun(
            [.. done.Values.OrderBy(c => c.Assembly, StringComparer.Ordinal).ThenBy(c => c.FullName, StringComparer.Ordinal)],
            discovered.Count,
            crashes);
    }

    /// <summary>The test host: runs the request at <paramref name="requestPath"/>, writing the results file as it goes. Returns 0.</summary>
    public static int Serve(string requestPath)
    {
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(requestPath), Json)
            ?? throw new InvalidOperationException($"empty test host request: {requestPath}");
        var filter = request.Filter is null ? null : new Regex(request.Filter, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        using var writer = new StreamWriter(request.Results, append: false, new UTF8Encoding(false)) { AutoFlush = true };
        var events = new LineWriter(writer);
        var total = NUnitHost.Run(request.Tests, request.Paths, filter, request.Skip.ToHashSet(StringComparer.Ordinal), events, request.EditorCases.ToHashSet(StringComparer.Ordinal));
        events.Write(new Line("done", null, total));
        return 0;
    }

    private static Line? TryParse(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<Line>(line, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Request(
        IReadOnlyList<TestAssemblyImage> Tests,
        IReadOnlyDictionary<string, string> Paths,
        string? Filter,
        IReadOnlyList<string> Skip,
        string Results,
        IReadOnlyList<string> EditorCases);

    private sealed record Line(string Event, TestCaseResult? Case, int Discovered = 0);

    private sealed class LineWriter(StreamWriter writer) : ITestEvents
    {
        public void Write(Line line) => writer.WriteLine(JsonSerializer.Serialize(line, Json));

        public void Discovered(TestCaseResult testCase) => Write(new Line("discovered", testCase));

        public void Started(string assembly, string fullName) =>
            Write(new Line("started", new TestCaseResult(assembly, string.Empty, fullName, TestCategory.Skipped, string.Empty)));

        public void Scanning(string assembly, string fullName) =>
            Write(new Line("scanning", new TestCaseResult(assembly, string.Empty, fullName, TestCategory.Skipped, string.Empty)));

        public void Scanned(string assembly, string fullName) =>
            Write(new Line("scanned", new TestCaseResult(assembly, string.Empty, fullName, TestCategory.Skipped, string.Empty)));

        public void Finished(TestCaseResult testCase) => Write(new Line("finished", testCase));
    }
}
