using System.Xml.Linq;
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
}
