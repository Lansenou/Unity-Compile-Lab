using Ucl.Core.Graph;
using Ucl.Core.Model;

namespace Ucl.Core.Tests;

/// <summary>The same project given in a different order yields the same graph.</summary>
public class GraphDeterminismTests
{
    private static InventoryBuilder BigProject() => new InventoryBuilder()
        .Settings(ProjectSettingsParserTests.Unity6Asset)
        .Package("com.unity.test-framework", "1.4.5")
        .Package("com.unity.modules.physics", "1.0.0")
        .Package("com.foo", "1.2.0")
        .Asmdef("Assets/Core/Core.asmdef", "Core", guid: "c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0")
        .Asmdef("Assets/Game/Game.asmdef", "Game", "\"references\": [\"GUID:c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0\", \"Missing\"], \"versionDefines\": [{ \"name\": \"com.foo\", \"expression\": \"1.0\", \"define\": \"FOO\" }, { \"name\": \"\", \"expression\": \"\", \"define\": \"X\" }]")
        .Asmdef("Assets/Game/Editor/Game.Editor.asmdef", "Game.Editor", "\"references\": [\"Game\"], \"includePlatforms\": [\"Editor\"]")
        .Asmdef("Assets/Cyc1/Cyc1.asmdef", "Cyc1", "\"references\": [\"Cyc2\"]")
        .Asmdef("Assets/Cyc2/Cyc2.asmdef", "Cyc2", "\"references\": [\"Cyc1\"]")
        .Asmdef("Assets/Tests/Tests.asmdef", "Tests", "\"optionalUnityReferences\": [\"TestAssemblies\"], \"references\": [\"Game\"]")
        .Asmdef("Assets/Dup/Core.asmdef", "Core")
        .Asmref("Assets/Ext/Core.asmref", "Core")
        .Asmref("Assets/Dead/Dead.asmref", "Nope")
        .Plugin("Assets/Plugins/B.dll")
        .Plugin("Assets/Plugins/A.dll", Metas.Plugin(Metas.AnyPlatformData("Android")))
        .Plugin("Assets/Core/Analyzers/Core.Analyzers.dll", Metas.Analyzer())
        .Plugin("Assets/Analyzers/Global.dll", Metas.Analyzer("dddddddddddddddddddddddddddddddd"))
        .Rsp("Assets/csc.rsp", "-define:R1 -nowarn:618 -bogus")
        .Rsp("Assets/Game/csc.rsp", "-define:R2 -unsupported")
        .RuleSet("Assets/Default.ruleset")
        .AnalyzerConfig("Assets/.editorconfig")
        .AnalyzerConfig("Assets/Game/.editorconfig")
        .Scripts(
            "Assets/Z.cs", "Assets/A.cs", "Assets/Editor/E2.cs", "Assets/Editor/E1.cs", "Assets/Plugins/P.cs",
            "Assets/Core/C2.cs", "Assets/Core/C1.cs", "Assets/Game/G.cs", "Assets/Game/Editor/GE.cs",
            "Assets/Ext/X.cs", "Assets/Dead/D.cs", "Packages/com.foo/Loose.cs", "Assets/Tests/T.cs");

    private static string Snapshot(AssemblyGraph g)
    {
        var lines = new List<string>();
        foreach (var a in g.Assemblies)
        {
            lines.Add($"{a.Name}|{a.Kind}|{a.DefinitionPath}|{a.Engine}|{a.AllowUnsafe}|{a.LangVersion}|{a.Nullable}|{a.RuleSet}|{a.ResponseFile}|{a.IsEditorOnly}|{a.WarnAsErrorAll}");
            lines.Add("  src " + string.Join(",", a.Sources));
            lines.Add("  ref " + string.Join(",", a.References));
            lines.Add("  drop " + string.Join(",", a.DroppedReferences));
            lines.Add("  dll " + string.Join(",", a.PrecompiledReferences));
            lines.Add("  ana " + string.Join(",", a.Analyzers));
            lines.Add("  def " + string.Join(",", a.Defines.Reasons.Select(r => r.Key + "=" + r.Value)));
            lines.Add("  nowarn " + string.Join(",", a.NoWarn));
            lines.Add("  cfg " + string.Join(",", a.AnalyzerConfigs));
        }

        lines.AddRange(g.Excluded.Select(e => $"excluded {e.Key}: {e.Value}"));
        lines.AddRange(g.ScriptOwners.Select(e => $"owner {e.Key}: {e.Value}"));
        lines.AddRange(g.Diagnostics.Select(d => $"diag {d.Id} {d.Severity} {d.Assembly} {d.File} {d.Message}"));
        lines.AddRange(g.Problems.Select(p => $"problem {p.Id} {p.File} {p.Message}"));
        lines.Add("modules " + string.Join(",", g.EnabledModules));
        lines.Add("base " + string.Join(",", g.BaseDefines.Symbols));
        return string.Join("\n", lines);
    }

    public static TheoryData<CompileCell> AllCells()
    {
        var data = new TheoryData<CompileCell>();
        foreach (var p in Enum.GetValues<BuildPlatform>())
        {
            data.Add(Cells.Editor(p));
            data.Add(Cells.Player(p));
            data.Add(Cells.Player(p, development: true));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllCells))]
    public void Same_graph_for_reversed_inventory_lists(CompileCell cell)
    {
        var builder = BigProject();
        var forward = AssemblyGraphBuilder.Build(builder.Build(), cell);
        var reversed = AssemblyGraphBuilder.Build(builder.BuildReversed(), cell);
        Assert.Equal(Snapshot(forward), Snapshot(reversed));
        Assert.Equal(forward.Assemblies.Select(a => a.Name), reversed.Assemblies.Select(a => a.Name));
        Assert.Equal(forward.Diagnostics, reversed.Diagnostics);
        Assert.Equal(forward.Problems, reversed.Problems);
    }

    [Fact]
    public void Same_graph_for_shuffled_inventory_lists()
    {
        var builder = BigProject();
        var baseline = builder.Build();
        var rng = new Random(4242);
        List<T> Shuffle<T>(IReadOnlyList<T> list) => [.. list.OrderBy(_ => rng.Next())];
        for (var n = 0; n < 20; n++)
        {
            var shuffled = baseline with
            {
                Scripts = Shuffle(baseline.Scripts),
                Asmdefs = Shuffle(baseline.Asmdefs),
                Asmrefs = Shuffle(baseline.Asmrefs),
                Plugins = Shuffle(baseline.Plugins),
                ResponseFiles = Shuffle(baseline.ResponseFiles),
                RuleSets = Shuffle(baseline.RuleSets),
                AnalyzerConfigs = Shuffle(baseline.AnalyzerConfigs),
                Packages = Shuffle(baseline.Packages),
            };
            Assert.Equal(Snapshot(AssemblyGraphBuilder.Build(baseline, Cells.Editor())), Snapshot(AssemblyGraphBuilder.Build(shuffled, Cells.Editor())));
        }
    }

    [Fact]
    public void Big_project_sanity()
    {
        var g = BigProject().Editor();
        Assert.Equal("UCL3005", Assert.Single(g.Problems).Id);
        Assert.Contains(g.Diagnostics, d => d.Id == "UCL1001");
        Assert.Contains(g.Diagnostics, d => d.Id == "UCL1002");
        Assert.Contains(g.Diagnostics, d => d.Id == "UCL1003");
        Assert.Contains(g.Diagnostics, d => d.Id == "UCL1010");
        Assert.Contains(g.Diagnostics, d => d.Id == "UCL1020");
        Assert.Contains(g.Diagnostics, d => d.Id == "UCL1021");
        Assert.Equal(["Assets/Core/C1.cs", "Assets/Core/C2.cs", "Assets/Ext/X.cs"], g.Find("Core")!.Sources);
        Assert.True(g.Find("Game")!.Defines.Contains("FOO"));
        Assert.True(g.Find("Game")!.Defines.Contains("R2"));
        Assert.False(g.Find("Game")!.Defines.Contains("R1"));
        Assert.NotNull(g.Find("Tests"));
        Assert.Equal(["Assets/Analyzers/Global.dll", "Assets/Core/Analyzers/Core.Analyzers.dll"], g.Find("Game")!.Analyzers);
    }
}
