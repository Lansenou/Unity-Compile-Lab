using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary><c>ucl test</c> on the fixtures that declare a <c>tests</c> expectation (fixtures/manifest.json).</summary>
public sealed class TestCommandTests
{
    private const string Fixture = "test-editmode";

    private static (string Project, TestEnvironment Env, ExpectedTestRun Expected) Setup(TempDir temp, string fixture = Fixture)
    {
        var entry = FixtureManifest.Load().Fixtures.Single(f => f.Name == fixture);
        var project = FixtureRunner.Prepare(entry, temp.Path);
        var home = Path.Combine(temp.Path, "home");
        Directory.CreateDirectory(home);
        return (project, new TestEnvironment(home), entry.Tests!);
    }

    private static (int Exit, string Stdout, string Stderr) Test(TestEnvironment env, string project, params string[] extra) =>
        Cli.Run(env, ["test", project, "--no-cache", "--editor-os", "linux", .. extra]);

    private static List<(string Assembly, string Name, string Category)> Cases(string json) =>
        [.. JsonDocument.Parse(json).RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => (c.GetProperty("assembly").GetString()!, c.GetProperty("name").GetString()!, c.GetProperty("category").GetString()!))];

    /// <summary>Every expected case is reported, in order, with its category: a case that disappears fails this test.</summary>
    [Fact]
    public void Every_case_is_reported_with_its_category()
    {
        using var temp = new TempDir();
        var (project, env, expected) = Setup(temp);
        var (exit, stdout, stderr) = Test(env, project, "--format", "json");
        Assert.True(exit == expected.ExitCode, stdout + stderr);
        Assert.Equal(expected.Cases.Select(c => (c.Assembly, c.Name, c.Category)), Cases(stdout));
    }

    /// <summary>Zero-loss accounting: the categories add up to the number of cases, and the counts match the fixture.</summary>
    [Fact]
    public void Categories_add_up_to_the_discovered_cases()
    {
        using var temp = new TempDir();
        var (project, env, expected) = Setup(temp);
        var summary = JsonDocument.Parse(Test(env, project, "--format", "json").Stdout).RootElement.GetProperty("summary");
        var total = summary.GetProperty("cases").GetInt32();
        Assert.Equal(expected.Cases.Count, total);
        Assert.Equal(total, new[] { "passed", "failed", "skipped", "ignored", "needsUnity", "unityOnly" }.Sum(k => summary.GetProperty(k).GetInt32()));
        Assert.Equal(19, summary.GetProperty("passed").GetInt32());
        Assert.Equal(1, summary.GetProperty("failed").GetInt32());
        Assert.Equal(2, summary.GetProperty("needsUnity").GetInt32());
        Assert.Equal(6, summary.GetProperty("unityOnly").GetInt32());

        // The cases behind #if UNITY_5_3_OR_NEWER, UNITY_EDITOR and NET_UNITY_4_8 are there, the #else branch is not.
        var names = expected.Cases.Select(c => c.Name).ToList();
        Assert.Contains("Game.Tests.DefineGuardedTests.Historical_version_symbol_is_defined", names);
        Assert.Contains("Game.Tests.DefineGuardedTests.Parameterised_case_behind_a_version_symbol(\"b\")", names);
        Assert.Contains("Game.Tests.DefineGuardedTests.Editor_assemblies_use_the_net_framework_profile", names);
        Assert.DoesNotContain("Game.Tests.DefineGuardedTests.Never_compiled_in_the_Editor", names);
    }

    [Fact]
    public void Emits_the_unity_filter_of_fully_passing_classes()
    {
        using var temp = new TempDir();
        var (project, env, expected) = Setup(temp);
        var file = Path.Combine(temp.Path, "out", "filter.txt");
        Test(env, project, "--emit-unity-filter", file);
        Assert.Equal(expected.UnityFilter + "\n", File.ReadAllText(file));
    }

    [Fact]
    public void Text_lists_what_did_not_pass_and_the_counts()
    {
        using var temp = new TempDir();
        var (project, env, _) = Setup(temp);
        var (exit, stdout, _) = Test(env, project);
        Assert.Equal(1, exit);
        Assert.Contains("Game.Tests.EditMode: 18 passed, 1 failed, 2 skipped, 1 ignored, 2 needs-unity, 4 unity-only\n", stdout, StringComparison.Ordinal);
        Assert.Contains("  needs-unity Game.Tests.EngineTests.Spawning_needs_the_engine: System.Security.SecurityException : ECall methods must be packaged into a system module.; engine member: UnityEngine.Native.Unavailable\n", stdout, StringComparison.Ordinal);
        Assert.Contains("  failed      Game.Tests.OutcomeTests.Arithmetic_is_wrong_on_purpose: a real failure, not an engine call\n", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Spending_less_than_the_balance_succeeds", stdout, StringComparison.Ordinal);
        Assert.EndsWith("result: 31 cases: 19 passed, 1 failed, 2 skipped, 1 ignored, 2 needs-unity, 6 unity-only, exit 1\n", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void JUnit_and_NUnit3_reports_count_the_same_cases()
    {
        using var temp = new TempDir();
        var (project, env, expected) = Setup(temp);
        var junit = XDocument.Parse(Test(env, project, "--format", "junit").Stdout).Root!;
        Assert.Equal(expected.Cases.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), junit.Attribute("tests")!.Value);
        Assert.Equal("1", junit.Attribute("failures")!.Value);
        Assert.Equal(expected.Cases.Count, junit.Descendants("testcase").Count());
        Assert.Single(junit.Descendants("failure"));

        var nunit = XDocument.Parse(Test(env, project, "--format", "nunit3").Stdout).Root!;
        Assert.Equal("Failed", nunit.Attribute("result")!.Value);
        Assert.Equal(expected.Cases.Count, nunit.Descendants("test-case").Count());
        Assert.Equal(3, nunit.Elements("test-suite").Count());
        Assert.Equal(2, nunit.Descendants("test-case").Count(c => (string?)c.Attribute("label") == "NeedsUnity"));
    }

    [Fact]
    public void Output_is_deterministic_and_a_warm_run_matches()
    {
        using var temp = new TempDir();
        var (project, env, _) = Setup(temp);
        var cache = Path.Combine(temp.Path, "cache");
        var cold = Cli.Run(env, "test", project, "--cache-dir", cache, "--editor-os", "linux", "--format", "json").Stdout;
        var warm = Cli.Run(env, "test", project, "--cache-dir", cache, "--editor-os", "linux", "--format", "json").Stdout;
        Assert.Equal(cold, warm);
        Assert.Equal(cold, Test(env, project, "--format", "json").Stdout);
    }

    [Fact]
    public void Filter_selects_cases_by_full_name()
    {
        using var temp = new TempDir();
        var (project, env, _) = Setup(temp);
        var (exit, stdout, _) = Test(env, project, "--format", "json", "--filter", @"WalletTests\.Balance");
        Assert.Equal(0, exit);
        Assert.Equal(3, Cases(stdout).Count);
        Assert.All(Cases(stdout), c => Assert.Equal("passed", c.Category));
        Assert.Equal(3, Test(env, project, "--filter", "(").Exit);
    }

    [Fact]
    public void A_test_assembly_that_does_not_compile_runs_nothing()
    {
        using var temp = new TempDir();
        var (project, env, _) = Setup(temp);
        File.AppendAllText(Path.Combine(project, "Assets", "Tests", "EditMode", "WalletTests.cs"), "\nclass Broken { int X() => nope; }\n");
        var (exit, stdout, _) = Test(env, project);
        Assert.Equal(1, exit);
        Assert.Contains("error CS0103", stdout, StringComparison.Ordinal);
        Assert.Contains("test assemblies do not compile; no test ran", stdout, StringComparison.Ordinal);
        Assert.Contains("result: 0 cases", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_without_test_assemblies_has_no_cases()
    {
        using var temp = new TempDir();
        var entry = FixtureManifest.Load().Fixtures.Single(f => f.Name == "basic-predefined");
        var project = FixtureRunner.Prepare(entry, temp.Path);
        var home = Path.Combine(temp.Path, "home");
        Directory.CreateDirectory(home);
        var (exit, stdout, _) = Test(new TestEnvironment(home), project);
        Assert.Equal(0, exit);
        Assert.EndsWith("result: 0 cases: 0 passed, 0 failed, 0 skipped, 0 ignored, 0 needs-unity, 0 unity-only, exit 0\n", stdout, StringComparison.Ordinal);
    }

    /// <summary>
    /// A finalizer that throws on the GC thread ends the test host process: the completed cases are kept, the case in
    /// flight is classified from the crash text, the rest run in a new host, and the crash is reported.
    /// </summary>
    [Fact]
    public void A_crashing_test_host_keeps_every_case_and_reports_the_crash()
    {
        using var temp = new TempDir();
        var (project, env, expected) = Setup(temp, "test-host-crash");
        var (exit, stdout, stderr) = Test(env, project, "--format", "json");
        Assert.True(exit == expected.ExitCode, stdout + stderr);
        Assert.Equal(expected.Cases.Select(c => (c.Assembly, c.Name, c.Category)), Cases(stdout));
        var crash = Assert.Single(JsonDocument.Parse(stdout).RootElement.GetProperty("hostCrashes").EnumerateArray());
        Assert.Equal("Game.Tests.RenderTests.C_pooled_command_buffer", crash.GetProperty("after").GetString());
        Assert.Equal("Game.Tests.RenderTests.D_collects_garbage", crash.GetProperty("during").GetString());
        Assert.Contains("at UnityEngine.Rendering.CommandBuffer.Finalize()", crash.GetProperty("text").GetString(), StringComparison.Ordinal);

        var text = Test(env, project).Stdout;
        Assert.Contains("test host crashed after Game.Tests.RenderTests.C_pooled_command_buffer", text, StringComparison.Ordinal);
        Assert.Contains("  needs-unity Game.Tests.RenderTests.B_creates_a_command_buffer: constructs UnityEngine.Rendering.CommandBuffer", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_native_member_is_named_through_a_helper_and_ranked()
    {
        using var temp = new TempDir();
        var (project, env, _) = Setup(temp);
        var (exit, stdout, stderr) = Test(env, project, "--format", "json", "--filter", "EngineTests.Spawning");
        Assert.True(exit == 0, stdout + stderr);
        using var json = JsonDocument.Parse(stdout);
        var test = Assert.Single(json.RootElement.GetProperty("cases").EnumerateArray());
        Assert.Equal("UnityEngine.Native.Unavailable", test.GetProperty("engineMember").GetString());
        Assert.Contains("UnityEngine.Native.Unavailable", test.GetProperty("reason").GetString(), StringComparison.Ordinal);
        var rank = Assert.Single(json.RootElement.GetProperty("summary").GetProperty("needsUnityByMember").EnumerateArray());
        Assert.Equal("UnityEngine.Native.Unavailable", rank.GetProperty("engineMember").GetString());
        Assert.Equal(1, rank.GetProperty("cases").GetInt32());
        var text = Test(env, project, "--filter", "EngineTests.Spawning").Stdout;
        Assert.Contains("needs-unity by member (top 20)", text, StringComparison.Ordinal);
        Assert.Contains("1  UnityEngine.Native.Unavailable", text, StringComparison.Ordinal);
    }


    [Fact]
    public void Native_member_ranking_is_bounded_and_ordered_by_case_count()
    {
        var cases = new List<Ucl.Core.Testing.TestCaseResult>();
        for (var member = 0; member < 25; member++)
            for (var test = 0; test <= member; test++)
                cases.Add(new("Probe", "Cases", $"Cases.Probe({member},{test})", Ucl.Core.Testing.TestCategory.NeedsUnity, "native call")
                { EngineMember = $"UnityEngine.Probe.Native{member:D2}" });
        var report = new Ucl.Core.Testing.TestRunReport { ToolVersion = "test", Cases = cases };
        using var json = JsonDocument.Parse(Ucl.Reporting.TestReport.Json(report));
        var ranks = json.RootElement.GetProperty("summary").GetProperty("needsUnityByMember").EnumerateArray().ToArray();
        Assert.Equal(20, ranks.Length);
        Assert.Equal("UnityEngine.Probe.Native24", ranks[0].GetProperty("engineMember").GetString());
        Assert.Equal(25, ranks[0].GetProperty("cases").GetInt32());
        Assert.Equal("UnityEngine.Probe.Native05", ranks[^1].GetProperty("engineMember").GetString());
        Assert.Equal(6, ranks[^1].GetProperty("cases").GetInt32());
    }

    [Fact]
    public void Relative_asset_paths_resolve_from_the_project_root()
    {
        using var temp = new TempDir();
        var (project, env, _) = Setup(temp);
        File.WriteAllText(Path.Combine(project, "Assets", "relative-probe.txt"), "project asset");
        File.WriteAllText(Path.Combine(project, "Assets", "Tests", "EditMode", "RelativePathTests.cs"), """
            using NUnit.Framework;
            public class RelativePathTests
            {
                [Test] public void Reads_asset() => Assert.AreEqual("project asset", System.IO.File.ReadAllText("Assets/relative-probe.txt"));
            }
            """);
        var parentDirectory = Directory.GetCurrentDirectory();
        var (exit, stdout, stderr) = Test(env, project, "--format", "json", "--filter", "RelativePathTests");
        Assert.True(exit == 0, stdout + stderr);
        Assert.Equal("passed", Assert.Single(JsonDocument.Parse(stdout).RootElement.GetProperty("cases").EnumerateArray()).GetProperty("category").GetString());
        Assert.Equal(parentDirectory, Directory.GetCurrentDirectory());
    }

}
