using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Parsing;

namespace Ucl.Core.Tests;

/// <summary>Cell membership: platforms, Editor assemblies in player cells, defineConstraints, engine references.</summary>
public class GraphPlatformTests
{
    private static InventoryBuilder Project(string asmdefExtra) => new InventoryBuilder()
        .Asmdef("Assets/Mod/Mod.asmdef", "Mod", asmdefExtra)
        .Scripts("Assets/Mod/M.cs", "Assets/A.cs");

    [Theory]
    [InlineData(BuildPlatform.StandaloneWindows64, "WindowsStandalone64")]
    [InlineData(BuildPlatform.StandaloneOSX, "macOSStandalone")]
    [InlineData(BuildPlatform.StandaloneLinux64, "LinuxStandalone64")]
    [InlineData(BuildPlatform.iOS, "iOS")]
    [InlineData(BuildPlatform.Android, "Android")]
    [InlineData(BuildPlatform.WebGL, "WebGL")]
    public void Player_cells_filter_by_the_asmdef_platform_name(BuildPlatform platform, string asmdefName)
    {
        var only = Project($"\"includePlatforms\": [\"{asmdefName}\"]");
        foreach (var p in Enum.GetValues<BuildPlatform>())
        {
            Assert.Equal(p == platform, only.Player(p).Find("Mod") is not null);
        }

        var except = Project($"\"excludePlatforms\": [\"{asmdefName}\"]");
        foreach (var p in Enum.GetValues<BuildPlatform>())
        {
            Assert.Equal(p != platform, except.Player(p).Find("Mod") is not null);
        }
    }

    [Fact]
    public void Platform_names_match_case_insensitively()
    {
        Assert.NotNull(Project("\"includePlatforms\": [\"android\"]").Player(BuildPlatform.Android).Find("Mod"));
        Assert.NotNull(Project("\"includePlatforms\": [\"editor\"]").Editor().Find("Mod"));
    }

    [Fact]
    public void Excluded_reason_names_the_platform()
    {
        var g = Project("\"includePlatforms\": [\"Android\"]").Player(BuildPlatform.iOS);
        Assert.Equal("platform iOS is not compatible with Assets/Mod/Mod.asmdef", g.Excluded["Mod"]);
    }

    [Fact]
    public void Editor_cells_filter_only_by_the_Editor_platform()
    {
        // An Android-only asmdef is not compiled by the Editor even when Android is the active build target.
        var androidOnly = Project("\"includePlatforms\": [\"Android\"]");
        Assert.Null(androidOnly.Editor(BuildPlatform.Android).Find("Mod"));
        Assert.Equal("platform Editor is not compatible with Assets/Mod/Mod.asmdef", androidOnly.Editor(BuildPlatform.Android).Excluded["Mod"]);

        // Excluding the active build target does not matter in the Editor.
        Assert.NotNull(Project("\"excludePlatforms\": [\"Android\"]").Editor(BuildPlatform.Android).Find("Mod"));

        Assert.Null(Project("\"excludePlatforms\": [\"Editor\"]").Editor().Find("Mod"));
        Assert.NotNull(Project("\"excludePlatforms\": [\"Editor\"]").Player().Find("Mod"));
        Assert.NotNull(Project("\"includePlatforms\": [\"Editor\", \"Android\"]").Editor(BuildPlatform.WebGL).Find("Mod"));
    }

    [Fact]
    public void Editor_only_asmdef_is_excluded_from_players_and_flagged_in_editor()
    {
        var p = Project("\"includePlatforms\": [\"Editor\"]");
        var editor = p.Editor();
        Assert.True(editor.Find("Mod")!.IsEditorOnly);
        Assert.Null(p.Player().Find("Mod"));
        Assert.Contains("Mod", p.Player().Excluded.Keys);
        Assert.False(Project("\"includePlatforms\": [\"Editor\", \"Android\"]").Editor().Find("Mod")!.IsEditorOnly);
        Assert.False(Project(string.Empty).Editor().Find("Mod")!.IsEditorOnly);
    }

    [Fact]
    public void Unknown_platform_names_never_match()
    {
        var p = Project("\"includePlatforms\": [\"PS5\", \"tvOS\", \"VisionOS\"]");
        Assert.Null(p.Editor().Find("Mod"));
        Assert.All(Enum.GetValues<BuildPlatform>(), pl => Assert.Null(p.Player(pl).Find("Mod")));
        Assert.NotNull(Project("\"excludePlatforms\": [\"PS5\"]").Player().Find("Mod"));
    }

    [Fact]
    public void Player_cells_contain_neither_Editor_predefined_assembly()
    {
        var g = new InventoryBuilder()
            .Scripts("Assets/A.cs", "Assets/Editor/E.cs", "Assets/Plugins/P.cs", "Assets/Plugins/Editor/PE.cs")
            .Player(BuildPlatform.Android);
        Assert.Equal(["Assembly-CSharp-firstpass", "Assembly-CSharp"], g.Assemblies.Select(a => a.Name));
        Assert.Equal("Editor scripts are not part of a player build", g.Excluded["Assembly-CSharp-Editor"]);
        Assert.Equal("Editor scripts are not part of a player build", g.Excluded["Assembly-CSharp-Editor-firstpass"]);
    }

    [Fact]
    public void Engine_references_by_target_and_noEngineReferences()
    {
        var p = new InventoryBuilder()
            .Asmdef("Assets/Pure/Pure.asmdef", "Pure", "\"noEngineReferences\": true")
            .Asmdef("Assets/Mod/Mod.asmdef", "Mod")
            .Scripts("Assets/A.cs");
        var editor = p.Editor();
        Assert.Equal(EngineReferences.None, editor.Find("Pure")!.Engine);
        Assert.Equal(EngineReferences.RuntimeAndEditor, editor.Find("Mod")!.Engine);
        Assert.Equal(EngineReferences.RuntimeAndEditor, editor.Find("Assembly-CSharp")!.Engine);
        var player = p.Player();
        Assert.Equal(EngineReferences.None, player.Find("Pure")!.Engine);
        Assert.Equal(EngineReferences.Runtime, player.Find("Mod")!.Engine);
        Assert.Equal(EngineReferences.Runtime, player.Find("Assembly-CSharp")!.Engine);
    }

    [Fact]
    public void Define_constraint_true_and_false()
    {
        Assert.NotNull(Project("\"defineConstraints\": [\"UNITY_EDITOR\"]").Editor().Find("Mod"));
        var g = Project("\"defineConstraints\": [\"UNITY_EDITOR\"]").Player();
        Assert.Null(g.Find("Mod"));
        Assert.Equal("defineConstraints [UNITY_EDITOR] not satisfied", g.Excluded["Mod"]);
        Assert.DoesNotContain("Mod", g.Find("Assembly-CSharp")!.References);
    }

    [Fact]
    public void Define_constraint_with_or()
    {
        var p = Project("\"defineConstraints\": [\"UNITY_ANDROID || UNITY_IOS\"]");
        Assert.NotNull(p.Player(BuildPlatform.Android).Find("Mod"));
        Assert.NotNull(p.Player(BuildPlatform.iOS).Find("Mod"));
        Assert.Null(p.Player(BuildPlatform.WebGL).Find("Mod"));
    }

    [Fact]
    public void Define_constraint_with_negation()
    {
        var p = Project("\"defineConstraints\": [\"!UNITY_WEBGL\"]");
        Assert.NotNull(p.Player(BuildPlatform.Android).Find("Mod"));
        Assert.Null(p.Player(BuildPlatform.WebGL).Find("Mod"));
    }

    [Fact]
    public void Define_constraints_all_must_hold_and_see_project_symbols()
    {
        var p = Project("\"defineConstraints\": [\"MY_GAME\", \"!NOT_DEFINED\", \"\"]")
            .Settings(ProjectSettingsParserTests.Unity6Asset);
        Assert.NotNull(p.Player(BuildPlatform.StandaloneWindows64).Find("Mod"));
        Assert.Null(p.Player(BuildPlatform.Android).Find("Mod"));
    }

    [Fact]
    public void Define_constraints_see_the_assembly_response_file_defines()
    {
        var p = Project("\"defineConstraints\": [\"FROM_RSP\"]").Rsp("Assets/Mod/csc.rsp", "-define:FROM_RSP");
        Assert.NotNull(p.Editor().Find("Mod"));
    }

    [Fact]
    public void Define_constraints_see_version_defines()
    {
        var p = Project("\"defineConstraints\": [\"HAS_FOO\"], \"versionDefines\": [{ \"name\": \"com.foo\", \"expression\": \"1.0\", \"define\": \"HAS_FOO\" }]");
        Assert.Null(p.Editor().Find("Mod"));
        Assert.NotNull(p.Package("com.foo", "1.2.0").Editor().Find("Mod"));
    }

    [Fact]
    public void D60_UNITY_INCLUDE_TESTS_in_define_constraints()
    {
        var p = Project("\"defineConstraints\": [\"UNITY_INCLUDE_TESTS\"]");
        Assert.Null(p.Editor().Find("Mod"));
        p.Package("com.unity.test-framework", "1.4.5");
        var g = p.Editor();
        Assert.NotNull(g.Find("Mod"));
        Assert.False(g.Find("Mod")!.Defines.Contains("UNITY_INCLUDE_TESTS"));
        Assert.False(g.BaseDefines.Contains("UNITY_INCLUDE_TESTS"));
        Assert.Null(p.Player().Find("Mod"));
        Assert.NotNull(p.Graph(Cells.Player(includeTests: true)).Find("Mod"));
    }

    [Fact]
    public void D60_test_assemblies_need_UNITY_INCLUDE_TESTS()
    {
        var p = new InventoryBuilder().Asmdef("Assets/Tests/Tests.asmdef", "Tests", "\"optionalUnityReferences\": [\"TestAssemblies\"]");
        Assert.Null(p.Editor().Find("Tests"));
        Assert.Contains("UNITY_INCLUDE_TESTS", p.Editor().Excluded["Tests"]);
        p.Package("com.unity.test-framework", "1.4.5");
        Assert.NotNull(p.Editor().Find("Tests"));
        Assert.Null(p.Player().Find("Tests"));
    }

    [Fact]
    public void Graph_reports_cell_wide_facts()
    {
        var g = new InventoryBuilder()
            .Package("com.unity.modules.physics", "1.0.0")
            .Package("com.unity.modules.audio", "1.0.0")
            .Package("com.unity.ugui", "2.0.0")
            .Settings(new ProjectSettingsData { ApiCompatibilityLevel = 3 })
            .Player(BuildPlatform.Android);
        Assert.Equal(["audio", "physics"], g.EnabledModules);
        Assert.True(g.NetFramework);
        Assert.Equal(Cells.Player(BuildPlatform.Android), g.Cell);
        Assert.True(g.BaseDefines.Contains("UNITY_ANDROID"));
        Assert.Null(g.Find("Assembly-CSharp"));
        Assert.False(new InventoryBuilder().Player().NetFramework);
    }
}
