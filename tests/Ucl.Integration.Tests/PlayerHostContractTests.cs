using System.Xml.Linq;
using System.Reflection;
using Ucl.Compilation;
using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Rules;
using Ucl.Discovery;
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
            [null, null, null, new CliOptions { EditorCases = audit }, report, TextWriter.Null])!;
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

}
