using System.Text.Json;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>Analyzer timings reflect callback execution, aggregate by analyzer, and do not leak into deterministic output.</summary>
public sealed class AnalyzerTimingTests
{
    /// <summary>The tracked timing path preserves suppression of compiler warnings, not just analyzer diagnostics.</summary>
    [Fact]
    public void Compiler_warnings_remain_suppressible_with_timing_enabled()
    {
        using var temp = new TempDir();
        var project = Path.Combine(temp.Path, "project");
        TempDir.Copy(Path.Combine(Repo.Fixtures, "basic-predefined"), project);
        File.WriteAllText(Path.Combine(project, "Assets", "Unused.cs"), "public class Unused { public void M() { int unused; } }");
        var dll = Path.Combine(project, "Assets", "Suppressor.dll");
        File.Copy(Path.Combine(Repo.Stubs, "dlls", "Ucl.Fixture.CompilerSuppressor.dll"), dll);
        File.WriteAllText(dll + ".meta", "labels:\n- RoslynAnalyzer\n");
        var result = Cli.Run(new TestEnvironment(temp.Path), "check", project, "--no-cache", "--format", "json", "--timings");
        Assert.Equal(0, result.Exit);
        using var report = JsonDocument.Parse(result.Stdout);
        Assert.DoesNotContain(report.RootElement.GetProperty("cells").EnumerateArray().SelectMany(c => c.GetProperty("diagnostics").EnumerateArray()),
            d => d.GetProperty("id").GetString() == "CS0168");
        Assert.DoesNotContain("AD0001", result.Stdout, StringComparison.Ordinal);
        var disabled = Cli.Run(new TestEnvironment(temp.Path), "check", project, "--no-cache", "--format", "json", "--analyzers", "off");
        Assert.Contains("CS0168", disabled.Stdout, StringComparison.Ordinal);
    }

    /// <summary>A deliberately slow analyzer has measurable time on every assembly and exact summary totals.</summary>
    [Fact]
    public void Slow_analyzer_has_per_assembly_timings_and_summary_totals()
    {
        using var temp = new TempDir();
        var fixture = FixtureManifest.Load().Fixtures.Single(f => f.Name == "analyzer-slow");
        var project = FixtureRunner.Prepare(fixture, temp.Path);
        var env = new TestEnvironment(temp.Path);
        var args = FixtureRunner.Args(project, fixture.Cells[0], Path.Combine(temp.Path, "cache"));
        var cold = Cli.Run(env, [.. args, "--timings"]);
        Assert.Equal(0, cold.Exit);
        using var json = JsonDocument.Parse(cold.Stdout);
        var root = json.RootElement;
        var total = 0.0;
        foreach (var assembly in root.GetProperty("cells")[0].GetProperty("assemblies").EnumerateArray())
        {
            Assert.True(assembly.TryGetProperty("analyzerTimings", out var timings), cold.Stdout);
            var timing = Assert.Single(timings.EnumerateArray());
            Assert.Equal("Ucl.Fixture.Slow.SlowAnalyzer", timing.GetProperty("analyzer").GetString());
            Assert.Equal("Assets/Analyzers/Ucl.Fixture.Slow.dll", timing.GetProperty("path").GetString());
            Assert.Equal("analyzer", timing.GetProperty("timeScope").GetString());
            Assert.Equal(new[] { "USLOW001", "USLOW002", "USLOW003" }, timing.GetProperty("ruleIds").EnumerateArray().Select(r => r.GetString()));
            var elapsed = timing.GetProperty("timeMs").GetDouble();
            Assert.True(elapsed >= 150, $"Expected the 200ms callback to be measured, got {elapsed}ms");
            total += elapsed;
        }

        var summary = Assert.Single(root.GetProperty("summary").GetProperty("analyzerTimings").EnumerateArray());
        Assert.Equal(total, summary.GetProperty("timeMs").GetDouble(), precision: 5);
        Assert.Equal(new[] { "USLOW001", "USLOW002", "USLOW003" }, summary.GetProperty("ruleIds").EnumerateArray().Select(r => r.GetString()));
        var cellSummary = Assert.Single(root.GetProperty("cells")[0].GetProperty("summary").GetProperty("analyzerTimings").EnumerateArray());
        Assert.Equal(total, cellSummary.GetProperty("timeMs").GetDouble(), precision: 5);

        var warm = Cli.Run(env, [.. args, "--timings"]);
        using var cached = JsonDocument.Parse(warm.Stdout);
        Assert.Equal(0, warm.Exit);
        Assert.All(cached.RootElement.GetProperty("cells")[0].GetProperty("assemblies").EnumerateArray(), a =>
        {
            Assert.True(a.GetProperty("cached").GetBoolean());
            Assert.Empty(a.GetProperty("analyzerTimings").EnumerateArray());
        });
        Assert.Empty(cached.RootElement.GetProperty("summary").GetProperty("analyzerTimings").EnumerateArray());
        Assert.Equal(4, cached.RootElement.GetProperty("summary").GetProperty("analyzerDiagnostics").GetInt32());

        var plain = Cli.Run(env, [.. args]);
        Assert.DoesNotContain("analyzerTimings", plain.Stdout, StringComparison.Ordinal);
        var disabled = Cli.Run(env, [.. args, "--no-cache", "--analyzers", "off", "--timings"]);
        using var off = JsonDocument.Parse(disabled.Stdout);
        Assert.Equal(0, disabled.Exit);
        Assert.Empty(off.RootElement.GetProperty("summary").GetProperty("analyzerTimings").EnumerateArray());
        Assert.Equal(0, off.RootElement.GetProperty("summary").GetProperty("analyzerDiagnostics").GetInt32());
    }

    [Fact]
    public void Text_timings_list_assemblies_analyzers_and_shared_rules_sorted_by_time()
    {
        using var temp = new TempDir();
        var fixture = FixtureManifest.Load().Fixtures.Single(f => f.Name == "analyzer-slow");
        var project = FixtureRunner.Prepare(fixture, temp.Path);
        var result = Cli.Run(new TestEnvironment(temp.Path), "check", project, "--no-cache", "--timings");
        Assert.Equal(0, result.Exit);
        Assert.Contains("assembly\tanalyzer\trule IDs (shared time)\ttimeMs", result.Stdout, StringComparison.Ordinal);
        var rows = result.Stdout.Split('\n').Select(l => l.Split('\t')).Where(r => r.Length == 4 && r[0] != "assembly").ToArray();
        Assert.Equal(4, rows.Length);
        Assert.All(rows, r =>
        {
            Assert.Equal(4, r.Length);
            Assert.Equal("Ucl.Fixture.Slow.SlowAnalyzer", r[1]);
            Assert.Equal("USLOW001,USLOW002,USLOW003", r[2]);
        });
        var times = rows.Select(r => double.Parse(r[3], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(times.OrderDescending(), times);
    }

}
