using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>R10: exit codes and argument handling of the CLI.</summary>
public sealed class CliTests
{
    /// <summary>Help lists every exit code.</summary>
    [Fact]
    public void Help_documents_exit_codes()
    {
        using var temp = new TempDir();
        var (exit, stdout, _) = Cli.Run(new TestEnvironment(temp.Path), "--help");
        Assert.Equal(0, exit);
        foreach (var code in new[] { "0  clean", "1  compile", "2  only warnings", "3  configuration", "4  internal" })
        {
            Assert.Contains(code, stdout, StringComparison.Ordinal);
        }
    }

    /// <summary>Version prints the tool version.</summary>
    [Fact]
    public void Version_prints()
    {
        using var temp = new TempDir();
        var (exit, stdout, _) = Cli.Run(new TestEnvironment(temp.Path), "version");
        Assert.Equal(0, exit);
        Assert.StartsWith("ucl ", stdout, StringComparison.Ordinal);
    }

    /// <summary>Unknown options are configuration problems.</summary>
    [Theory]
    [InlineData("--bogus")]
    [InlineData("--target", "server")]
    [InlineData("--platform", "PS5")]
    [InlineData("--format", "xml")]
    [InlineData("--unity-version", "six")]
    [InlineData("--jobs", "0")]
    [InlineData("--target")]
    public void Bad_arguments_exit_3(params string[] args)
    {
        using var temp = new TempDir();
        var (exit, _, stderr) = Cli.Run(new TestEnvironment(temp.Path), ["check", .. args]);
        Assert.Equal(3, exit);
        Assert.Contains("UCL3009", stderr, StringComparison.Ordinal);
    }

    /// <summary>A folder that is not a Unity project is exit 3 with UCL3001.</summary>
    [Fact]
    public void Not_a_project_exit_3()
    {
        using var temp = new TempDir();
        var (exit, stdout, _) = Cli.Run(new TestEnvironment(temp.Path), "check", temp.Path, "--format", "json");
        Assert.Equal(3, exit);
        Assert.Contains("UCL3001", stdout, StringComparison.Ordinal);
    }

    /// <summary>A Unity 6 project with no matching editor installed is exit 3 with UCL3003.</summary>
    [Fact]
    public void Missing_editor_exit_3()
    {
        using var temp = new TempDir();
        var project = Path.Combine(temp.Path, "p");
        TempDir.Copy(Path.Combine(Repo.Fixtures, "basic-predefined"), project);
        var (exit, stdout, _) = Cli.Run(new TestEnvironment(temp.Path), "check", project, "--unity-version", "6000.9.9f1", "--cache-dir", Path.Combine(temp.Path, "c"));
        Assert.Equal(3, exit);
        Assert.Contains("UCL3003", stdout, StringComparison.Ordinal);
    }

    /// <summary>--output refuses the protected project folders.</summary>
    [Fact]
    public void Output_inside_Assets_is_refused()
    {
        using var temp = new TempDir();
        var project = Path.Combine(temp.Path, "p");
        TempDir.Copy(Path.Combine(Repo.Fixtures, "basic-predefined"), project);
        var (exit, _, stderr) = Cli.Run(new TestEnvironment(temp.Path), "check", project, "--output", Path.Combine(project, "Assets", "r.json"), "--cache-dir", Path.Combine(temp.Path, "c"));
        Assert.Equal(3, exit);
        Assert.Contains("Assets", stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project, "Assets", "r.json")));
    }
}
