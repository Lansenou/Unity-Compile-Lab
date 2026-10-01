using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>R12: <c>ucl fetch</c> fills the download cache from a registry so the project resolves offline.</summary>
public sealed class FetchTests
{
    private static (string Project, TestEnvironment Env, string Cache) Setup(TempDir temp, string dependencies, string registryUrl)
    {
        var project = Path.Combine(temp.Path, "p");
        TempDir.Copy(Path.Combine(Repo.Fixtures, "basic-predefined"), project);
        File.WriteAllText(Path.Combine(project, "Packages", "manifest.json"),
            $"{{ \"dependencies\": {{ {dependencies} }}, \"scopedRegistries\": [ {{ \"name\": \"Loopback\", \"url\": \"{registryUrl}\", \"scopes\": [ \"com.example\" ] }} ] }}");
        File.Delete(Path.Combine(project, "Packages", "packages-lock.json"));
        var cache = Path.Combine(temp.Path, "package cache");
        var home = Path.Combine(temp.Path, "home");
        Directory.CreateDirectory(home);
        return (project, new TestEnvironment(home, new Dictionary<string, string> { ["UCL_PACKAGE_CACHE"] = cache }), cache);
    }

    /// <summary>Fetching from a scoped registry over HTTP, transitively, then the project resolves and graphs cleanly.</summary>
    [Fact]
    public void Fetch_downloads_then_project_resolves()
    {
        using var temp = new TempDir();
        using var registry = new LoopbackRegistry()
            .Publish("com.example.a", "1.0.0", "\"com.example.b\": \"2.0.0\"", ("Runtime/A.cs", "namespace Example { public class A {} }"), ("Runtime/Example.A.asmdef", "{ \"name\": \"Example.A\" }"))
            .Publish("com.example.b", "2.0.0");
        var (project, env, cache) = Setup(temp, "\"com.example.a\": \"1.0.0\"", registry.Url);
        var cacheDir = Path.Combine(temp.Path, "c");

        var before = Cli.Run(env, "graph", project, "--cache-dir", cacheDir);
        Assert.Equal(3, before.Exit);
        Assert.Contains("UCL3006", before.Stdout + before.Stderr, StringComparison.Ordinal);

        var (exit, stdout, stderr) = Cli.Run(env, "fetch", project);
        Assert.True(exit == 0, stdout + stderr);
        Assert.Contains($"fetched com.example.a@1.0.0 from {registry.Url}", stdout, StringComparison.Ordinal);
        Assert.Contains($"fetched com.example.b@2.0.0 from {registry.Url}", stdout, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(cache, "com.example.a@1.0.0", "Runtime", "A.cs")));

        var after = Cli.Run(env, "graph", project, "--format", "json", "--cache-dir", cacheDir);
        Assert.True(after.Exit == 0, after.Stdout + after.Stderr);
        Assert.Contains("Example.A", after.Stdout, StringComparison.Ordinal);

        var again = Cli.Run(env, "fetch", project);
        Assert.Equal(0, again.Exit);
        Assert.Contains("cached com.example.a@1.0.0 (download)", again.Stdout, StringComparison.Ordinal);
    }

    /// <summary>A package the registry does not have is a failure line and UCL3006, exit 3.</summary>
    [Fact]
    public void Missing_package_exit_3()
    {
        using var temp = new TempDir();
        using var registry = new LoopbackRegistry();
        var (project, env, cache) = Setup(temp, "\"com.example.missing\": \"1.0.0\", \"com.example.git\": \"https://example.com/x.git\"", registry.Url);

        var (exit, stdout, stderr) = Cli.Run(env, "fetch", project);

        Assert.Equal(3, exit);
        Assert.Contains("failed com.example.missing@1.0.0:", stdout, StringComparison.Ordinal);
        Assert.Contains("failed com.example.git@https://example.com/x.git: not a registry version", stdout, StringComparison.Ordinal);
        Assert.Contains("error UCL3006", stderr, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(cache, "com.example.missing@1.0.0")));
    }

    /// <summary>A folder that is not a project is exit 3.</summary>
    [Fact]
    public void Not_a_project_exit_3()
    {
        using var temp = new TempDir();
        var (exit, _, stderr) = Cli.Run(new TestEnvironment(temp.Path), "fetch", temp.Path);
        Assert.Equal(3, exit);
        Assert.Contains("UCL3001", stderr, StringComparison.Ordinal);
    }
}
