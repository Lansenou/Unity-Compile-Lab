using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Ucl.Integration.Tests;

/// <summary>
/// The oracle's Editor.log parsers (oracle/parse-log.sh and the PowerShell twin in oracle/record.ps1) turn the
/// hand-written sample logs in oracle/samples/ into exactly the expected JSON. A test whose shell is not
/// installed passes with a note in the test output (Git Bash provides bash on Windows CI).
/// </summary>
public sealed class OracleParserTests(ITestOutputHelper output)
{
    /// <summary>The sample logs, each with a <c>.expected.json</c> beside it.</summary>
    public static TheoryData<string> Samples => new() { "editor-errors", "player-ok" };

    /// <summary><c>bash oracle/parse-log.sh &lt;sample&gt;</c> prints the expected JSON.</summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public void OracleParser_bash_matches_expected(string sample)
    {
        var bash = FindBash();
        if (bash is null)
        {
            output.WriteLine("SKIPPED: bash is not on PATH (on Windows install Git for Windows).");
            return;
        }

        AssertMatches(sample, Run(bash, "oracle/parse-log.sh", $"oracle/samples/{sample}.log"));
    }

    /// <summary><c>pwsh -NoProfile -File oracle/record.ps1 -ParseOnly &lt;sample&gt;</c> prints the expected JSON.</summary>
    [Theory]
    [MemberData(nameof(Samples))]
    public void OracleParser_pwsh_matches_expected(string sample)
    {
        var pwsh = FindOnPath("pwsh");
        if (pwsh is null)
        {
            output.WriteLine("SKIPPED: pwsh (PowerShell 7) is not on PATH.");
            return;
        }

        AssertMatches(sample, Run(pwsh, "-NoProfile", "-NonInteractive", "-File", "oracle/record.ps1", "-ParseOnly", $"oracle/samples/{sample}.log"));
    }

    private static void AssertMatches(string sample, string actual)
    {
        var expected = File.ReadAllText(Path.Combine(Repo.Root, "oracle", "samples", sample + ".expected.json"));
        var actualNode = JsonNode.Parse(actual);
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expected), actualNode),
            $"parser output for {sample}.log differs from {sample}.expected.json:\n{actual}");
    }

    private static string Run(string exe, params string[] args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = Repo.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi)!;
        var stderrTask = p.StandardError.ReadToEndAsync();
        var stdout = p.StandardOutput.ReadToEnd();
        Assert.True(p.WaitForExit(120_000), $"{exe} did not finish within 120 s");
        Assert.True(p.ExitCode == 0, $"{exe} {string.Join(' ', args)} exited {p.ExitCode}: {stderrTask.Result}");
        return stdout;
    }

    private static string? FindBash()
    {
        if (OperatingSystem.IsWindows())
        {
            // Prefer Git Bash: System32\bash.exe is the WSL launcher, which does not see this checkout's paths.
            foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramW6432") })
            {
                var git = root is null ? null : Path.Combine(root, "Git", "bin", "bash.exe");
                if (git is not null && File.Exists(git))
                {
                    return git;
                }
            }
        }

        return FindOnPath("bash");
    }

    private static string? FindOnPath(string name)
    {
        var exts = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", string.Empty } : [string.Empty];
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in exts)
            {
                var candidate = Path.Combine(dir, name + ext);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
