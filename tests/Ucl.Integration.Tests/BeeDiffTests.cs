using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>
/// <c>ucl bee-diff</c> against hand-written Bee response files (tests/Ucl.Integration.Tests/BeeSamples) placed into a copy
/// of fixture <c>realistic-netfx-nuget</c>; <c>{EDITOR_DATA}</c> stands for the stub editor's data folder.
/// </summary>
public class BeeDiffTests
{
    private const string Fixture = "realistic-netfx-nuget";
    private const string EditorDag = "Library/Bee/artifacts/1900b0aE.dag";

    private static string Prepare(TempDir temp, Action<string>? edit = null)
    {
        var entry = FixtureManifest.Load().Fixtures.Single(f => f.Name == Fixture);
        var project = FixtureRunner.Prepare(entry, temp.Path);
        var data = Path.Combine(Repo.StubEditors, "6000.3.2f1", "Editor", "Data").Replace('\\', '/');
        var samples = Path.Combine(Repo.Root, "tests", "Ucl.Integration.Tests", "BeeSamples", Fixture);
        foreach (var file in Directory.EnumerateFiles(samples, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(project, "Library", "Bee", "artifacts", Path.GetRelativePath(samples, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, File.ReadAllText(file).Replace("{EDITOR_DATA}", data, StringComparison.Ordinal));
        }

        edit?.Invoke(project);
        return project;
    }

    private static (int Exit, string Stdout, string Stderr) Run(TempDir temp, string project, params string[] extra)
    {
        var home = Path.Combine(temp.Path, "home");
        Directory.CreateDirectory(home);
        return Cli.Run(new TestEnvironment(home), ["bee-diff", project, .. extra]);
    }

    private static void Edit(string project, string relative, Func<string, string> change)
    {
        var path = Path.Combine(project, relative);
        File.WriteAllText(path, change(File.ReadAllText(path)));
    }

    [Fact]
    public void Matching_command_lines_agree_and_exit_0()
    {
        using var temp = new TempDir();
        var project = Prepare(temp);
        var (exit, stdout, stderr) = Run(temp, project);
        Assert.True(exit == 0, stdout + stderr);
        Assert.Equal(
            """
            == 1900b0aE.dag (6000.3.2f1 editor StandaloneWindows64 windows)
            Assembly-CSharp: agrees
            Assembly-CSharp-Editor: agrees
            Vendor.Tools.CodeGen: agrees
            == 2000b0aP.dag (6000.3.2f1 player StandaloneWindows64)
            Assembly-CSharp: agrees
            result: 2 dags, 4 assemblies (4 agree), 0 differences, exit 0

            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            stdout);
    }

    [Fact]
    public void Every_kind_of_difference_is_reported_and_exits_1()
    {
        using var temp = new TempDir();
        var project = Prepare(temp, p =>
        {
            Edit(p, $"{EditorDag}/Assembly-CSharp.rsp", t => t
                .Replace("-define:UNITY_ASSERTIONS\n", string.Empty, StringComparison.Ordinal)
                .Replace("\"Assets/Scripts/Leaderboard.cs\"", "\"Assets/Scripts/Leaderboard.cs\"\n\"Assets/Scripts/Ghost.cs\"", StringComparison.Ordinal)
                .Replace("UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll", "NetStandard/ref/2.1.0/netstandard.dll", StringComparison.Ordinal)
                .Replace("-langversion:9.0", "-langversion:10.0", StringComparison.Ordinal)
                .Replace("/nowarn:0282\n", string.Empty, StringComparison.Ordinal)
                .Replace("/deterministic", "-analyzer:\"Assets/Analyzers/Gen.dll\"\n/additionalfile:\"Assets/extra.txt\"\n-unsafe\n/deterministic", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(p, EditorDag, "Ghost.Assembly.rsp"), "\"Assets/Ghost/G.cs\"\n-define:UNITY_EDITOR\n-define:UNITY_STANDALONE_WIN\n");
            File.Delete(Path.Combine(p, EditorDag, "Vendor.Tools.CodeGen.rsp"));
        });
        var (exit, stdout, _) = Run(temp, project);
        Assert.Equal(1, exit);
        Assert.Equal(
            """
            == 1900b0aE.dag (6000.3.2f1 editor StandaloneWindows64 windows)
            Assembly-CSharp: 9 differences
              sources missing in ucl: Assets/Scripts/Ghost.cs
              references missing in ucl: netstandard.dll 2.1.0.0 (editor:NetStandard/ref/2.1.0/netstandard.dll)
              references extra in ucl: mscorlib.dll 4.0.0.0 (editor:UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll)
              defines extra in ucl: UNITY_ASSERTIONS
              options differ for langversion: Editor 10.0, ucl 9.0
              options differ for unsafe: Editor on, ucl off
              nowarn extra in ucl: CS0282
              analyzers missing in ucl: Assets/Analyzers/Gen.dll
              additionalfiles missing in ucl: Assets/extra.txt
            Assembly-CSharp-Editor: agrees
            Ghost.Assembly: 1 difference
              assembly missing in ucl: Ghost.Assembly
            Vendor.Tools.CodeGen: 1 difference
              assembly extra in ucl: Vendor.Tools.CodeGen
            == 2000b0aP.dag (6000.3.2f1 player StandaloneWindows64)
            Assembly-CSharp: agrees
            result: 2 dags, 5 assemblies (2 agree), 11 differences, exit 1

            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            stdout);

        var json = JsonDocument.Parse(Run(temp, project, "--format", "json").Stdout).RootElement;
        Assert.Equal("ucl-beediff/1", json.GetProperty("schema").GetString());
        Assert.Equal(11, json.GetProperty("summary").GetProperty("differences").GetInt32());
        var categories = json.GetProperty("dags")[0].GetProperty("assemblies").EnumerateArray()
            .SelectMany(a => a.GetProperty("differences").EnumerateArray())
            .Select(d => d.GetProperty("category").GetString())
            .ToHashSet();
        Assert.Equal(["assembly", "sources", "references", "defines", "options", "nowarn", "analyzers", "additionalfiles"], categories);

        var sarif = JsonDocument.Parse(Run(temp, project, "--format", "sarif").Stdout).RootElement;
        var rules = sarif.GetProperty("runs")[0].GetProperty("tool").GetProperty("driver").GetProperty("rules").EnumerateArray().Select(r => r.GetProperty("id").GetString());
        Assert.Equal(["UCL5001", "UCL5002", "UCL5003", "UCL5004", "UCL5005", "UCL5006", "UCL5007", "UCL5008"], rules);
        Assert.Equal(11, sarif.GetProperty("runs")[0].GetProperty("results").GetArrayLength());
    }

    [Fact]
    public void Project_assemblies_are_matched_by_name_whatever_their_output_path()
    {
        using var temp = new TempDir();
        var project = Prepare(temp, p => Edit(p, $"{EditorDag}/Assembly-CSharp-Editor.rsp", t => t.Replace(
            "Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.ref.dll", "Library/ScriptAssemblies/Assembly-CSharp.dll", StringComparison.Ordinal)));
        var (exit, stdout, _) = Run(temp, project);
        Assert.True(exit == 0, stdout);
    }

    [Fact]
    public void Without_a_Bee_folder_it_exits_3()
    {
        using var temp = new TempDir();
        var project = Prepare(temp, p => Directory.Delete(Path.Combine(p, "Library", "Bee"), recursive: true));
        var (exit, stdout, _) = Run(temp, project);
        Assert.Equal(3, exit);
        Assert.Contains("error UCL3010: no Library/Bee/artifacts", stdout, StringComparison.Ordinal);
        Assert.Contains("\"id\": \"UCL3010\"", Run(temp, project, "--format", "json").Stdout, StringComparison.Ordinal);
        Assert.Contains("\"ruleId\": \"UCL3010\"", Run(temp, project, "--format", "sarif").Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dag_for_an_unsupported_platform_is_noted_not_compared()
    {
        using var temp = new TempDir();
        var project = Prepare(temp, p =>
        {
            Directory.CreateDirectory(Path.Combine(p, "Library/Bee/artifacts/3000b0aP.dag"));
            File.WriteAllText(Path.Combine(p, "Library/Bee/artifacts/3000b0aP.dag/Assembly-CSharp.rsp"), "\"Assets/Scripts/Leaderboard.cs\"\n-define:UNITY_PS5\n");
            Directory.CreateDirectory(Path.Combine(p, "Library/Bee/artifacts/4000b0aP.dag"));
            File.WriteAllText(Path.Combine(p, "Library/Bee/artifacts/4000b0aP.dag/Empty.rsp"), "-define:UNITY_STANDALONE_WIN\n");
        });
        var (exit, stdout, _) = Run(temp, project);
        Assert.Equal(0, exit);
        Assert.Contains("== 3000b0aP.dag\nnot compared: no platform define", stdout, StringComparison.Ordinal);
        Assert.Contains("== 4000b0aP.dag\nnot compared: no response file with source files", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_is_deterministic_and_the_project_is_untouched()
    {
        using var temp = new TempDir();
        var project = Prepare(temp);
        var before = TreeHash(project);
        var first = Run(temp, project, "--format", "json").Stdout;
        Assert.Equal(first, Run(temp, project, "--format", "json").Stdout);
        Assert.Equal(before, TreeHash(project));
        Assert.DoesNotContain(temp.Path.Replace('\\', '/'), first, StringComparison.Ordinal);
    }

    private static string TreeHash(string root)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            sha.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(root, f)));
            sha.AppendData(File.ReadAllBytes(f));
        }

        return Convert.ToHexString(sha.GetHashAndReset());
    }
}
