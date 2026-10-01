using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>R12: <c>--changed &lt;git-ref&gt;</c> reports only assemblies whose inputs changed, plus their dependents.</summary>
public sealed class ChangedTests
{
    /// <summary>A change in a dependency reports it and its dependent; a change in a leaf reports only the leaf.</summary>
    [Fact]
    public void Reports_changed_assemblies_and_dependents()
    {
        using var temp = new TempDir();
        var project = Repository(temp, "asmdef-ref-by-name");
        var env = new TestEnvironment(Path.Combine(temp.Path, "home"));
        var cache = Path.Combine(temp.Path, "cache");

        Assert.Empty(Reported(Cli.Run(env, "check", project, "--changed", "HEAD", "--format", "json", "--cache-dir", cache)));

        File.AppendAllText(Path.Combine(project, "Assets", "Game", "Core", "Health.cs"), "\n// edit\n");
        Assert.Equal(["Game.Core", "Game.Gameplay"], Reported(Cli.Run(env, "check", project, "--changed", "HEAD", "--format", "json", "--cache-dir", cache)));

        Git(project, "checkout", "--", ".");
        File.AppendAllText(Path.Combine(project, "Assets", "Game", "Gameplay", "DamageZone.cs"), "\n// edit\n");
        Assert.Equal(["Game.Gameplay"], Reported(Cli.Run(env, "check", project, "--changed", "HEAD", "--format", "json", "--cache-dir", cache)));

        // A new untracked script counts too; an asmdef change means everything.
        File.WriteAllText(Path.Combine(project, "Assets", "Game", "Core", "Extra.cs"), "namespace Game.Core { public class Extra {} }\n");
        Assert.Equal(["Game.Core", "Game.Gameplay"], Reported(Cli.Run(env, "check", project, "--changed", "HEAD", "--format", "json", "--cache-dir", cache)));
    }

    /// <summary>A ref git cannot resolve is a configuration problem.</summary>
    [Fact]
    public void Unknown_ref_is_exit_3()
    {
        using var temp = new TempDir();
        var project = Repository(temp, "basic-predefined");
        var (exit, stdout, _) = Cli.Run(new TestEnvironment(Path.Combine(temp.Path, "home")), "check", project, "--changed", "no-such-ref", "--cache-dir", Path.Combine(temp.Path, "c"));
        Assert.Equal(3, exit);
        Assert.Contains("--changed", stdout, StringComparison.Ordinal);
    }

    private static string Repository(TempDir temp, string fixture)
    {
        var project = Path.Combine(temp.Path, "project");
        TempDir.Copy(Path.Combine(Repo.Fixtures, fixture), project);
        Directory.CreateDirectory(Path.Combine(temp.Path, "home"));
        Git(project, "init", "-q");
        Git(project, "add", "-A");
        Git(project, "-c", "user.name=t", "-c", "user.email=t@example.com", "-c", "commit.gpgsign=false", "commit", "-qm", "base");
        return project;
    }

    private static List<string> Reported((int Exit, string Stdout, string Stderr) run) =>
        JsonDocument.Parse(run.Stdout).RootElement.GetProperty("cells")[0].GetProperty("assemblies").EnumerateArray()
            .Select(a => a.GetProperty("name").GetString()!).Order(StringComparer.Ordinal).ToList();

    private static void Git(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi)!;
        p.WaitForExit();
        Assert.True(p.ExitCode == 0, $"git {string.Join(' ', args)}: {p.StandardError.ReadToEnd()}");
    }
}
