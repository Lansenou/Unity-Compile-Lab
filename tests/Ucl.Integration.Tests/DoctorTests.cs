using System.Text.Json;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary><c>ucl doctor</c>: environment report, project readiness, exit codes, and no writes.</summary>
public sealed class DoctorTests
{
    private static string CopyFixture(TempDir temp, string fixture = "basic-predefined")
    {
        var project = Path.Combine(temp.Path, "p");
        TempDir.Copy(Path.Combine(Repo.Fixtures, fixture), project);
        return project;
    }

    /// <summary>Without a project it lists the environment and the stub editors, exit 0.</summary>
    [Fact]
    public void Environment_only()
    {
        using var temp = new TempDir();
        var (exit, stdout, _) = Cli.Run(new TestEnvironment(temp.Path), "doctor");
        Assert.Equal(0, exit);
        Assert.Contains("ucl ", stdout, StringComparison.Ordinal);
        Assert.Contains(".NET", stdout, StringComparison.Ordinal);
        Assert.Contains(Repo.StubEditors, stdout, StringComparison.Ordinal);
        Assert.Contains("6000.0.30f1", stdout, StringComparison.Ordinal);
        Assert.Contains("UCL_EDITOR_ROOTS=" + Repo.StubEditors, stdout, StringComparison.Ordinal);
        Assert.Contains("UNITY_EDITOR_PATH=(not set)", stdout, StringComparison.Ordinal);
    }

    /// <summary>A project with its editor installed and every package resolved is ready: exit 0, and nothing is written.</summary>
    [Fact]
    public void Ready_project_exit_0_and_writes_nothing()
    {
        using var temp = new TempDir();
        var project = CopyFixture(temp);
        var files = Directory.EnumerateFileSystemEntries(temp.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();

        var (exit, stdout, stderr) = Cli.Run(new TestEnvironment(temp.Path), "doctor", project);

        Assert.True(exit == 0, stdout + stderr);
        Assert.Contains("ok: the project can be checked", stdout, StringComparison.Ordinal);
        Assert.Contains("version: 6000.0.30f1", stdout, StringComparison.Ordinal);
        Assert.Equal(files, Directory.EnumerateFileSystemEntries(temp.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>JSON output has the schema id, the project summary and the fixes list.</summary>
    [Fact]
    public void Json_output()
    {
        using var temp = new TempDir();
        var project = CopyFixture(temp);
        var (exit, stdout, _) = Cli.Run(new TestEnvironment(temp.Path), "doctor", project, "--format", "json");
        Assert.Equal(0, exit);
        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        Assert.Equal("ucl-doctor/1", root.GetProperty("schema").GetString());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal(0, root.GetProperty("fixes").GetArrayLength());
        Assert.Contains(root.GetProperty("editors").EnumerateArray(), e => e.GetProperty("version").GetString() == "6000.0.30f1" && e.GetProperty("hasManaged").GetBoolean());
        Assert.Equal("6000.0.30f1", root.GetProperty("project").GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("git").ValueKind);
    }

    /// <summary>A missing editor and an unresolved package are both listed as fixes, exit 3.</summary>
    [Fact]
    public void Missing_editor_and_package_exit_3()
    {
        using var temp = new TempDir();
        var project = CopyFixture(temp);
        File.WriteAllText(Path.Combine(project, "Packages", "manifest.json"), "{ \"dependencies\": { \"com.example.nowhere\": \"1.0.0\" } }");
        var (exit, stdout, _) = Cli.Run(new TestEnvironment(temp.Path), "doctor", project, "--unity-version", "6000.9.9f1", "--format", "json");
        Assert.Equal(3, exit);
        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("ok").GetBoolean());
        var fixes = root.GetProperty("fixes").EnumerateArray().Select(f => f.GetString()!).ToList();
        Assert.Contains(fixes, f => f.Contains("UCL3003", StringComparison.Ordinal));
        Assert.Contains(fixes, f => f.Contains("UCL3006", StringComparison.Ordinal));
        Assert.Equal("com.example.nowhere@1.0.0", Assert.Single(root.GetProperty("project").GetProperty("unresolved").EnumerateArray()).GetString());
    }

    /// <summary>Formats other than text and json are refused.</summary>
    [Fact]
    public void Sarif_is_refused()
    {
        using var temp = new TempDir();
        var (exit, _, stderr) = Cli.Run(new TestEnvironment(temp.Path), "doctor", "--format", "sarif");
        Assert.Equal(3, exit);
        Assert.Contains("UCL3009", stderr, StringComparison.Ordinal);
    }
}
