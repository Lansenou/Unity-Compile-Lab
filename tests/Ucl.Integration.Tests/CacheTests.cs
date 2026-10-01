using System.Text.Json;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>R11: the incremental cache gives the same report as a cold run and recompiles only what changed.</summary>
public sealed class CacheTests
{
    /// <summary>A warm run reports exactly what the cold run reported, from the cache.</summary>
    [Theory]
    [InlineData("asmdef-ref-by-name")]
    [InlineData("compile-error")]
    [InlineData("asmdef-missing-reference")]
    public void Warm_run_matches_cold_run(string fixture)
    {
        using var temp = new TempDir();
        var (project, env, cache) = Setup(temp, fixture);
        var cold = Cli.Run(env, "check", project, "--format", "json", "--cache-dir", cache, "--editor-os", "linux");
        var warm = Cli.Run(env, "check", project, "--format", "json", "--cache-dir", cache, "--editor-os", "linux");
        Assert.Equal(cold.Stdout, warm.Stdout);
        Assert.Equal(cold.Exit, warm.Exit);
        var timed = Cli.Run(env, "check", project, "--format", "json", "--cache-dir", cache, "--editor-os", "linux", "--timings");
        Assert.All(Assemblies(timed.Stdout).Where(a => a.GetProperty("status").GetString() != "skipped"), a => Assert.True(a.GetProperty("cached").GetBoolean()));
    }

    /// <summary>Editing a method body recompiles only that assembly: its public surface is unchanged, so dependents stay cached.</summary>
    [Fact]
    public void Body_edit_recompiles_one_assembly()
    {
        using var temp = new TempDir();
        var (project, env, cache) = Setup(temp, "asmdef-ref-by-name");
        Cli.Run(env, "check", project, "--cache-dir", cache);
        var before = Assemblies(Cli.Run(env, "check", project, "--format", "json", "--cache-dir", cache, "--timings").Stdout).ToList();
        Assert.True(before.Count >= 2);

        // The dependency at the bottom of the graph: the first assembly in compile order.
        var first = before[0].GetProperty("name").GetString()!;
        var file = Directory.EnumerateFiles(Path.Combine(project, "Assets"), "*.cs", SearchOption.AllDirectories)
            .First(f => Path.GetDirectoryName(f)!.Split(Path.DirectorySeparatorChar).Any(p => Directory.EnumerateFiles(Path.GetDirectoryName(f)!, "*.asmdef").Any(a => Path.GetFileNameWithoutExtension(a) == first)));
        File.AppendAllText(file, "\n// edited\n");
        var after = Assemblies(Cli.Run(env, "check", project, "--format", "json", "--cache-dir", cache, "--timings").Stdout).ToList();
        foreach (var a in after)
        {
            var name = a.GetProperty("name").GetString();
            Assert.Equal(name != first, a.GetProperty("cached").GetBoolean());
        }
    }

    /// <summary>--no-cache never writes the cache folder.</summary>
    [Fact]
    public void No_cache_writes_nothing()
    {
        using var temp = new TempDir();
        var (project, env, cache) = Setup(temp, "basic-predefined");
        Cli.Run(env, "check", project, "--cache-dir", cache, "--no-cache");
        Assert.False(Directory.Exists(cache) && Directory.EnumerateFileSystemEntries(cache).Any());
    }

    private static (string Project, TestEnvironment Env, string Cache) Setup(TempDir temp, string fixture)
    {
        var project = Path.Combine(temp.Path, "project");
        TempDir.Copy(Path.Combine(Repo.Fixtures, fixture), project);
        var home = Path.Combine(temp.Path, "home");
        Directory.CreateDirectory(home);
        return (project, new TestEnvironment(home), Path.Combine(temp.Path, "cache"));
    }

    private static IEnumerable<JsonElement> Assemblies(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("cells")[0].GetProperty("assemblies").EnumerateArray().Select(a => a.Clone());
}
