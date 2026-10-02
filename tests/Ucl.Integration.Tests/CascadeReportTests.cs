using System.Text.Json;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>G5: a failed assembly blocks its dependents; the report names the root failure and leads with it.</summary>
public sealed class CascadeReportTests
{
    // Core fails; Mid and Edge (through Mid) are skipped; Other compiles. Wide also fails and blocks nothing.
    private static string Project(TempDir temp)
    {
        var root = Path.Combine(temp.Path, "project");
        void Write(string relative, string text)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        Write("ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.0.30f1\n");
        Write("Packages/manifest.json", "{ \"dependencies\": {} }\n");
        Write("Assets/Core/Core.asmdef", "{ \"name\": \"Core\" }");
        Write("Assets/Core/Broken.cs", "namespace Core { public class Broken { int A() => missing1; int B() => missing2; string C() => 1; } }");
        Write("Assets/Mid/Mid.asmdef", "{ \"name\": \"Mid\", \"references\": [\"Core\"] }");
        Write("Assets/Mid/Mid.cs", "namespace Mid { public class M : Core.Broken { } }");
        Write("Assets/Edge/Edge.asmdef", "{ \"name\": \"Edge\", \"references\": [\"Mid\"] }");
        Write("Assets/Edge/Edge.cs", "namespace Edge { public class E : Mid.M { } }");
        Write("Assets/Other/Other.asmdef", "{ \"name\": \"Other\" }");
        Write("Assets/Other/Other.cs", "namespace Other { public class O { } }");
        Write("Assets/Wide/Wide.asmdef", "{ \"name\": \"Wide\" }");
        Write("Assets/Wide/Wide.cs", "namespace Wide { public class W { int A() => nope; } }");
        return root;
    }

    private static (int Exit, string Stdout, string Stderr) Check(TempDir temp, string project, params string[] extra)
    {
        var home = Path.Combine(temp.Path, "home");
        Directory.CreateDirectory(home);
        return Cli.Run(new TestEnvironment(home), ["check", project, "--no-cache", "--editor-os", "linux", .. extra]);
    }

    [Fact]
    public void Skipped_assemblies_name_their_root_failure()
    {
        using var temp = new TempDir();
        var project = Project(temp);
        var json = JsonDocument.Parse(Check(temp, project, "--format", "json").Stdout).RootElement;
        var assemblies = json.GetProperty("cells")[0].GetProperty("assemblies").EnumerateArray().ToDictionary(a => a.GetProperty("name").GetString()!);
        Assert.Equal("failed", assemblies["Core"].GetProperty("status").GetString());
        Assert.False(assemblies["Core"].TryGetProperty("blockedBy", out _));
        Assert.Equal("dependency 'Core' failed", assemblies["Mid"].GetProperty("skipReason").GetString());
        Assert.Equal("dependency 'Core' failed (through 'Mid', skipped)", assemblies["Edge"].GetProperty("skipReason").GetString());
        Assert.Equal(["Core"], assemblies["Edge"].GetProperty("blockedBy").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("compiled", assemblies["Other"].GetProperty("status").GetString());
    }

    [Fact]
    public void Text_leads_with_root_failures_and_summary_keeps_only_them()
    {
        using var temp = new TempDir();
        var project = Project(temp);
        var (exit, text, _) = Check(temp, project);
        Assert.Equal(1, exit);
        var lines = text.Split('\n');
        Assert.StartsWith("== 6000.0.30f1 editor StandaloneWindows64", lines[0], StringComparison.Ordinal);
        Assert.Equal("root failures: 2 assemblies failed to compile; 2 skipped because of them", lines[1]);
        Assert.Equal("  Core: 3 errors, blocks 2 assemblies", lines[2]);
        Assert.StartsWith("    2 x CS0103, first: Assets/Core/Broken.cs(1,", lines[3], StringComparison.Ordinal);
        Assert.StartsWith("    1 x CS0029, first: Assets/Core/Broken.cs(1,", lines[4], StringComparison.Ordinal);
        Assert.Equal("  Wide: 1 error, blocks 0 assemblies", lines[5]);
        Assert.Contains("skipped Edge: dependency 'Core' failed (through 'Mid', skipped)\n", text, StringComparison.Ordinal);

        var summary = Check(temp, project, "--summary").Stdout;
        Assert.Equal(string.Join('\n', lines[..7]) + "\nresult: 4 errors, 0 warnings, 5 assemblies (2 skipped), exit 1\nexit 1\n", summary);
    }
}
