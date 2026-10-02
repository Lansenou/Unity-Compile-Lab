using Ucl.Core.Graph;
using Ucl.Core.Model;

namespace Ucl.Core.Tests;

/// <summary>Which assembly owns each script: special folders, asmdef, asmref, and the definition problems.</summary>
public class GraphOwnershipTests
{
    private const string GuidA = "11111111111111111111111111111111";

    [Fact]
    public void Special_folders_select_the_four_predefined_assemblies()
    {
        var g = new InventoryBuilder()
            .Scripts(
                "Assets/Scripts/Player.cs",
                "Assets/Plugins/Lib.cs",
                "Assets/Standard Assets/Water.cs",
                "Assets/Pro Standard Assets/Image.cs",
                "Assets/Editor/Tool.cs",
                "Assets/Game/Deep/editor/Inspector.cs",
                "Assets/Plugins/Editor/PluginTool.cs")
            .Editor();

        Assert.Equal(["Assets/Scripts/Player.cs"], g.Find("Assembly-CSharp")!.Sources);
        Assert.Equal(["Assets/Plugins/Lib.cs", "Assets/Pro Standard Assets/Image.cs", "Assets/Standard Assets/Water.cs"], g.Find("Assembly-CSharp-firstpass")!.Sources);
        Assert.Equal(["Assets/Editor/Tool.cs", "Assets/Game/Deep/editor/Inspector.cs"], g.Find("Assembly-CSharp-Editor")!.Sources);
        Assert.Equal(["Assets/Plugins/Editor/PluginTool.cs"], g.Find("Assembly-CSharp-Editor-firstpass")!.Sources);
        Assert.All(g.Assemblies, a => Assert.Equal(AssemblyKind.Predefined, a.Kind));
        Assert.All(g.Assemblies, a => Assert.Null(a.DefinitionPath));
        Assert.Equal("Assembly-CSharp-Editor-firstpass", g.ScriptOwners["Assets/Plugins/Editor/PluginTool.cs"]);
        Assert.Empty(g.Problems);
        Assert.Empty(g.Diagnostics);
    }

    [Fact]
    public void Predefined_assemblies_exist_only_with_scripts()
    {
        var g = new InventoryBuilder().Scripts("Assets/A.cs").Editor();
        Assert.Equal(["Assembly-CSharp"], g.Assemblies.Select(a => a.Name));
        Assert.Empty(new InventoryBuilder().Editor().Assemblies);
    }

    [Fact]
    public void Editor_predefined_assemblies_are_editor_only()
    {
        var g = new InventoryBuilder().Scripts("Assets/A.cs", "Assets/Editor/E.cs", "Assets/Plugins/Editor/F.cs", "Assets/Plugins/P.cs").Editor();
        Assert.True(g.Find("Assembly-CSharp-Editor")!.IsEditorOnly);
        Assert.True(g.Find("Assembly-CSharp-Editor-firstpass")!.IsEditorOnly);
        Assert.False(g.Find("Assembly-CSharp")!.IsEditorOnly);
        Assert.False(g.Find("Assembly-CSharp-firstpass")!.IsEditorOnly);
    }

    [Fact]
    public void Asmdef_owns_its_folder_and_subfolders_and_wins_over_Editor_and_Plugins_rules()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Game/Game.asmdef", "Game")
            .Asmdef("Assets/Plugins/Vendor/Vendor.asmdef", "Vendor")
            .Scripts("Assets/Game/A.cs", "Assets/Game/Editor/GameEditor.cs", "Assets/Game/Sub/B.cs", "Assets/Plugins/Vendor/V.cs", "Assets/GameOther/C.cs")
            .Editor();

        var game = g.Find("Game")!;
        Assert.Equal(AssemblyKind.Asmdef, game.Kind);
        Assert.Equal("Assets/Game/Game.asmdef", game.DefinitionPath);
        Assert.Equal(["Assets/Game/A.cs", "Assets/Game/Editor/GameEditor.cs", "Assets/Game/Sub/B.cs"], game.Sources);
        Assert.Equal(["Assets/Plugins/Vendor/V.cs"], g.Find("Vendor")!.Sources);
        Assert.Equal(["Assets/GameOther/C.cs"], g.Find("Assembly-CSharp")!.Sources);
        Assert.Null(g.Find("Assembly-CSharp-Editor"));
        Assert.Null(g.Find("Assembly-CSharp-firstpass"));
    }

    [Fact]
    public void Asmdef_Editor_folder_scripts_stay_in_the_asmdef_in_player_cells()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Game/Game.asmdef", "Game")
            .Scripts("Assets/Game/Editor/GameEditor.cs")
            .Player();
        Assert.Equal(["Assets/Game/Editor/GameEditor.cs"], g.Find("Game")!.Sources);
    }

    [Fact]
    public void Nearest_asmdef_wins()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Game/Game.asmdef", "Game")
            .Asmdef("Assets/Game/Editor/Game.Editor.asmdef", "Game.Editor", "\"includePlatforms\": [\"Editor\"]")
            .Scripts("Assets/Game/A.cs", "Assets/Game/Editor/E.cs", "Assets/Game/Editor/Deep/F.cs")
            .Editor();
        Assert.Equal(["Assets/Game/A.cs"], g.Find("Game")!.Sources);
        Assert.Equal(["Assets/Game/Editor/Deep/F.cs", "Assets/Game/Editor/E.cs"], g.Find("Game.Editor")!.Sources);
    }

    [Fact]
    public void Asmdef_without_scripts_is_skipped_with_UCL1006()
    {
        var g = new InventoryBuilder().WithoutPlaceholderScripts()
            .Asmdef("Assets/Empty/Empty.asmdef", "Empty", "\"references\": [\"GUID:00000000000000000000000000000000\"]").Editor();
        Assert.Empty(g.Assemblies);
        Assert.Equal("no scripts", g.Excluded["Empty"]);
        var d = Assert.Single(g.Diagnostics);
        Assert.Equal((ProblemIds.ScriptlessAssembly, Severity.Info), (d.Id, d.Severity));
    }

    [Fact]
    public void Package_script_without_asmdef_is_UCL1010_and_not_compiled()
    {
        var g = new InventoryBuilder()
            .Package("com.foo", "1.0.0")
            .Asmdef("Packages/com.foo/Runtime/Foo.asmdef", "Foo")
            .Scripts("Packages/com.foo/Runtime/Foo.cs", "Packages/com.foo/Samples/Loose.cs")
            .Editor();

        Assert.Equal(["Packages/com.foo/Runtime/Foo.cs"], g.Find("Foo")!.Sources);
        Assert.Null(g.Find("Assembly-CSharp"));
        Assert.False(g.ScriptOwners.ContainsKey("Packages/com.foo/Samples/Loose.cs"));
        var d = Assert.Single(g.Diagnostics);
        Assert.Equal(ProblemIds.PackageScriptWithoutAsmdef, d.Id);
        Assert.Equal("UCL1010", d.Id);
        Assert.Equal(Severity.Warning, d.Severity);
        Assert.Equal(DiagnosticOrigin.Ucl, d.Origin);
        Assert.Equal("Packages/com.foo/Samples/Loose.cs", d.File);
    }

    [Fact]
    public void Asmref_adds_its_folder_scripts_to_the_target()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Game/Game.asmdef", "Game")
            .Asmref("Assets/Extensions/Game.asmref", "Game")
            .Scripts("Assets/Game/A.cs", "Assets/Extensions/Ext.cs", "Assets/Extensions/Editor/ExtEditor.cs")
            .Editor();

        Assert.Equal(["Assets/Extensions/Editor/ExtEditor.cs", "Assets/Extensions/Ext.cs", "Assets/Game/A.cs"], g.Find("Game")!.Sources);
        Assert.Equal("Game", g.ScriptOwners["Assets/Extensions/Ext.cs"]);
        Assert.Single(g.Assemblies);
        Assert.Empty(g.Diagnostics);
    }

    [Fact]
    public void Asmref_by_GUID()
    {
        var g = new InventoryBuilder().WithoutPlaceholderScripts()
            .Asmdef("Packages/com.foo/Runtime/Foo.asmdef", "Foo", guid: GuidA)
            .Asmref("Assets/FooExt/Foo.asmref", "GUID:" + GuidA.ToUpperInvariant())
            .Scripts("Assets/FooExt/X.cs")
            .Editor();
        Assert.Equal(["Assets/FooExt/X.cs"], g.Find("Foo")!.Sources);
    }

    [Fact]
    public void Unresolved_asmref_is_UCL1003_and_its_scripts_are_dropped()
    {
        var g = new InventoryBuilder()
            .Asmref("Assets/Orphan/Missing.asmref", "Does.Not.Exist")
            .Scripts("Assets/Orphan/X.cs", "Assets/Orphan/Sub/Y.cs", "Assets/Main.cs")
            .Editor();

        var d = Assert.Single(g.Diagnostics);
        Assert.Equal("UCL1003", d.Id);
        Assert.Equal(Severity.Warning, d.Severity);
        Assert.Equal("Assets/Orphan/Missing.asmref", d.File);
        Assert.Equal(["Assets/Main.cs"], g.Find("Assembly-CSharp")!.Sources);
        Assert.False(g.ScriptOwners.ContainsKey("Assets/Orphan/X.cs"));
        Assert.False(g.ScriptOwners.ContainsKey("Assets/Orphan/Sub/Y.cs"));
        Assert.Empty(g.Problems);
    }

    [Fact]
    public void Duplicate_assembly_names_are_UCL3005_first_path_wins()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/B/Game.asmdef", "Game")
            .Asmdef("Assets/A/Game.asmdef", "Game")
            .Scripts("Assets/A/X.cs", "Assets/B/Y.cs")
            .Editor();

        var p = Assert.Single(g.Problems);
        Assert.Equal("UCL3005", p.Id);
        Assert.Equal("Assets/B/Game.asmdef", p.File);
        Assert.Contains("Assets/A/Game.asmdef", p.Message);
        Assert.Equal("Assets/A/Game.asmdef", g.Find("Game")!.DefinitionPath);
        Assert.Equal(["Assets/A/X.cs"], g.Find("Game")!.Sources);
        Assert.Equal(["Assets/B/Y.cs"], g.Find("Assembly-CSharp")!.Sources);
    }

    [Fact]
    public void Asmdef_named_like_a_predefined_assembly_is_UCL3005()
    {
        var g = new InventoryBuilder().Asmdef("Assets/X/X.asmdef", "Assembly-CSharp").Scripts("Assets/X/A.cs").Editor();
        var p = Assert.Single(g.Problems);
        Assert.Equal("UCL3005", p.Id);
        Assert.Contains("a predefined assembly", p.Message);
        Assert.Equal(["Assets/X/A.cs"], g.Find("Assembly-CSharp")!.Sources);
    }

    [Fact]
    public void Two_asmdefs_in_one_folder_are_UCL3007()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Game/A.asmdef", "A")
            .Asmdef("Assets/Game/B.asmdef", "B")
            .Scripts("Assets/Game/X.cs")
            .Editor();
        var p = Assert.Single(g.Problems);
        Assert.Equal("UCL3007", p.Id);
        Assert.Equal("Assets/Game/B.asmdef", p.File);
        Assert.Equal(["Assets/Game/X.cs"], g.Find("A")!.Sources);
        Assert.Null(g.Find("B"));
    }

    [Fact]
    public void Asmdef_and_asmref_in_one_folder_are_UCL3007()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Game/A.asmdef", "A")
            .Asmdef("Assets/Other/B.asmdef", "B")
            .Asmref("Assets/Game/B.asmref", "B")
            .Scripts("Assets/Game/X.cs")
            .Editor();
        var p = Assert.Single(g.Problems);
        Assert.Equal("UCL3007", p.Id);
        Assert.Equal("Assets/Game/B.asmref", p.File);
        Assert.Equal(["Assets/Game/X.cs"], g.Find("A")!.Sources);
    }

    [Fact]
    public void Malformed_asmdef_is_UCL3004_and_its_scripts_fall_back_to_predefined()
    {
        var g = new InventoryBuilder()
            .AsmdefRaw("Assets/Bad/Bad.asmdef", "{ \"name\": \"Bad\", ")
            .Scripts("Assets/Bad/X.cs")
            .Editor();
        var p = Assert.Single(g.Problems);
        Assert.Equal("UCL3004", p.Id);
        Assert.Equal("Assets/Bad/Bad.asmdef", p.File);
        Assert.Contains("invalid JSON", p.Message);
        Assert.Equal(["Assets/Bad/X.cs"], g.Find("Assembly-CSharp")!.Sources);
    }

    [Fact]
    public void Asmdef_with_include_and_exclude_platforms_is_UCL3004()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Both/Both.asmdef", "Both", "\"includePlatforms\": [\"Editor\"], \"excludePlatforms\": [\"Android\"]")
            .Editor();
        var p = Assert.Single(g.Problems);
        Assert.Equal("UCL3004", p.Id);
        Assert.Contains("both 'includePlatforms' and 'excludePlatforms'", p.Message);
        Assert.Null(g.Find("Both"));
    }

    [Fact]
    public void Malformed_asmref_is_UCL3004()
    {
        var g = new InventoryBuilder().AsmrefRaw("Assets/X/X.asmref", "{ }").Scripts("Assets/X/A.cs").Editor();
        var p = Assert.Single(g.Problems);
        Assert.Equal("UCL3004", p.Id);
        Assert.Equal("Assets/X/X.asmref", p.File);
        Assert.Equal(["Assets/X/A.cs"], g.Find("Assembly-CSharp")!.Sources);
    }

    [Fact]
    public void ScriptOwners_include_scripts_of_excluded_assemblies()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Tools/Tools.asmdef", "Tools", "\"includePlatforms\": [\"Editor\"]")
            .Scripts("Assets/Tools/T.cs", "Assets/Editor/E.cs", "Assets/A.cs")
            .Player();
        Assert.Equal("Tools", g.ScriptOwners["Assets/Tools/T.cs"]);
        Assert.Equal("Assembly-CSharp-Editor", g.ScriptOwners["Assets/Editor/E.cs"]);
        Assert.Equal(["Assembly-CSharp"], g.Assemblies.Select(a => a.Name));
        Assert.Contains("Tools", g.Excluded.Keys);
        Assert.Equal("Editor scripts are not part of a player build", g.Excluded["Assembly-CSharp-Editor"]);
    }

    [Fact]
    public void DefinitionIndex_public_api()
    {
        var inventory = new InventoryBuilder()
            .Asmdef("Assets/Game/Game.asmdef", "Game", guid: GuidA)
            .Asmdef("Assets/Game/Sub/Sub.asmdef", "Sub")
            .Asmref("Assets/Ext/Game.asmref", "Game")
            .Asmref("Assets/Dead/Dead.asmref", "Nope")
            .Build();
        var index = new DefinitionIndex(inventory);

        Assert.Equal(["Game", "Sub"], index.Asmdefs.Select(a => a.Data.Name));
        Assert.Equal("Game", index.Resolve("Game")!.Data.Name);
        Assert.Equal("Game", index.Resolve("guid:" + GuidA)!.Data.Name);
        Assert.Null(index.Resolve("GUID:22222222222222222222222222222222"));
        Assert.Null(index.Resolve("game"));
        Assert.Equal("Game", index.OwnerOf("Assets/Game/X.cs"));
        Assert.Equal("Sub", index.OwnerOf("Assets/Game/Sub/Deep/X.cs"));
        Assert.Equal("Game", index.OwnerOf("Assets/Ext/X.cs"));
        Assert.Equal(string.Empty, index.OwnerOf("Assets/Dead/X.cs"));
        Assert.Null(index.OwnerOf("Assets/X.cs"));
        Assert.Equal("Game", index.AsmdefFolderOwner("Assets/Game/Analyzers/A.dll")!.Data.Name);
        Assert.Equal("Sub", index.AsmdefFolderOwner("Assets/Game/Sub/A.dll")!.Data.Name);
        Assert.Null(index.AsmdefFolderOwner("Assets/Ext/A.dll"));
        Assert.Empty(index.Problems);
        Assert.Equal("UCL1003", Assert.Single(index.Diagnostics).Id);

        var entry = index.Resolve("Game")!;
        Assert.Equal("Assets/Game", entry.Folder);
        Assert.Equal(GuidA, entry.Guid);
        Assert.False(entry.IsTestAssembly);
    }

    [Fact]
    public void Asmdef_without_meta_has_no_guid()
    {
        var index = new DefinitionIndex(new InventoryBuilder().Asmdef("Assets/A/A.asmdef", "A").Build());
        Assert.Null(index.Resolve("A")!.Guid);
    }
}
