using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ucl.Core.Model;
using Ucl.Core.Testing;

namespace Ucl.Reporting;

/// <summary><c>ucl test</c> output: text, JSON (schema <c>ucl-test/1</c>), JUnit XML and NUnit 3 XML. Deterministic.</summary>
public static class TestReport
{
    /// <summary>The JSON schema identifier.</summary>
    public const string Schema = "ucl-test/1";

    private static readonly TestCategory[] Order =
        [TestCategory.Passed, TestCategory.Failed, TestCategory.Skipped, TestCategory.Ignored, TestCategory.NeedsUnity, TestCategory.UnityOnly];

    /// <summary>Lowercase category name (JSON contract).</summary>
    public static string Name(TestCategory c) => c switch
    {
        TestCategory.Passed => "passed",
        TestCategory.Failed => "failed",
        TestCategory.Skipped => "skipped",
        TestCategory.Ignored => "ignored",
        TestCategory.NeedsUnity => "needs-unity",
        _ => "unity-only",
    };

    /// <summary>Unity negated-regex filter for cases already completed by this report's execution owners.</summary>
    public static string UnityFilter(TestRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Cases.All(c => c.Route is null)) return UnityTestFilter.Build(report.Cases);
        var complete = report.Cases.GroupBy(c => c.FullName, StringComparer.Ordinal)
            .Where(g => g.All(c => c.Route is "dotnet" or "host"
                && c.Category is not (TestCategory.NeedsUnity or TestCategory.UnityOnly)))
            .Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var filters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in report.Cases.GroupBy(c => c.ClassName, StringComparer.Ordinal))
        {
            var prefix = group.Key + ".";
            if (!prefix.Contains(';') && group.All(c => c.FullName.StartsWith(prefix, StringComparison.Ordinal) && complete.Contains(c.FullName))
                && report.Cases.Where(c => c.FullName.StartsWith(prefix, StringComparison.Ordinal)).All(c => complete.Contains(c.FullName)))
            {
                filters.Add("!^" + Regex.Escape(prefix));
                continue;
            }
            foreach (var name in group.Select(c => c.FullName).Where(complete.Contains).Where(n => !n.Contains(';')))
                filters.Add("!^" + Regex.Escape(name) + "\\z");
        }
        return string.Join(';', filters.Order(StringComparer.Ordinal));
    }
    /// <summary>Renders <paramref name="report"/> in <paramref name="format"/> (text, json, junit, nunit3).</summary>
    public static string Render(TestRunReport report, string format)
    {
        ArgumentNullException.ThrowIfNull(report);
        return format switch
        {
            "json" => Json(report),
            "junit" => JUnit(report),
            "nunit3" => NUnit3(report),
            _ => Text(report),
        };
    }

    /// <summary>Human-readable report: the compile result, every case that did not pass, counts.</summary>
    public static string Text(TestRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        foreach (var p in report.Problems)
        {
            sb.Append(TextReport.FormatProblem(p)).Append('\n');
        }

        if (report.Cell is { } cell)
        {
            sb.Append("== ").Append(cell.Label).Append(" (ucl test)\n");
        }

        if (report.Compile is { ExitCode: not 0 } compile)
        {
            foreach (var d in compile.Diagnostics.Where(d => d.Severity == Severity.Error))
            {
                sb.Append(TextReport.Format(d)).Append('\n');
            }

            sb.Append("test assemblies do not compile; no test ran\n");
        }

        foreach (var assembly in report.Cases.GroupBy(c => c.Assembly, StringComparer.Ordinal))
        {
            sb.Append(assembly.Key).Append(": ").Append(Counts(assembly.ToList())).Append('\n');
            foreach (var c in assembly.Where(c => c.Category != TestCategory.Passed || report.Timings))
            {
                sb.Append("  ").Append(Name(c.Category).PadRight(11)).Append(' ').Append(c.FullName);
                if (c.Reason.Length > 0)
                {
                    sb.Append(": ").Append(c.Reason);
                }

                if (report.Timings)
                {
                    sb.Append(" (").Append(c.DurationMs.ToString(CultureInfo.InvariantCulture)).Append(" ms)");
                }

                sb.Append('\n');
            }
        }

        if (report.Cases.Any(c => c.Route is not null))
            sb.Append("routing: ").Append(string.Join(", ", report.Cases.GroupBy(c => c.Route).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Count()} {g.Key}"))).Append('\n');

        var members = NeedsUnityMembers(report).ToArray();
        if (members.Length > 0)
        {
            sb.Append("needs-unity by member (top 20):\n");
            foreach (var (member, cases) in members)
                sb.Append("  ").Append(cases.ToString(CultureInfo.InvariantCulture)).Append("  ").Append(member).Append('\n');
        }

        foreach (var crash in report.HostCrashes)
        {
            sb.Append("test host crashed after ").Append(crash.After ?? "(no case)");
            if (crash.During is { } during)
            {
                sb.Append(", during ").Append(during);
            }

            sb.Append("; a new host ran the rest:\n");
            foreach (var line in crash.Text.Split('\n'))
            {
                sb.Append("    ").Append(line.TrimEnd()).Append('\n');
            }
        }

        sb.Append("result: ").Append(report.Cases.Count).Append(" cases: ").Append(Counts(report.Cases)).Append(", exit ").Append(report.ExitCode).Append('\n');
        return sb.ToString();
    }

    private static IEnumerable<(string Member, int Cases)> NeedsUnityMembers(TestRunReport report) =>
        report.Cases.Where(c => c.Category == TestCategory.NeedsUnity)
            .GroupBy(c => c.EngineMember ?? "(unknown)", StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(20)
            .Select(g => (g.Key, g.Count()));

    private static string Counts(IReadOnlyCollection<TestCaseResult> cases) =>
        string.Join(", ", Order.Select(o => $"{cases.Count(c => c.Category == o)} {Name(o)}"));

    /// <summary>JSON report, schema <c>ucl-test/1</c>.</summary>
    public static string Json(TestRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("schema", Schema);
            w.WriteStartObject("tool");
            w.WriteString("name", "ucl");
            w.WriteString("version", report.ToolVersion);
            w.WriteEndObject();
            w.WriteNumber("exitCode", report.ExitCode);
            w.WriteStartObject("summary");
            w.WriteNumber("cases", report.Cases.Count);
            foreach (var o in Order)
            {
                w.WriteNumber(o == TestCategory.NeedsUnity ? "needsUnity" : o == TestCategory.UnityOnly ? "unityOnly" : Name(o), report.Count(o));
            }

            w.WriteStartArray("needsUnityByMember");
            foreach (var (member, cases) in NeedsUnityMembers(report))
            {
                w.WriteStartObject();
                w.WriteString("engineMember", member);
                w.WriteNumber("cases", cases);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteStartArray("problems");
            foreach (var p in report.Problems)
            {
                w.WriteStartObject();
                w.WriteString("id", p.Id);
                w.WriteString("message", p.Message);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            if (report.Cell is { } cell)
            {
                w.WriteStartObject("cell");
                w.WriteString("unityVersion", cell.UnityVersion.ToString());
                w.WriteString("target", Names.Of(cell.Target));
                w.WriteString("platform", cell.Platform.ToString());
                w.WriteString("editorOs", Names.Of(cell.EditorOs));
                w.WriteEndObject();
            }

            if (report.Compile is { } compile)
            {
                w.WriteStartObject("compile");
                w.WriteNumber("exitCode", compile.ExitCode);
                w.WriteNumber("errors", compile.Diagnostics.Count(d => d.Severity == Severity.Error));
                w.WriteNumber("warnings", compile.Diagnostics.Count(d => d.Severity == Severity.Warning));
                w.WriteEndObject();
            }

            w.WriteStartArray("assemblies");
            foreach (var a in report.Assemblies)
            {
                w.WriteStringValue(a);
            }

            w.WriteEndArray();
            w.WriteStartArray("hostCrashes");
            foreach (var crash in report.HostCrashes)
            {
                w.WriteStartObject();
                w.WriteString("after", crash.After);
                w.WriteString("during", crash.During);
                w.WriteString("text", crash.Text);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("cases");
            foreach (var c in report.Cases)
            {
                w.WriteStartObject();
                w.WriteString("assembly", c.Assembly);
                w.WriteString("class", c.ClassName);
                w.WriteString("name", c.FullName);
                w.WriteString("category", Name(c.Category));
                if (c.Route is not null) w.WriteString("route", c.Route);
                if (c.Category == TestCategory.NeedsUnity) w.WriteString("engineMember", c.EngineMember);
                if (c.Reason.Length > 0)
                {
                    w.WriteString("reason", c.Reason);
                }

                if (report.Timings)
                {
                    w.WriteNumber("durationMs", c.DurationMs);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>JUnit XML: failed cases are failures; skipped, ignored, needs-unity and unity-only cases are skipped with the category in the message.</summary>
    public static string JUnit(TestRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var suites = new XElement("testsuites",
            new XAttribute("name", "ucl test"),
            new XAttribute("tests", report.Cases.Count),
            new XAttribute("failures", report.Count(TestCategory.Failed)),
            new XAttribute("skipped", report.Cases.Count(c => c.Category is not (TestCategory.Passed or TestCategory.Failed))));
        foreach (var assembly in report.Cases.GroupBy(c => c.Assembly, StringComparer.Ordinal))
        {
            var suite = new XElement("testsuite",
                new XAttribute("name", assembly.Key),
                new XAttribute("tests", assembly.Count()),
                new XAttribute("failures", assembly.Count(c => c.Category == TestCategory.Failed)),
                new XAttribute("skipped", assembly.Count(c => c.Category is not (TestCategory.Passed or TestCategory.Failed))));
            foreach (var c in assembly)
            {
                var testCase = new XElement("testcase", new XAttribute("classname", c.ClassName), new XAttribute("name", c.FullName));
                if (report.Timings)
                {
                    testCase.Add(new XAttribute("time", (c.DurationMs / 1000.0).ToString("0.000", CultureInfo.InvariantCulture)));
                }

                if (c.Category == TestCategory.Failed)
                {
                    testCase.Add(new XElement("failure", new XAttribute("message", c.Reason)));
                }
                else if (c.Category != TestCategory.Passed)
                {
                    testCase.Add(new XElement("skipped", new XAttribute("message", $"{Name(c.Category)}: {c.Reason}")));
                }

                suite.Add(testCase);
            }

            suites.Add(suite);
        }

        return Xml(suites);
    }

    /// <summary>NUnit 3 result XML (<c>test-run</c>, one <c>test-suite type="Assembly"</c> per assembly, <c>test-case</c> leaves).</summary>
    public static string NUnit3(TestRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var id = 1;
        var run = Counted(new XElement("test-run", new XAttribute("id", 0), new XAttribute("name", "ucl test")), report.Cases);
        if (report.ExitCode != 0) run.SetAttributeValue("result", "Failed");
        run.Add(new XAttribute("engine-version", report.ToolVersion));
        foreach (var assembly in report.Cases.GroupBy(c => c.Assembly, StringComparer.Ordinal))
        {
            var suite = Counted(new XElement("test-suite", new XAttribute("type", "Assembly"), new XAttribute("id", id++), new XAttribute("name", assembly.Key), new XAttribute("fullname", assembly.Key)), assembly.ToList());
            foreach (var c in assembly)
            {
                var (result, label) = c.Category switch
                {
                    TestCategory.Passed => ("Passed", null),
                    TestCategory.Failed => ("Failed", null),
                    TestCategory.Ignored => ("Skipped", "Ignored"),
                    TestCategory.Skipped => ("Skipped", null),
                    TestCategory.NeedsUnity => ("Skipped", "NeedsUnity"),
                    _ => ("Skipped", "UnityOnly"),
                };
                var testCase = new XElement("test-case",
                    new XAttribute("id", id++),
                    new XAttribute("name", c.FullName.StartsWith(c.ClassName + ".", StringComparison.Ordinal) ? c.FullName[(c.ClassName.Length + 1)..] : c.FullName),
                    new XAttribute("fullname", c.FullName),
                    new XAttribute("classname", c.ClassName),
                    new XAttribute("result", result));
                if (c.Route is not null)
                    testCase.Add(new XElement("properties", new XElement("property", new XAttribute("name", "ucl-route"), new XAttribute("value", c.Route))));

                if (label is not null)
                {
                    testCase.Add(new XAttribute("label", label));
                }

                if (report.Timings)
                {
                    testCase.Add(new XAttribute("duration", (c.DurationMs / 1000.0).ToString("0.000", CultureInfo.InvariantCulture)));
                }

                if (c.Reason.Length > 0)
                {
                    testCase.Add(new XElement(c.Category == TestCategory.Failed ? "failure" : "reason", new XElement("message", new XCData(c.Reason))));
                }

                suite.Add(testCase);
            }

            run.Add(suite);
        }

        return Xml(run);
    }

    private static XElement Counted(XElement e, IReadOnlyCollection<TestCaseResult> cases)
    {
        var failed = cases.Count(c => c.Category == TestCategory.Failed);
        e.Add(
            new XAttribute("testcasecount", cases.Count),
            new XAttribute("result", failed > 0 ? "Failed" : "Passed"),
            new XAttribute("total", cases.Count),
            new XAttribute("passed", cases.Count(c => c.Category == TestCategory.Passed)),
            new XAttribute("failed", failed),
            new XAttribute("inconclusive", 0),
            new XAttribute("skipped", cases.Count(c => c.Category is not (TestCategory.Passed or TestCategory.Failed))));
        return e;
    }

    private static string Xml(XElement root) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + root.ToString().Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
}
