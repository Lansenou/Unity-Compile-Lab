using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

/// <summary>Precompiled DLLs: plugin import settings, Auto Reference, overrideReferences, plugin constraints.</summary>
public class GraphPluginTests
{
    private const string Dll = "Assets/Plugins/Lib.dll";

    private static InventoryBuilder WithPlugin(string? meta) => new InventoryBuilder()
        .Plugin(Dll, meta)
        .Asmdef("Assets/Mod/Mod.asmdef", "Mod")
        .Scripts("Assets/A.cs", "Assets/Editor/E.cs");

    private static bool Referenced(Graph.AssemblyGraph g, string assembly, string dll = Dll) =>
        g.Find(assembly)!.PrecompiledReferences.Contains(dll);

    [Theory]
    [InlineData(1, 1, 0, false, true)]
    [InlineData(1, 0, 0, true, true)]
    [InlineData(0, 0, 1, true, false)]
    public void Map_importer_controls_references_on_all_assembly_kinds(int any, int excludeEditor, int editor, bool inEditor, bool inPlayer)
    {
        var meta = $"PluginImporter:\n  serializedVersion: 3\n  platformData:\n    Any:\n      enabled: {any}\n      settings:\n        Exclude Editor: {excludeEditor}\n    Editor:\n      enabled: {editor}\n";
        var inventory = WithPlugin(meta);
        Assert.All(inventory.Editor().Assemblies, a => Assert.Equal(inEditor, a.PrecompiledReferences.Contains(Dll)));
        Assert.All(inventory.Player().Assemblies, a => Assert.Equal(inPlayer, a.PrecompiledReferences.Contains(Dll)));
    }

    [Fact]
    public void Dll_without_meta_is_referenced_everywhere()
    {
        var p = WithPlugin(null);
        foreach (var platform in Enum.GetValues<BuildPlatform>())
        {
            Assert.True(Referenced(p.Player(platform), "Assembly-CSharp"));
            Assert.True(Referenced(p.Player(platform), "Mod"));
        }

        var editor = p.Editor();
        Assert.True(Referenced(editor, "Assembly-CSharp-Editor"));
        Assert.True(Referenced(editor, "Mod"));
    }

    [Fact]
    public void Dll_with_empty_platform_data_is_referenced_everywhere()
    {
        var p = WithPlugin(Metas.Plugin(string.Empty));
        Assert.True(Referenced(p.Player(BuildPlatform.iOS), "Mod"));
        Assert.True(Referenced(p.Editor(), "Mod"));
    }

    [Fact]
    public void Any_platform_with_Exclude_Win64()
    {
        var p = WithPlugin(Metas.Plugin(Metas.AnyPlatformData("Win64")));
        Assert.False(Referenced(p.Player(BuildPlatform.StandaloneWindows64), "Assembly-CSharp"));
        Assert.True(Referenced(p.Player(BuildPlatform.StandaloneOSX), "Assembly-CSharp"));
        Assert.True(Referenced(p.Player(BuildPlatform.StandaloneLinux64), "Assembly-CSharp"));
        Assert.True(Referenced(p.Player(BuildPlatform.Android), "Mod"));
        // Editor cells use the Editor key, not the active build target.
        Assert.True(Referenced(p.Editor(BuildPlatform.StandaloneWindows64), "Assembly-CSharp"));
    }

    [Fact]
    public void Any_platform_with_Exclude_Editor()
    {
        var p = WithPlugin(Metas.Plugin(Metas.AnyPlatformData("Editor")));
        Assert.False(Referenced(p.Editor(), "Assembly-CSharp"));
        Assert.False(Referenced(p.Editor(), "Mod"));
        Assert.True(Referenced(p.Player(), "Assembly-CSharp"));
    }

    [Fact]
    public void Explicit_per_platform_enable()
    {
        var p = WithPlugin(Metas.Plugin(Metas.ExplicitPlatformData(("Standalone", "Win64"), ("Android", "Android"))));
        Assert.True(Referenced(p.Player(BuildPlatform.StandaloneWindows64), "Assembly-CSharp"));
        Assert.True(Referenced(p.Player(BuildPlatform.Android), "Assembly-CSharp"));
        Assert.False(Referenced(p.Player(BuildPlatform.StandaloneLinux64), "Assembly-CSharp"));
        Assert.False(Referenced(p.Player(BuildPlatform.iOS), "Assembly-CSharp"));
        Assert.False(Referenced(p.Editor(BuildPlatform.StandaloneWindows64), "Assembly-CSharp"));
    }

    [Fact]
    public void Editor_key_for_editor_cells()
    {
        var p = WithPlugin(Metas.Plugin(Metas.ExplicitPlatformData(("Editor", "Editor"))));
        var editor = p.Editor(BuildPlatform.Android);
        Assert.True(Referenced(editor, "Assembly-CSharp"));
        Assert.True(Referenced(editor, "Assembly-CSharp-Editor"));
        Assert.True(Referenced(editor, "Mod"));
        Assert.False(Referenced(p.Player(BuildPlatform.Android), "Assembly-CSharp"));
        Assert.False(Referenced(p.Player(BuildPlatform.StandaloneWindows64), "Assembly-CSharp"));
    }

    [Fact]
    public void Explicitly_referenced_dll_is_seen_only_through_overrideReferences()
    {
        var p = new InventoryBuilder()
            .Plugin("Assets/Plugins/Newtonsoft.Json.dll", Metas.Plugin(Metas.AnyPlatformData(), explicitlyReferenced: true))
            .Asmdef("Assets/Plain/Plain.asmdef", "Plain")
            .Asmdef("Assets/Uses/Uses.asmdef", "Uses", "\"overrideReferences\": true, \"precompiledReferences\": [\"newtonsoft.json.dll\"]")
            .Asmdef("Assets/Listed/Listed.asmdef", "Listed", "\"overrideReferences\": false, \"precompiledReferences\": [\"Newtonsoft.Json.dll\"]")
            .Scripts("Assets/A.cs");
        var g = p.Editor();
        Assert.Equal(["Assets/Plugins/Newtonsoft.Json.dll"], g.Find("Uses")!.PrecompiledReferences);
        Assert.Empty(g.Find("Plain")!.PrecompiledReferences);
        Assert.Empty(g.Find("Listed")!.PrecompiledReferences);
        Assert.Empty(g.Find("Assembly-CSharp")!.PrecompiledReferences);
        Assert.Empty(g.Diagnostics);
    }

    [Fact]
    public void OverrideReferences_sees_only_listed_dlls()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Plugins/A.dll")
            .Plugin("Assets/Plugins/B.dll")
            .Asmdef("Assets/Mod/Mod.asmdef", "Mod", "\"overrideReferences\": true, \"precompiledReferences\": [\"B.dll\"]")
            .Asmdef("Assets/None/None.asmdef", "None", "\"overrideReferences\": true")
            .Asmdef("Assets/All/All.asmdef", "All")
            .Editor();
        Assert.Equal(["Assets/Plugins/B.dll"], g.Find("Mod")!.PrecompiledReferences);
        Assert.Empty(g.Find("None")!.PrecompiledReferences);
        Assert.Equal(["Assets/Plugins/A.dll", "Assets/Plugins/B.dll"], g.Find("All")!.PrecompiledReferences);
    }

    [Fact]
    public void Listed_dll_that_is_incompatible_with_the_cell_is_not_referenced_and_not_an_error()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Plugins/Win.dll", Metas.Plugin(Metas.ExplicitPlatformData(("Standalone", "Win64"))))
            .Asmdef("Assets/Mod/Mod.asmdef", "Mod", "\"overrideReferences\": true, \"precompiledReferences\": [\"Win.dll\"]")
            .Player(BuildPlatform.Android);
        Assert.Empty(g.Find("Mod")!.PrecompiledReferences);
        Assert.Empty(g.Diagnostics);
    }

    [Fact]
    public void Missing_precompiled_reference_is_UCL1004_info_and_skipped()
    {
        // Unity skips a listed name it cannot find without a message; the assembly still compiles (G3).
        var g = new InventoryBuilder()
            .Plugin("Assets/Plugins/Present.dll")
            .Asmdef("Assets/Mod/Mod.asmdef", "Mod", "\"overrideReferences\": true, \"precompiledReferences\": [\"Present.dll\", \"Absent.dll\"]")
            .Editor();
        var d = Assert.Single(g.Diagnostics);
        Assert.Equal(ProblemIds.MissingPrecompiledReference, d.Id);
        Assert.Equal("UCL1004", d.Id);
        Assert.Equal(Severity.Info, d.Severity);
        Assert.Equal("Mod", d.Assembly);
        Assert.Equal("Assets/Mod/Mod.asmdef", d.File);
        Assert.Contains("'Absent.dll'", d.Message, StringComparison.Ordinal);
        Assert.Contains("not in the project", d.Message, StringComparison.Ordinal);
        Assert.Equal(["Assets/Plugins/Present.dll"], g.Find("Mod")!.PrecompiledReferences);
        Assert.Equal(0, ExitCodes.Compute([], g.Diagnostics));
    }

    [Fact]
    public void Native_plugins_are_never_references()
    {
        var b = new InventoryBuilder()
            .Scripts("Assets/A.cs", "Assets/Mod/M.cs")
            .Asmdef("Assets/Mod/Mod.asmdef", "Mod", "\"overrideReferences\": true, \"precompiledReferences\": [\"native.dll\"]");
        var inventory = b.Build() with { NativePlugins = ["Assets/Plugins/x86_64/native.dll"] };
        var g = AssemblyGraphBuilder.Build(inventory, Cells.Editor());
        Assert.Empty(g.Find("Assembly-CSharp")!.PrecompiledReferences);
        Assert.Empty(g.Find("Mod")!.PrecompiledReferences);
        var d = Assert.Single(g.Diagnostics);
        Assert.Equal(Severity.Info, d.Severity);
        Assert.Contains("native DLL", d.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plugin_define_constraints()
    {
        var p = WithPlugin(Metas.Plugin(Metas.AnyPlatformData(), defineConstraints: ["UNITY_ANDROID || UNITY_IOS", "!UNITY_EDITOR"]));
        Assert.True(Referenced(p.Player(BuildPlatform.Android), "Assembly-CSharp"));
        Assert.True(Referenced(p.Player(BuildPlatform.iOS), "Mod"));
        Assert.False(Referenced(p.Player(BuildPlatform.WebGL), "Assembly-CSharp"));
        Assert.False(Referenced(p.Editor(BuildPlatform.Android), "Assembly-CSharp"));
    }

    [Fact]
    public void Plugin_define_constraints_see_project_symbols()
    {
        var p = WithPlugin(Metas.Plugin(Metas.AnyPlatformData(), defineConstraints: ["STEAM"]))
            .Settings(ProjectSettingsParserTests.Unity6Asset);
        Assert.True(Referenced(p.Player(BuildPlatform.StandaloneWindows64), "Assembly-CSharp"));
        Assert.False(Referenced(p.Player(BuildPlatform.Android), "Assembly-CSharp"));
    }

    [Fact]
    public void D60_plugin_define_constraint_UNITY_INCLUDE_TESTS()
    {
        // com.unity.ext.nunit ships nunit.framework.dll with Auto Reference off and defineConstraints UNITY_INCLUDE_TESTS.
        const string NUnit = "Packages/com.unity.ext.nunit/net40/unity-custom/nunit.framework.dll";
        var p = new InventoryBuilder()
            .Package("com.unity.test-framework", "1.4.5")
            .Package("com.unity.ext.nunit", "2.0.3")
            .Plugin(NUnit, Metas.Plugin(Metas.AnyPlatformData(), explicitlyReferenced: true, defineConstraints: ["UNITY_INCLUDE_TESTS"]))
            .Asmdef("Assets/Tests/Tests.asmdef", "Tests",
                "\"optionalUnityReferences\": [\"TestAssemblies\"], \"overrideReferences\": true, \"precompiledReferences\": [\"nunit.framework.dll\"]");
        var g = p.Editor();
        Assert.Equal([NUnit], g.Find("Tests")!.PrecompiledReferences);
        Assert.Empty(g.Diagnostics);
    }

    [Fact]
    public void Precompiled_references_include_rsp_references_sorted_and_distinct()
    {
        var g = new InventoryBuilder()
            .Plugin("Assets/Plugins/Z.dll")
            .Rsp("Assets/csc.rsp", "-r:Assets/Libs/A.dll -r:Assets/Plugins/Z.dll")
            .Scripts("Assets/A.cs")
            .Editor();
        Assert.Equal(["Assets/Libs/A.dll", "Assets/Plugins/Z.dll"], g.Find("Assembly-CSharp")!.PrecompiledReferences);
    }

    [Fact]
    public void Package_plugins_work_like_asset_plugins()
    {
        var g = new InventoryBuilder()
            .Package("com.foo", "1.0.0")
            .Plugin("Packages/com.foo/Plugins/Foo.Native.dll", Metas.Plugin(Metas.AnyPlatformData("Editor")))
            .Scripts("Assets/A.cs")
            .Player(BuildPlatform.WebGL);
        Assert.Equal(["Packages/com.foo/Plugins/Foo.Native.dll"], g.Find("Assembly-CSharp")!.PrecompiledReferences);
    }

    [Fact]
    public void Unique_auto_referenced_package_plugin_is_independent_of_package_testability()
    {
        const string dll = "Packages/com.example.cached/Tests/Plugins/System.IO.Hashing.dll";
        var g = new InventoryBuilder()
            .Package("com.example.cached", "1.0.0")
            .Asmdef("Packages/com.example.cached/Tests/Cached.Tests.asmdef", "Cached.Tests",
                "\"defineConstraints\": [\"UNITY_INCLUDE_TESTS\"]")
            .Plugin(dll, Metas.Plugin(Metas.AnyPlatformData()))
            .Asmdef("Assets/Game/Game.asmdef", "Game")
            .Scripts("Packages/com.example.cached/Tests/T.cs", "Assets/A.cs", "Assets/Game/G.cs")
            .Editor();
        Assert.Equal([dll], g.Find("Assembly-CSharp")!.PrecompiledReferences);
        Assert.Equal([dll], g.Find("Game")!.PrecompiledReferences);
        Assert.Null(g.Find("Cached.Tests"));
    }
}
