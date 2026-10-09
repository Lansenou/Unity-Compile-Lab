using System.Xml.Linq;
using System.Reflection;
using Ucl.Compilation;
using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Rules;
using Ucl.Discovery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Ucl.Cli;
using Ucl.Core.Testing;
using Ucl.Reporting;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>Optional host flags and reports preserve gate attribution and real failures.</summary>
public sealed class PlayerHostContractTests
{
    [Theory]
    [InlineData("check", "--host")]
    [InlineData("test", "--nographics")]
    [InlineData("test", "--editor-cases", "audit.txt")]
    public void Host_only_flags_refuse_other_commands(params string[] args) => Assert.False(ArgParser.Parse(args).Ok);

    [Fact]
    public void Host_failures_are_red_and_xml_keeps_routing_and_exclusion_reasons()
    {
        var report = new TestRunReport
        {
            ToolVersion = "test",
            Cases =
            [
                new("Example.Tests", "Example.BufferTests", "Example.BufferTests.Failed", TestCategory.Failed, "real failure") { Route = "host" },
                new("Example.Tests", "Example.SourceTests", "Example.SourceTests.NeedsEditor", TestCategory.NeedsUnity, "Application.dataPath") { Route = "needs-editor" },
            ],
        };
        Assert.Equal(1, report.ExitCode);
        var xml = XDocument.Parse(TestReport.NUnit3(report));
        Assert.Equal("1", xml.Root!.Attribute("failed")!.Value);
        Assert.Equal("2", xml.Root.Attribute("total")!.Value);
        var cases = xml.Descendants("test-case").ToArray();
        Assert.Equal("Failed", cases[0].Attribute("result")!.Value);
        Assert.Equal("host", cases[0].Descendants("property").Single().Attribute("value")!.Value);
        Assert.Equal("Application.dataPath", cases[1].Descendants("message").Single().Value);
        Assert.Contains("needs-editor", TestReport.Json(report), StringComparison.Ordinal);
    }

    [Fact]
    public void Configuration_failures_never_emit_a_green_nunit_root()
    {
        var report = new TestRunReport { ToolVersion = "test", Problems = [new Ucl.Core.Model.Problem("UCL3001", "unsupported host")] };
        var xml = XDocument.Parse(TestReport.NUnit3(report));
        Assert.Equal("Failed", xml.Root!.Attribute("result")!.Value);
        Assert.Equal(3, report.ExitCode);
    }

    [Fact]
    public void Unsupported_host_adapter_fails_clearly_without_executing_tests()
    {
        using var temp = new TempDir();
        var fixture = FixtureManifest.Load().Fixtures.Single(f => f.Name == "test-editmode");
        var project = FixtureRunner.Prepare(fixture, temp.Path);
        var env = new TestEnvironment(Path.Combine(temp.Path, "home"));
        var result = Cli.Run(env, ["test", project, "--host", "--format", "json", "--cache-dir", Path.Combine(temp.Path, "cache")]);
        Assert.Equal(3, result.Exit);
        Assert.Contains("Player host:", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\": \"Game.Tests.", result.Stdout, StringComparison.Ordinal);
    }
    [Fact]
    public void Editor_audit_cannot_hide_an_existing_managed_failure()
    {
        using var temp = new TempDir();
        var audit = Path.Combine(temp.Path, "audit.txt");
        File.WriteAllText(audit, "Example.Tests.Failure\n");
        var report = new TestRunReport
        {
            ToolVersion = "test",
            Cases =
            [new("Example", "Example.Tests", "Example.Tests.Failure", TestCategory.Failed, "assertion failed")]
        };
        var type = typeof(CliOptions).Assembly.GetType("Ucl.Cli.ProjectPlayerTest", throwOnError: true)!;
        var routed = (TestRunReport)type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!.Invoke(null,
            [null, null, null, new CliOptions { EditorCases = audit }, report, TextWriter.Null, null])!;
        Assert.Equal(1, routed.ExitCode);
        Assert.Equal(TestCategory.Failed, Assert.Single(routed.Cases).Category);
        Assert.Equal("assertion failed", routed.Cases[0].Reason);
        Assert.Equal("dotnet", routed.Cases[0].Route);
    }

    [Fact]
    public void Compiler_cache_changes_with_cell_defines_options_and_external_config()
    {
        using var temp = new TempDir();
        var config = Path.Combine(temp.Path, "config.globalconfig");
        File.WriteAllText(config, "is_global = true");
        var project = new ProjectContext { Root = temp.Path };
        var defines = new DefineSet();
        var plan = new AssemblyPlan { Name = "Example", Kind = AssemblyKind.Asmdef, Defines = defines, AnalyzerConfigs = [config] };
        var graph = new AssemblyGraph
        {
            Cell = new CompileCell(UnityVersion.Parse("6000.3.19f1").Value!, TargetKind.Editor,
            BuildPlatform.StandaloneWindows64, null, false, HostOs.Windows),
            BaseDefines = defines,
            Assemblies = [plan]
        };
        var tests = new HashSet<string>(StringComparer.Ordinal) { "Example" };
        var original = PlayerTestCompiler.Key(graph, project, tests, false);
        defines.Add("DIFFERENT", "test");
        var changedDefine = PlayerTestCompiler.Key(graph, project, tests, false);
        Assert.NotEqual(original, changedDefine);
        Assert.NotEqual(changedDefine, PlayerTestCompiler.Key(graph with { Assemblies = [plan with { Nullable = "enable" }] }, project, tests, false));
        Assert.NotEqual(changedDefine, PlayerTestCompiler.Key(graph with { Cell = graph.Cell with { Development = true } }, project, tests, false));
        File.WriteAllText(config, "is_global = true\nbuild_property.changed = true");
        Assert.NotEqual(changedDefine, PlayerTestCompiler.Key(graph, project, tests, false));
    }

    [Fact]
    public void Unsupported_generators_exclude_whole_assembly_before_emission()
    {
        using var temp = new TempDir();
        var managed = Path.Combine(temp.Path, "managed");
        Directory.CreateDirectory(managed);
        var source = Path.Combine(temp.Path, "Example.cs");
        File.WriteAllText(source, "public class Example { public GeneratedType Generated; }");
        var defines = new DefineSet();
        var plan = new AssemblyPlan
        {
            Name = "Example",
            Kind = AssemblyKind.Asmdef,
            Defines = defines,
            Sources = [source],
            Analyzers = ["Generator.dll"]
        };
        var graph = new AssemblyGraph
        {
            Cell = new CompileCell(UnityVersion.Parse("6000.3.19f1").Value!, TargetKind.Editor,
            BuildPlatform.StandaloneWindows64, null, false, HostOs.Windows),
            BaseDefines = defines,
            Assemblies = [plan]
        };
        var output = Path.Combine(temp.Path, "output");
        var reasons = PlayerTestCompiler.Compile(graph, new ProjectContext { Root = temp.Path }, managed, output,
            new HashSet<string>(StringComparer.Ordinal) { "Example" });
        Assert.Contains("generators", reasons[source], StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(output, "Example.dll")));
    }

    [Fact]
    public void Mono_host_build_disables_burst_only_in_the_build_process_arguments()
    {
        var type = typeof(CliOptions).Assembly.GetType("Ucl.Cli.ProjectPlayerCache", throwOnError: true)!;
        var arguments = (string[])type.GetMethod("BuildArguments", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, ["scratch", "player", "build"])!;
        Assert.Contains("--burst-disable-compilation", arguments);
        Assert.Equal("scratch", arguments[Array.IndexOf(arguments, "-projectPath") + 1]);
        Assert.Equal(Path.Combine("player", "Host.exe"), arguments[Array.IndexOf(arguments, "-playerOutput") + 1]);
    }

    [Fact]
    public void Scratch_layout_preserves_inherited_config_glob_semantics()
    {
        using var temp = new TempDir();
        var input = Path.Combine(temp.Path, "AppProject");
        var cache = Path.Combine(temp.Path, "cache");
        var type = typeof(CliOptions).Assembly.GetType("Ucl.Cli.ProjectPlayerCache", throwOnError: true)!;
        var scratch = (string)type.GetMethod("ScratchRoot", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [cache, input])!;
        Assert.Equal("AppProject", Path.GetFileName(scratch));
        var configText = "root = true\n[AppProject/Assets/*.cs]\ndotnet_diagnostic.CS0168.severity = error\n";
        var original = AnalyzerConfigSet.Create(new[] { AnalyzerConfig.Parse(configText, Path.Combine(temp.Path, ".editorconfig")) });
        var rebased = AnalyzerConfigSet.Create(new[] { AnalyzerConfig.Parse(configText, Path.Combine(Directory.GetParent(scratch)!.FullName, ".editorconfig")) });
        var expected = original.GetOptionsForSourcePath(Path.Combine(input, "Assets", "Example.cs")).TreeOptions["CS0168"];
        var actual = rebased.GetOptionsForSourcePath(Path.Combine(scratch, "Assets", "Example.cs")).TreeOptions["CS0168"];
        Assert.Equal(ReportDiagnostic.Error, expected);
        Assert.Equal(expected, actual);
    }


    [Fact]
    public void Identical_project_trees_with_different_config_path_semantics_have_distinct_player_keys()
    {
        using var temp = new TempDir();
        var first = Path.Combine(temp.Path, "First", "AppProject");
        var renamedProject = Path.Combine(temp.Path, "First", "OtherProject");
        var renamedAncestor = Path.Combine(temp.Path, "Second", "AppProject");
        foreach (var project in new[] { first, renamedProject, renamedAncestor })
            TempDir.Copy(Path.Combine(Repo.Fixtures, "basic-predefined"), project);
        var config = Path.Combine(temp.Path, ".editorconfig");
        var configText = "root = true\n[First/AppProject/Assets/*.cs]\ndotnet_diagnostic.CS0168.severity = error\n";
        File.WriteAllText(config, configText);
        var configs = AnalyzerConfigSet.Create(new[] { AnalyzerConfig.Parse(configText, config) });
        Assert.Equal(ReportDiagnostic.Error, configs.GetOptionsForSourcePath(Path.Combine(first, "Assets", "Example.cs")).TreeOptions["CS0168"]);
        Assert.False(configs.GetOptionsForSourcePath(Path.Combine(renamedProject, "Assets", "Example.cs")).TreeOptions.ContainsKey("CS0168"));
        Assert.False(configs.GetOptionsForSourcePath(Path.Combine(renamedAncestor, "Assets", "Example.cs")).TreeOptions.ContainsKey("CS0168"));
        Assert.Equal(PlayerHostCache.TreeDigest(first), PlayerHostCache.TreeDigest(renamedProject));
        Assert.Equal(PlayerHostCache.TreeDigest(first), PlayerHostCache.TreeDigest(renamedAncestor));
        var editorRoot = Path.Combine(temp.Path, "editor");
        var data = Path.Combine(editorRoot, "Data");
        Directory.CreateDirectory(Path.Combine(data, "Managed"));
        File.WriteAllText(Path.Combine(editorRoot, "Unity.exe"), "fixture editor revision");
        var editor = new EditorInstall { Root = editorRoot, DataPath = data, Version = UnityVersion.Parse("6000.3.19f1").Value! };
        var sessionType = typeof(CliOptions).Assembly.GetType("Ucl.Cli.Session", throwOnError: true)!;
        var cacheType = typeof(CliOptions).Assembly.GetType("Ucl.Cli.ProjectPlayerCache", throwOnError: true)!;
        string Key(string project)
        {
            var session = sessionType.GetMethod("Open", BindingFlags.Public | BindingFlags.Static)!.Invoke(null,
                [new CliOptions { Project = project, CacheDir = Path.Combine(temp.Path, "compile-cache") }, new TestEnvironment(temp.Path), false]);
            return (string)cacheType.GetMethod("Key", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [session, editor])!;
        }
        Assert.NotEqual(Key(first), Key(renamedProject));
        Assert.NotEqual(Key(first), Key(renamedAncestor));
    }


    [Fact]
    public void Source_ownership_shares_one_fresh_snapshot_without_changing_whole_file_exclusions()
    {
        using var temp = new TempDir();
        var project = new ProjectContext { Root = temp.Path };
        var source = Path.Combine(temp.Path, "Engine.cs");
        var editorSource = Path.Combine(temp.Path, "Editor.cs");
        var portableSource = Path.Combine(temp.Path, "Portable.cs");
        var texts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [source] = "class First {} class Second {} // Application.dataPath",
            [editorSource] = "class Third {}",
            [portableSource] = "class Fourth {}"
        };
        var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        string Read(string path) { reads[path] = reads.GetValueOrDefault(path) + 1; return texts[path]; }
        var defines = new DefineSet();
        var graph = new AssemblyGraph
        {
            Cell = new CompileCell(UnityVersion.Parse("6000.3.19f1").Value!, TargetKind.Editor,
                BuildPlatform.StandaloneWindows64, null, false, HostOs.Windows),
            BaseDefines = defines,
            Assemblies = [new AssemblyPlan { Name = "Example", Kind = AssemblyKind.Asmdef,
                Defines = defines, Sources = [source, editorSource, portableSource] }]
        };
        TestCaseResult Case(string name, string method = "Test") => new("Example", "Example." + name, "Example." + name + "." + method, TestCategory.NeedsUnity, string.Empty);
        var cases = new[] { Case("First"), Case("First", "Other"), Case("Second"), Case("Third"), Case("Fourth") };
        var exclusions = new Dictionary<string, string> { [source] = "Application.dataPath source context", [editorSource] = "CS0234: Editor API" };
        var type = typeof(CliOptions).Assembly.GetType("Ucl.Cli.ProjectPlayerTest", throwOnError: true)!;
        Dictionary<(string Assembly, string Class), string> Reasons() =>
            (Dictionary<(string Assembly, string Class), string>)type.GetMethod("SourceReasons", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [graph, project, cases, exclusions, (Func<string, string>)Read])!;
        var first = Reasons();
        Assert.Contains("Application.dataPath", first[("Example", "Example.First")], StringComparison.Ordinal);
        Assert.Contains("Application.dataPath", first[("Example", "Example.Second")], StringComparison.Ordinal);
        Assert.Contains("CS0234", first[("Example", "Example.Third")], StringComparison.Ordinal);
        Assert.False(first.ContainsKey(("Example", "Example.Fourth")));
        Assert.Equal(2, reads.Count);
        Assert.All(reads.Values, count => Assert.Equal(1, count));
        exclusions.Remove(source);
        var second = Reasons();
        Assert.False(second.ContainsKey(("Example", "Example.First")));
        Assert.False(second.ContainsKey(("Example", "Example.Second")));
        Assert.Contains("CS0234", second[("Example", "Example.Third")], StringComparison.Ordinal);
        Assert.Equal(1, reads[source]);
        Assert.Equal(2, reads[editorSource]);
    }

    [Fact]
    public void Player_runs_engine_unity_test_and_play_mode_cases_but_not_platform_cases()
    {
        var type = typeof(CliOptions).Assembly.GetType("Ucl.Cli.ProjectPlayerTest", throwOnError: true)!;
        bool Candidate(TestCategory category, string reason, bool platform = false) => (bool)type.GetMethod("PlayerCandidate", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [new TestCaseResult("A", "A.C", "A.C.T", category, reason) { EditorPlatform = platform }])!;
        string Attribute(string name) => TestClassifier.UnityOnlyAttributes["UnityEngine.TestTools." + name];
        Assert.True(Candidate(TestCategory.NeedsUnity, "engine call"));
        Assert.True(Candidate(TestCategory.UnityOnly, Attribute("UnityTestAttribute")));
        Assert.True(Candidate(TestCategory.UnityOnly, Attribute("RequiresPlayModeAttribute")));
        Assert.True(Candidate(TestCategory.UnityOnly, TestClassifier.UnityOnlyReason(true, [])!));
        Assert.False(Candidate(TestCategory.UnityOnly, Attribute("UnityPlatformAttribute"), platform: true));
        Assert.False(Candidate(TestCategory.UnityOnly, TestClassifier.UnityOnlyReason(true, [])!, platform: true));
        Assert.False(Candidate(TestCategory.NeedsUnity, "IL scan failure", platform: true));
        Assert.False(Candidate(TestCategory.Passed, string.Empty));
    }

    [Fact]
    public void Player_test_assemblies_are_staged_under_Library_ucl_like_the_Editor_script_assemblies()
    {
        using var temp = new TempDir();
        var dlls = Path.Combine(temp.Path, "cache", "tests-key");
        Directory.CreateDirectory(dlls);
        File.WriteAllText(Path.Combine(dlls, "Example.Tests.dll"), "tests");
        File.WriteAllText(Path.Combine(dlls, "Example.dll"), "runtime");
        File.WriteAllText(Path.Combine(dlls, "compile-exclusions.json"), "{}");
        var project = Path.Combine(temp.Path, "Project");
        var staged = (string)typeof(CliOptions).Assembly.GetType("Ucl.Cli.ProjectPlayerTest", throwOnError: true)!
            .GetMethod("StageAssemblies", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [dlls, project])!;
        Assert.Equal(Path.Combine(project, "Library", "ucl"), Path.GetDirectoryName(staged));
        Assert.Equal(["Example.Tests.dll", "Example.dll"], Directory.GetFiles(staged).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal("tests", File.ReadAllText(Path.Combine(staged, "Example.Tests.dll")));
    }

    [Fact]
    public void A_player_that_dies_keeps_its_streamed_results_fails_the_case_in_flight_and_reports_the_rest_not_run()
    {
        using var temp = new TempDir();
        var resultsFile = Path.Combine(temp.Path, "results.json");
        File.WriteAllText(resultsFile + ".jsonl", """
            {"assembly":"A.Tests","name":"A.C.First","outcome":"Passed","message":"","seconds":0.5}
            {"assembly":"A.Tests","name":"A.C.Second","outcome":"Failed","message":"real failure","seconds":1.0}

            """);
        File.WriteAllText(resultsFile + ".started", "A.Tests\nA.C.Third");
        TestCaseResult Case(string name) => new("A.Tests", "A.C", "A.C." + name, TestCategory.NeedsUnity, "engine call");
        var results = new Dictionary<(string Assembly, string Name), System.Text.Json.JsonElement>();
        var crash = (TestHostCrash?)typeof(CliOptions).Assembly.GetType("Ucl.Cli.ProjectPlayerTest", throwOnError: true)!
            .GetMethod("Salvage", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [resultsFile, "exited -1", new[] { Case("First"), Case("Second"), Case("Third"), Case("Fourth") }, results]);
        string Outcome(string name) => results[("A.Tests", "A.C." + name)].GetProperty("outcome").GetString()!;
        string Message(string name) => results[("A.Tests", "A.C." + name)].GetProperty("message").GetString()!;
        Assert.Equal(["Passed", "Failed", "Failed", "Failed"], new[] { "First", "Second", "Third", "Fourth" }.Select(Outcome));
        Assert.Equal("real failure", Message("Second"));
        Assert.Contains("during this case", Message("Third"), StringComparison.Ordinal);
        Assert.Contains("not run", Message("Fourth"), StringComparison.Ordinal);
        Assert.NotNull(crash);
        Assert.Equal("A.C.Second", crash.After);
        Assert.Equal("A.C.Third", crash.During);
        Assert.Contains("-1", crash.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_torn_last_stream_record_keeps_the_complete_ones_and_the_case_in_flight_is_matched_by_assembly()
    {
        using var temp = new TempDir();
        var resultsFile = Path.Combine(temp.Path, "results.json");
        File.WriteAllText(resultsFile + ".jsonl", """
            {"assembly":"A.Tests","name":"A.C.First","outcome":"Passed","message":"","seconds":0.5}
            {"assembly":"A.Tests","name":"A.C.Second","outcome":"Failed","message":"real failure","seconds":1.0}
            {"assembly":"A.Tests","name":"A.C.Thi
            """);
        File.WriteAllText(resultsFile + ".started", "A.Tests\nA.C.Third");
        TestCaseResult[] selected =
        [
            Case("A.Tests", "First"), Case("A.Tests", "Second"), Case("A.Tests", "Third"), Case("B.Tests", "Third"),
        ];
        var results = new Dictionary<(string Assembly, string Name), System.Text.Json.JsonElement>();
        var crash = (TestHostCrash?)PlayerTest("Salvage").Invoke(null, [resultsFile, "exited -1", selected, results]);
        Assert.Equal(["Passed", "Failed", "Failed", "Failed"], selected.Select(c => results[(c.Assembly, c.FullName)].GetProperty("outcome").GetString()));
        Assert.Contains("during this case", results[("A.Tests", "A.C.Third")].GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("not run", results[("B.Tests", "A.C.Third")].GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.NotNull(crash);
        Assert.Equal("A.C.Second", crash.After);
        Assert.Equal("A.C.Third", crash.During);
    }

    [Fact]
    public void A_torn_results_json_falls_back_to_the_streamed_results()
    {
        using var temp = new TempDir();
        var resultsFile = Path.Combine(temp.Path, "results.json");
        File.WriteAllText(resultsFile, """{"firstTestUnixMs":1,"finishedUnixMs":2,"fatal":"","tests":[{"assembly":"A.Tests","na""");
        File.WriteAllText(resultsFile + ".jsonl", """
            {"assembly":"A.Tests","name":"A.C.First","outcome":"Passed","message":"","seconds":0.5}

            """);
        File.WriteAllText(resultsFile + ".started", "A.Tests\nA.C.First");
        TestCaseResult[] selected = [Case("A.Tests", "First"), Case("A.Tests", "Second")];
        var results = new Dictionary<(string Assembly, string Name), System.Text.Json.JsonElement>();
        var crash = (TestHostCrash?)PlayerTest("Collect").Invoke(null, [resultsFile, 0, "exited 0", 0L, 0L, null, selected, results]);
        Assert.NotNull(crash);
        Assert.Null(crash.During);
        Assert.Contains("results.json", crash.Text, StringComparison.Ordinal);
        Assert.Equal("Passed", results[("A.Tests", "A.C.First")].GetProperty("outcome").GetString());
        Assert.Contains("not run", results[("A.Tests", "A.C.Second")].GetProperty("message").GetString(), StringComparison.Ordinal);

        File.WriteAllText(resultsFile, """{"firstTestUnixMs":1,"finishedUnixMs":2,"fatal":"","tests":[{"assembly":"A.Tests","name":"A.C.First","outcome":"Passed","message":"","seconds":0.5}]}""");
        results.Clear();
        Assert.Null(PlayerTest("Collect").Invoke(null, [resultsFile, 0, "exited 0", 0L, 0L, null, selected, results]));
        Assert.Equal([("A.Tests", "A.C.First")], results.Keys);
    }

    [Fact]
    public void The_player_bootstrap_writes_its_final_results_atomically_and_names_the_case_in_flight_with_its_assembly()
    {
        var source = BootstrapSource();
        Assert.Contains("File.WriteAllText(output + \".tmp\", JsonUtility.ToJson(report, true));", source, StringComparison.Ordinal);
        Assert.Contains("File.Move(output + \".tmp\", output);", source, StringComparison.Ordinal);
        Assert.Contains("File.WriteAllText(output + \".started\", AssemblyName(test) + \"\\n\" + test.FullName);", source, StringComparison.Ordinal);
    }

    private static TestCaseResult Case(string assembly, string name) => new(assembly, "A.C", "A.C." + name, TestCategory.NeedsUnity, "engine call");

    private static MethodInfo PlayerTest(string method) => typeof(CliOptions).Assembly.GetType("Ucl.Cli.ProjectPlayerTest", throwOnError: true)!
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string BootstrapSource()
    {
        using var stream = typeof(CliOptions).Assembly.GetManifestResourceStream("Ucl.Cli.PlayerHost.ProjectTestHost.cs")!;
        return new StreamReader(stream).ReadToEnd();
    }

    [Fact]
    public void Application_dataPath_reads_compile_to_the_input_project_Assets_tree_without_rewriting_sources()
    {
        using var temp = new TempDir();
        var managed = Path.Combine(temp.Path, "managed");
        Directory.CreateDirectory(managed);
        File.Copy(typeof(object).Assembly.Location, Path.Combine(managed, "System.Private.CoreLib.dll"));
        var texts = new Dictionary<string, string>
        {
            ["Engine.cs"] = "namespace UnityEngine { public static class Application { public static string dataPath => \"player\"; } }",
            ["Base.cs"] = "using App = UnityEngine.Application; namespace Example { public class BaseFixture { protected string Root => App.dataPath; } }",
            ["Derived.cs"] = "namespace Example { public class DerivedFixture : BaseFixture { public string Read() => Root; } }",
            ["Caller.cs"] = "namespace Example { public class CallerFixture { public BaseFixture Create() => new BaseFixture(); } }",
            ["Portable.cs"] = "namespace Example { public class PortableFixture { public string Read() => \"Application.dataPath\"; } } // Application.dataPath",
            ["Inactive.cs"] = "#if NEVER\nclass Hidden { string Root => UnityEngine.Application.dataPath; }\n#endif\nnamespace Example { public class InactiveFixture {} }",
            ["Names.cs"] = "using static UnityEngine.Application; namespace Example { public class NamesFixture { public string Read() => nameof(UnityEngine.Application.dataPath) + nameof(dataPath) + \"|\" + UnityEngine.Application.dataPath + \"|\" + dataPath; } }",
            ["Unrelated.cs"] = "namespace Other { public static class Application { public static string dataPath => \"portable\"; } public class UnrelatedFixture { public string Read() => Application.dataPath; } }"
        };
        foreach (var (name, source) in texts) File.WriteAllText(Path.Combine(temp.Path, name), source);
        var defines = new DefineSet();
        var graph = new AssemblyGraph
        {
            Cell = new CompileCell(UnityVersion.Parse("6000.3.19f1").Value!, TargetKind.Editor,
                BuildPlatform.StandaloneWindows64, null, false, HostOs.Windows),
            BaseDefines = defines,
            Assemblies = [new AssemblyPlan { Name = "Example", Kind = AssemblyKind.Asmdef,
                Defines = defines, Sources = texts.Keys.Select(n => Path.Combine(temp.Path, n)).ToList() }]
        };
        var output = Path.Combine(temp.Path, "output");
        var reasons = PlayerTestCompiler.Compile(graph, new ProjectContext { Root = temp.Path }, managed, output,
            new HashSet<string>(StringComparer.Ordinal) { "Example" }, analyzers: false);
        Assert.Empty(reasons);
        var context = new System.Runtime.Loader.AssemblyLoadContext("datapath", isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(new MemoryStream(File.ReadAllBytes(Path.Combine(output, "Example.dll"))));
            string Read(string type) => (string)assembly.GetType(type, throwOnError: true)!.GetMethod("Read")!.Invoke(Activator.CreateInstance(assembly.GetType(type)!), null)!;
            Assert.Equal(temp.Path.Replace('\\', '/') + "/Assets", Read("Example.DerivedFixture"));
            var assets = temp.Path.Replace('\\', '/') + "/Assets";
            Assert.Equal("dataPathdataPath|" + assets + "|" + assets, Read("Example.NamesFixture"));
            Assert.Equal("Application.dataPath", Read("Example.PortableFixture"));
            Assert.Equal("portable", Read("Other.UnrelatedFixture"));
        }
        finally { context.Unload(); }
        foreach (var (name, source) in texts) Assert.Equal(source, File.ReadAllText(Path.Combine(temp.Path, name)));
    }

    [Fact]
    public void Editor_filter_excludes_completed_mixed_cases_and_retains_failures_and_ownership_collisions()
    {
        TestCaseResult Case(string assembly, string name, TestCategory category, string? route) =>
            new(assembly, "Example.Mixed", "Example.Mixed." + name, category, "retained reason") { Route = route };
        var report = new TestRunReport
        {
            ToolVersion = "test",
            Cases =
            [
                Case("First", "Complete(\"a.b\")", TestCategory.Passed, "host"),
                Case("First", "Failure", TestCategory.Failed, "dotnet"),
                Case("First", "Ignored", TestCategory.Ignored, "host"),
                Case("First", "Pending", TestCategory.NeedsUnity, "needs-editor"),
                Case("First", "Collision", TestCategory.Passed, "host"),
                Case("Second", "Collision", TestCategory.NeedsUnity, "needs-editor"),
                Case("First", "Unknown", TestCategory.Passed, null),
                new("First", "Example.Complete", "Example.Complete.Skipped", TestCategory.Skipped, "skip") { Route = "host" },
                new("First", "Example.Complete", "Example.Complete.Failed", TestCategory.Failed, "failure") { Route = "host" },
                new("Second", "Example.Complete.Nested", "Example.Complete.Nested.Pending", TestCategory.NeedsUnity, "pending") { Route = "needs-editor" },
                new("First", "Example.Finished", "Example.Finished.Done", TestCategory.Skipped, "skip") { Route = "host" },
                Case("First", "Delimiter(\"a;b\")", TestCategory.Passed, "host"),
                Case("First", "Tail", TestCategory.Passed, "host"),
                Case("Second", "Tail\n", TestCategory.NeedsUnity, "needs-editor")
            ]
        };
        var before = TestReport.NUnit3(report);
        var filter = TestReport.UnityFilter(report);
        bool Included(string name) => !filter.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Any(p => System.Text.RegularExpressions.Regex.IsMatch(name, p[1..]));
        Assert.False(Included(report.Cases[0].FullName));
        Assert.True(Included("Example.Mixed.Complete(\"axb\")"));
        Assert.False(Included(report.Cases[1].FullName));
        Assert.False(Included(report.Cases[2].FullName));
        Assert.True(Included(report.Cases[3].FullName));
        Assert.True(Included(report.Cases[4].FullName));
        Assert.True(Included(report.Cases[6].FullName));
        Assert.False(Included(report.Cases[7].FullName));
        Assert.False(Included(report.Cases[8].FullName));
        Assert.True(Included(report.Cases[9].FullName));
        Assert.Contains("!^Example\\.Finished\\.", filter, StringComparison.Ordinal);
        Assert.True(Included(report.Cases[11].FullName));
        Assert.False(Included(report.Cases[12].FullName));
        Assert.True(Included(report.Cases[13].FullName));
        Assert.Equal(filter, TestReport.UnityFilter(report with { Cases = report.Cases.Reverse().ToList() }));
        Assert.Equal(before, TestReport.NUnit3(report));
        Assert.Equal(1, report.ExitCode);
        var legacy = report with { Cases = report.Cases.Select(c => c with { Route = null }).ToList() };
        Assert.Equal(UnityTestFilter.Build(legacy.Cases), TestReport.UnityFilter(legacy));
    }

    /// <summary>The Editor test list names every case ucl did not complete, once, in discovery order; no regex, no length limit.</summary>
    [Fact]
    public void Editor_test_list_names_every_pending_case_once_and_keeps_collisions()
    {
        TestCaseResult Case(string assembly, string name, TestCategory category, string? route) =>
            new(assembly, "Example.Mixed", "Example.Mixed." + name, category, "reason") { Route = route };
        var report = new TestRunReport
        {
            ToolVersion = "test",
            Cases =
            [
                Case("First", "Complete(\"a.b\")", TestCategory.Passed, "host"),
                Case("First", "Failure", TestCategory.Failed, "dotnet"),
                Case("First", "Pending(1.5d)", TestCategory.NeedsUnity, "needs-editor"),
                Case("First", "Collision", TestCategory.Passed, "host"),
                Case("Second", "Collision", TestCategory.NeedsUnity, "needs-editor"),
                Case("First", "Unknown", TestCategory.Passed, null),
                Case("First", "Play", TestCategory.UnityOnly, "needs-editor"),
                Case("First", "Delimiter(\"a;b\")", TestCategory.NeedsUnity, "needs-editor"),
            ]
        };
        Assert.Equal(
            "Example.Mixed.Pending(1.5d)\nExample.Mixed.Collision\nExample.Mixed.Unknown\nExample.Mixed.Play\nExample.Mixed.Delimiter(\"a;b\")\n",
            TestReport.UnityTestList(report));
        var legacy = report with { Cases = report.Cases.Select(c => c with { Route = null }).ToList() };
        Assert.Equal("Example.Mixed.Pending(1.5d)\nExample.Mixed.Collision\nExample.Mixed.Play\nExample.Mixed.Delimiter(\"a;b\")\n",
            TestReport.UnityTestList(legacy));
        Assert.Equal("", TestReport.UnityTestList(report with { Cases = [report.Cases[0]] }));
    }

    /// <summary>A name with a line break cannot be listed one per line; it is counted, never silently written.</summary>
    [Fact]
    public void Editor_test_list_omits_and_counts_names_with_line_breaks()
    {
        var report = new TestRunReport
        {
            ToolVersion = "test",
            Cases = [new("A", "Example.Lines", "Example.Lines.Tail\n", TestCategory.NeedsUnity, "pending") { Route = "needs-editor" },
                new("A", "Example.Lines", "Example.Lines.Plain", TestCategory.NeedsUnity, "pending") { Route = "needs-editor" }]
        };
        Assert.Equal("Example.Lines.Plain\n", TestReport.UnityTestList(report));
        Assert.Equal(1, TestReport.UnlistableEditorCases(report));
    }
}
