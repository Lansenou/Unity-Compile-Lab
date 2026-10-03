using System.Text.Json;
using Ucl.Compilation;
using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Discovery;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>Dependent compilation progresses while root analysis runs; late errors preserve diagnostic/cascade behavior.</summary>
public sealed class AnalyzerPipelineTests
{
    /// <summary>Suppressed promoted warnings allow metadata images; full emit errors remain authoritative.</summary>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    public void Suppressed_promoted_warning_preserves_image_and_emit_error_behavior(bool fullImages, int exit)
    {
        using var temp = new TempDir();
        var project = Path.Combine(temp.Path, "project");
        TempDir.Copy(Path.Combine(Repo.Fixtures, "basic-predefined"), project);
        File.WriteAllText(Path.Combine(project, "Assets", "Unused.cs"), "public class Unused { public void M() { int unused; } }");
        var dll = Path.Combine(project, "Assets", "Suppressor.dll");
        File.Copy(Path.Combine(Repo.Stubs, "dlls", "Ucl.Fixture.CompilerSuppressor.dll"), dll);
        File.WriteAllText(dll + ".meta", "labels:\n- RoslynAnalyzer\n");
        var env = new TestEnvironment(temp.Path);
        var fs = new PhysicalFileSystem(temp.Path);
        var context = new ProjectLoader(fs, env).Load(project);
        Assert.NotNull(context.Inventory);
        var cell = new CompileCell(context.Inventory.ProjectVersion, TargetKind.Editor, BuildPlatform.StandaloneWindows64, null, false, HostOs.Linux);
        var graph = AssemblyGraphBuilder.Build(context.Inventory, cell);
        var editor = new EditorLocator(fs, env).Locate(cell.UnityVersion, null);
        Assert.True(editor.Ok);
        var output = new CompilationRunner(fs).RunWithImages(graph, context, editor.Value!,
            new CompileSettings { FullImages = fullImages, WarnAsError = true, MaxParallelism = 1 });
        Assert.Equal(exit, output.Result.ExitCode);
        Assert.Equal(fullImages ? "Failed" : "Compiled", Assert.Single(output.Result.Assemblies).Status.ToString());
        Assert.Equal(fullImages ? 0 : 1, output.Images.Count);
        if (fullImages) Assert.Contains(output.Result.Diagnostics, d => d.Id == "CS0168");
        else Assert.DoesNotContain(output.Result.Diagnostics, d => d.Id == "CS0168");
    }

    /// <summary>The leaf generator signals its start before the root analyzer finishes, even with one compile slot.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Dependents_start_before_root_analysis_completes(int jobs)
    {
        using var temp = new TempDir();
        var entry = FixtureManifest.Load().Fixtures.Single(f => f.Name == "analyzer-slow");
        var project = FixtureRunner.Prepare(entry, temp.Path);
        var signal = Path.Combine(temp.Path, "signal");
        Directory.CreateDirectory(signal);
        File.WriteAllText(Path.Combine(project, ".globalconfig"), $"is_global = true\nucl_fixture.signal_dir = {signal.Replace('\\', '/')}\n");
        var args = FixtureRunner.Args(project, entry.Cells[0], Path.Combine(temp.Path, "cache"));
        var result = Cli.Run(new TestEnvironment(temp.Path), [.. args, "--no-cache", "--jobs", jobs.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        Assert.True(result.Exit == 0, $"Exit {result.Exit}\n{result.Stdout}\n{result.Stderr}");
        Assert.DoesNotContain("USLOW002", result.Stdout, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(signal, "leaf.started")), result.Stdout);
        using var json = JsonDocument.Parse(result.Stdout);
        Assert.Equal(4, json.RootElement.GetProperty("summary").GetProperty("analyzerDiagnostics").GetInt32());
        Assert.All(json.RootElement.GetProperty("cells")[0].GetProperty("assemblies").EnumerateArray(), a => Assert.Equal("compiled", a.GetProperty("status").GetString()));
    }

    /// <summary>A root analyzer's eventual error is reported, cached and causes the same final dependency cascade.</summary>
    [Fact]
    public void Late_analyzer_failure_preserves_cached_diagnostics_and_dependency_cascades()
    {
        using var temp = new TempDir();
        var entry = FixtureManifest.Load().Fixtures.Single(f => f.Name == "analyzer-slow");
        var project = FixtureRunner.Prepare(entry, temp.Path);
        File.WriteAllText(Path.Combine(project, ".globalconfig"), "is_global = true\nucl_fixture.fail_root = true\n");
        var args = FixtureRunner.Args(project, entry.Cells[0], Path.Combine(temp.Path, "cache"));
        var env = new TestEnvironment(temp.Path);
        var cold = Cli.Run(env, [.. args]);
        var warm = Cli.Run(env, [.. args]);
        Assert.Equal(1, cold.Exit);
        Assert.Equal(cold.Exit, warm.Exit);
        Assert.Equal(cold.Stdout, warm.Stdout);
        using var json = JsonDocument.Parse(warm.Stdout);
        Assert.Contains("USLOW003", warm.Stdout, StringComparison.Ordinal);
        var assemblies = json.RootElement.GetProperty("cells")[0].GetProperty("assemblies").EnumerateArray().ToList();
        Assert.Equal("failed", assemblies[0].GetProperty("status").GetString());
        Assert.All(assemblies.Skip(1), a =>
        {
            Assert.Equal("skipped", a.GetProperty("status").GetString());
            Assert.Equal("Timing.Root", Assert.Single(a.GetProperty("blockedBy").EnumerateArray()).GetString());
        });

        var timed = Cli.Run(env, [.. args, "--no-cache", "--timings"]);
        Assert.Equal(1, timed.Exit);
        using var timingJson = JsonDocument.Parse(timed.Stdout);
        // Speculative work is included in timing totals even when its diagnostics are blocked by a late dependency error.
        Assert.All(timingJson.RootElement.GetProperty("cells")[0].GetProperty("assemblies").EnumerateArray(), a =>
            Assert.NotEmpty(a.GetProperty("analyzerTimings").EnumerateArray()));
        var slowTime = Assert.Single(timingJson.RootElement.GetProperty("summary").GetProperty("analyzerTimings").EnumerateArray());
        Assert.True(slowTime.GetProperty("timeMs").GetDouble() >= 600);

        // ucl test must never receive speculative full images belonging to a failed root or its blocked dependents.
        var fs = new PhysicalFileSystem(temp.Path);
        var context = new ProjectLoader(fs, env).Load(project);
        Assert.NotNull(context.Inventory);
        var cell = new CompileCell(context.Inventory.ProjectVersion, TargetKind.Editor, BuildPlatform.StandaloneWindows64, null, false, HostOs.Linux);
        var graph = AssemblyGraphBuilder.Build(context.Inventory, cell);
        var editor = new EditorLocator(fs, env).Locate(cell.UnityVersion, null);
        Assert.True(editor.Ok);
        var settings = new CompileSettings { FullImages = true, MaxParallelism = 1, CacheDirectory = Path.Combine(temp.Path, "full-cache") };
        var runner = new CompilationRunner(fs);
        var full = runner.RunWithImages(graph, context, editor.Value!, settings);
        var cachedFull = runner.RunWithImages(graph, context, editor.Value!, settings);
        Assert.Equal(1, full.Result.ExitCode);
        Assert.Equal(full.Result.ExitCode, cachedFull.Result.ExitCode);
        Assert.Empty(full.Images);
        Assert.Empty(cachedFull.Images);
    }
}
