using Ucl.Core.Model;

namespace Ucl.Core.Tests;

/// <summary>asmdef references (name and GUID), predefined implicit references, cycles and ordering.</summary>
public class GraphReferenceTests
{
    private const string CoreGuid = "c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0";

    [Fact]
    public void References_by_name_and_by_GUID_from_the_meta()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Core/Core.asmdef", "Core", guid: CoreGuid)
            .Asmdef("Assets/Util/Util.asmdef", "Util")
            .Asmdef("Assets/Game/Game.asmdef", "Game", $"\"references\": [\"GUID:{CoreGuid}\", \"Util\"]")
            .Editor();

        Assert.Equal(["Core", "Util"], g.Find("Game")!.References);
        Assert.Empty(g.Find("Game")!.DroppedReferences);
        Assert.Empty(g.Diagnostics);
        Assert.Equal(["Core", "Util", "Game"], g.Assemblies.Select(a => a.Name));
    }

    [Fact]
    public void Runtime_predefined_assemblies_never_reference_Editor_only_asmdefs()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Tools/Tools.asmdef", "Tools", "\"includePlatforms\": [\"Editor\"]")
            .Asmdef("Assets/Lib/Lib.asmdef", "Lib")
            .Scripts("Assets/Main.cs", "Assets/Plugins/P.cs", "Assets/Editor/E.cs", "Assets/Plugins/Editor/PE.cs")
            .Editor();

        Assert.Equal(["Lib"], g.Find("Assembly-CSharp-firstpass")!.References);
        Assert.Equal(["Assembly-CSharp-firstpass", "Lib"], g.Find("Assembly-CSharp")!.References);
        Assert.Contains("Tools", g.Find("Assembly-CSharp-Editor")!.References);
        Assert.Contains("Tools", g.Find("Assembly-CSharp-Editor-firstpass")!.References);
    }

    [Fact]
    public void GUID_reference_without_matching_meta_is_UCL1001()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Core/Core.asmdef", "Core")
            .Asmdef("Assets/Game/Game.asmdef", "Game", $"\"references\": [\"GUID:{CoreGuid}\"]")
            .Editor();
        var d = Assert.Single(g.Diagnostics);
        Assert.Equal("UCL1001", d.Id);
        Assert.Equal("Game", d.Assembly);
        Assert.Empty(g.Find("Game")!.References);
    }

    [Fact]
    public void Unresolved_name_is_UCL1001_warning_and_dropped()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Game/Game.asmdef", "Game", "\"references\": [\"Missing.Lib\", \"Assembly-CSharp\"]")
            .Scripts("Assets/A.cs")
            .Editor();

        Assert.Equal(2, g.Diagnostics.Count);
        Assert.All(g.Diagnostics, d =>
        {
            Assert.Equal(ProblemIds.MissingReference, d.Id);
            Assert.Equal(Severity.Warning, d.Severity);
            Assert.Equal("Assets/Game/Game.asmdef", d.File);
        });
        Assert.Contains(g.Diagnostics, d => d.Message.Contains("'Missing.Lib'", StringComparison.Ordinal));
        Assert.Empty(g.Find("Game")!.References);
        Assert.Empty(g.Find("Game")!.DroppedReferences);
    }

    [Fact]
    public void Self_and_duplicate_references_are_ignored()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Core/Core.asmdef", "Core", guid: CoreGuid)
            .Asmdef("Assets/Game/Game.asmdef", "Game", $"\"references\": [\"Game\", \"Core\", \"GUID:{CoreGuid}\"]")
            .Editor();
        Assert.Equal(["Core"], g.Find("Game")!.References);
        Assert.Empty(g.Diagnostics);
    }

    [Fact]
    public void Reference_to_assembly_not_in_cell_is_dropped_silently()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/EditorTools/EditorTools.asmdef", "EditorTools", "\"includePlatforms\": [\"Editor\"]")
            .Asmdef("Assets/Game/Game.asmdef", "Game", "\"references\": [\"EditorTools\", \"EditorTools\"]")
            .Player();
        var game = g.Find("Game")!;
        Assert.Empty(game.References);
        Assert.Equal(["EditorTools"], game.DroppedReferences);
        Assert.Empty(g.Diagnostics);
    }

    [Fact]
    public void Predefined_runtime_assemblies_reference_auto_referenced_asmdefs_and_firstpass()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Lib/Lib.asmdef", "Lib")
            .Asmdef("Assets/Hidden/Hidden.asmdef", "Hidden", "\"autoReferenced\": false")
            .Scripts("Assets/A.cs", "Assets/Plugins/P.cs")
            .Editor();

        Assert.Equal(["Lib"], g.Find("Assembly-CSharp-firstpass")!.References);
        Assert.Equal(["Assembly-CSharp-firstpass", "Lib"], g.Find("Assembly-CSharp")!.References);
    }

    [Fact]
    public void autoReferenced_false_is_not_seen_by_Assembly_CSharp_but_asmdefs_can_reference_it()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Hidden/Hidden.asmdef", "Hidden", "\"autoReferenced\": false")
            .Asmdef("Assets/User/User.asmdef", "User", "\"references\": [\"Hidden\"]")
            .Scripts("Assets/A.cs", "Assets/Editor/E.cs")
            .Editor();
        Assert.DoesNotContain("Hidden", g.Find("Assembly-CSharp")!.References);
        Assert.DoesNotContain("Hidden", g.Find("Assembly-CSharp-Editor")!.References);
        Assert.Contains("User", g.Find("Assembly-CSharp")!.References);
        Assert.Equal(["Hidden"], g.Find("User")!.References);
    }

    [Fact]
    public void Editor_predefined_references_all_three_earlier_phases()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Lib/Lib.asmdef", "Lib")
            .Scripts("Assets/A.cs", "Assets/Plugins/P.cs", "Assets/Plugins/Editor/PE.cs", "Assets/Editor/E.cs")
            .Editor();

        Assert.Equal(["Assembly-CSharp", "Assembly-CSharp-Editor-firstpass", "Assembly-CSharp-firstpass", "Lib"], g.Find("Assembly-CSharp-Editor")!.References);
        Assert.Equal(["Assembly-CSharp-firstpass", "Lib"], g.Find("Assembly-CSharp-Editor-firstpass")!.References);
        Assert.Equal(
            ["Lib", "Assembly-CSharp-firstpass", "Assembly-CSharp", "Assembly-CSharp-Editor-firstpass", "Assembly-CSharp-Editor"],
            g.Assemblies.Select(a => a.Name));
    }

    [Fact]
    public void Editor_predefined_references_only_phases_that_exist()
    {
        var g = new InventoryBuilder().Scripts("Assets/Editor/E.cs").Editor();
        Assert.Empty(g.Find("Assembly-CSharp-Editor")!.References);
    }

    [Fact]
    public void Test_assemblies_are_not_referenced_by_predefined_assemblies()
    {
        var g = new InventoryBuilder()
            .Package("com.unity.test-framework", "1.4.5")
            .Asmdef("Assets/Tests/Tests.asmdef", "Tests", "\"optionalUnityReferences\": [\"TestAssemblies\"]")
            .Scripts("Assets/A.cs")
            .Editor();
        Assert.NotNull(g.Find("Tests"));
        Assert.Empty(g.Find("Assembly-CSharp")!.References);
    }

    [Fact]
    public void Predefined_assemblies_never_see_excluded_asmdefs()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Droid/Droid.asmdef", "Droid", "\"includePlatforms\": [\"Android\"]")
            .Scripts("Assets/A.cs")
            .Player(BuildPlatform.StandaloneWindows64);
        Assert.Empty(g.Find("Assembly-CSharp")!.References);
        Assert.Empty(g.Find("Assembly-CSharp")!.DroppedReferences);
    }

    [Fact]
    public void Cycles_are_UCL1002_errors_and_excluded()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/A/A.asmdef", "A", "\"references\": [\"B\"]")
            .Asmdef("Assets/B/B.asmdef", "B", "\"references\": [\"C\"]")
            .Asmdef("Assets/C/C.asmdef", "C", "\"references\": [\"A\"]")
            .Asmdef("Assets/D/D.asmdef", "D", "\"references\": [\"A\"]")
            .Asmdef("Assets/E/E.asmdef", "E")
            .Scripts("Assets/Main.cs")
            .Editor();

        var cyc = g.Diagnostics.Where(d => d.Id == "UCL1002").ToList();
        Assert.Equal(["A", "B", "C"], cyc.Select(d => d.Assembly));
        Assert.All(cyc, d => Assert.Equal(Severity.Error, d.Severity));
        Assert.Equal("Assets/A/A.asmdef", cyc[0].File);
        Assert.Contains("cyclic", cyc[0].Message, StringComparison.Ordinal);
        Assert.Equal("cyclic references", g.Excluded["A"]);
        Assert.Equal("cyclic references", g.Excluded["B"]);
        Assert.Equal("cyclic references", g.Excluded["C"]);
        Assert.Equal(["D", "E", "Assembly-CSharp"], g.Assemblies.Select(a => a.Name));
        Assert.Equal(["A"], g.Find("D")!.DroppedReferences);
        Assert.Empty(g.Find("D")!.References);
        Assert.Equal(["D", "E"], g.Find("Assembly-CSharp")!.References);
    }

    [Fact]
    public void Self_cycle_via_GUID_is_ignored_like_a_self_reference()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/A/A.asmdef", "A", $"\"references\": [\"GUID:{CoreGuid}\"]", guid: CoreGuid)
            .Editor();
        Assert.Empty(g.Diagnostics);
        Assert.NotNull(g.Find("A"));
    }

    [Fact]
    public void Assemblies_are_in_dependency_order_with_ordinal_ties()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Z/Z.asmdef", "Zeta")
            .Asmdef("Assets/A/A.asmdef", "Alpha", "\"references\": [\"Zeta\"]")
            .Asmdef("Assets/M/M.asmdef", "Mid")
            .Asmdef("Assets/T/T.asmdef", "Top", "\"references\": [\"Alpha\", \"Mid\"]")
            .Editor();
        Assert.Equal(["Mid", "Zeta", "Alpha", "Top"], g.Assemblies.Select(a => a.Name));
    }
}
