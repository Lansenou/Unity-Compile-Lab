namespace Ucl.Core.Tests;

/// <summary>DLLs labelled <c>RoslynAnalyzer</c>: never a reference; scoped by asmdef folder.</summary>
public class GraphAnalyzerTests
{
    [Fact]
    public void Analyzer_is_never_a_reference()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Analyzers/MyAnalyzer.dll", Metas.Analyzer())
            .Asmdef("Assets/Mod/Mod.asmdef", "Mod", "\"overrideReferences\": true, \"precompiledReferences\": [\"MyAnalyzer.dll\"]")
            .Scripts("Assets/A.cs")
            .Editor();
        Assert.All(g.Assemblies, a => Assert.Empty(a.PrecompiledReferences));
        Assert.Empty(g.Diagnostics);
    }

    [Fact]
    public void Analyzer_outside_any_asmdef_folder_applies_to_all_predefined_assemblies_only()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Analyzers/MyAnalyzer.dll", Metas.Analyzer())
            .Asmdef("Assets/Mod/Mod.asmdef", "Mod")
            .Scripts("Assets/A.cs", "Assets/Plugins/P.cs", "Assets/Editor/E.cs", "Assets/Plugins/Editor/PE.cs")
            .Editor();
        foreach (var name in new[] { "Assembly-CSharp", "Assembly-CSharp-firstpass", "Assembly-CSharp-Editor", "Assembly-CSharp-Editor-firstpass" })
        {
            Assert.Equal(["Assets/Analyzers/MyAnalyzer.dll"], g.Find(name)!.Analyzers);
        }

        Assert.Empty(g.Find("Mod")!.Analyzers);
    }

    [Fact]
    public void Analyzer_outside_asmdef_in_player_cell_skips_absent_Editor_assemblies()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Analyzers/MyAnalyzer.dll", Metas.Analyzer())
            .Scripts("Assets/A.cs", "Assets/Editor/E.cs")
            .Player();
        Assert.Equal(["Assets/Analyzers/MyAnalyzer.dll"], g.Find("Assembly-CSharp")!.Analyzers);
        Assert.Single(g.Assemblies);
    }

    [Fact]
    public void Analyzer_in_asmdef_folder_applies_to_that_asmdef_and_its_direct_referrers()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Core/Analyzers/Core.Analyzers.dll", Metas.Analyzer())
            .Asmdef("Assets/Core/Core.asmdef", "Core")
            .Asmdef("Assets/Game/Game.asmdef", "Game", "\"references\": [\"Core\"]")
            .Asmdef("Assets/Top/Top.asmdef", "Top", "\"references\": [\"Game\"]")
            .Asmdef("Assets/Other/Other.asmdef", "Other")
            .Scripts("Assets/A.cs", "Assets/Editor/E.cs")
            .Editor();

        Assert.Equal(["Assets/Core/Analyzers/Core.Analyzers.dll"], g.Find("Core")!.Analyzers);
        Assert.Equal(["Assets/Core/Analyzers/Core.Analyzers.dll"], g.Find("Game")!.Analyzers);
        Assert.Empty(g.Find("Top")!.Analyzers);
        Assert.Empty(g.Find("Other")!.Analyzers);

        // Assembly-CSharp references every auto-referenced asmdef, so it is a direct referrer too.
        Assert.Equal(["Assets/Core/Analyzers/Core.Analyzers.dll"], g.Find("Assembly-CSharp")!.Analyzers);
        Assert.Equal(["Assets/Core/Analyzers/Core.Analyzers.dll"], g.Find("Assembly-CSharp-Editor")!.Analyzers);
    }

    [Fact]
    public void Analyzer_in_folder_of_non_auto_referenced_asmdef_skips_predefined()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Core/Core.Analyzers.dll", Metas.Analyzer())
            .Asmdef("Assets/Core/Core.asmdef", "Core", "\"autoReferenced\": false")
            .Scripts("Assets/A.cs")
            .Editor();
        Assert.Equal(["Assets/Core/Core.Analyzers.dll"], g.Find("Core")!.Analyzers);
        Assert.Empty(g.Find("Assembly-CSharp")!.Analyzers);
    }

    [Fact]
    public void Analyzer_of_excluded_asmdef_applies_nowhere()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Tools/Tools.Analyzers.dll", Metas.Analyzer())
            .Asmdef("Assets/Tools/Tools.asmdef", "Tools", "\"includePlatforms\": [\"Editor\"]")
            .Scripts("Assets/A.cs")
            .Player();
        Assert.All(g.Assemblies, a => Assert.Empty(a.Analyzers));
    }

    [Fact]
    public void Analyzer_label_must_be_exact()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Plugins/NotAnalyzer.dll", Metas.Plugin(Metas.AnyPlatformData(), labels: ["roslynanalyzer"]))
            .Scripts("Assets/A.cs")
            .Editor();
        Assert.Equal(["Assets/Plugins/NotAnalyzer.dll"], g.Find("Assembly-CSharp")!.PrecompiledReferences);
        Assert.Empty(g.Find("Assembly-CSharp")!.Analyzers);
    }

    [Fact]
    public void Multiple_analyzers_are_sorted()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Z/Z.dll", Metas.Analyzer("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"))
            .Plugin("Assets/A/A.dll", Metas.Analyzer())
            .Scripts("Assets/X.cs")
            .Editor();
        Assert.Equal(["Assets/A/A.dll", "Assets/Z/Z.dll"], g.Find("Assembly-CSharp")!.Analyzers);
    }
}
