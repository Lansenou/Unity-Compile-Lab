using Ucl.Core.Model;
using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

/// <summary>
/// Rules taken from UnityCsReference in session 3 (docs/architecture.md): auto-referenced uGUI, test runner references,
/// testables, one precompiled DLL per file name, code-gen names.
/// </summary>
public class GraphUnityRulesTests
{
    private static InventoryBuilder Ugui() => new InventoryBuilder()
        .Package("com.unity.ugui", "2.0.0")
        .Asmdef("Packages/com.unity.ugui/Runtime/UnityEngine.UI.asmdef", "UnityEngine.UI")
        .Asmdef("Packages/com.unity.ugui/Editor/UnityEditor.UI.asmdef", "UnityEditor.UI", "\"references\": [\"UnityEngine.UI\"], \"includePlatforms\": [\"Editor\"]")
        .Scripts("Packages/com.unity.ugui/Runtime/A.cs", "Packages/com.unity.ugui/Editor/B.cs");

    [Fact]
    public void UGUI_is_added_to_every_asmdef_assembly_and_UnityEditor_UI_only_in_the_Editor()
    {
        var p = Ugui()
            .Asmdef("Assets/Input/Input.asmdef", "Input", "\"references\": [\"Unity.ugui\"]")
            .Scripts("Assets/Input/I.cs");
        var editor = p.Editor();
        Assert.Equal(["UnityEditor.UI", "UnityEngine.UI"], editor.Find("Input")!.References);
        var player = p.Player();
        Assert.Equal(["UnityEngine.UI"], player.Find("Input")!.References);
        Assert.Empty(player.Find("UnityEngine.UI")!.References);
    }

    [Fact]
    public void UGUI_skips_noEngineReferences_codegen_and_test_runner_assemblies()
    {
        var g = Ugui()
            .Asmdef("Assets/Math/Math.asmdef", "Math", "\"noEngineReferences\": true")
            .Asmdef("Assets/Gen/Unity.Weaver.CodeGen.asmdef", "Unity.Weaver.CodeGen")
            .Asmdef("Assets/Runner/UnityEngine.TestRunner.asmdef", "UnityEngine.TestRunner")
            .Scripts("Assets/Math/M.cs", "Assets/Gen/G.cs", "Assets/Runner/R.cs")
            .Editor();
        Assert.Empty(g.Find("Math")!.References);
        Assert.DoesNotContain("UnityEngine.UI", g.Find("Unity.Weaver.CodeGen")!.References);
        Assert.Empty(g.Find("UnityEngine.TestRunner")!.References);
    }

    [Fact]
    public void Without_uGUI_nothing_is_added()
    {
        var g = new InventoryBuilder().Asmdef("Assets/A/A.asmdef", "A").Scripts("Assets/A/a.cs").Editor();
        Assert.Empty(g.Find("A")!.References);
    }

    private static InventoryBuilder TestFramework() => new InventoryBuilder()
        .Package("com.unity.test-framework", "1.4.5", "embedded")
        .Asmdef("Packages/com.unity.test-framework/Runtime/UnityEngine.TestRunner.asmdef", "UnityEngine.TestRunner",
            "\"autoReferenced\": false, \"defineConstraints\": [\"UNITY_TESTS_FRAMEWORK\"], \"versionDefines\": [{ \"name\": \"com.unity.test-framework\", \"expression\": \"\", \"define\": \"UNITY_TESTS_FRAMEWORK\" }]")
        .Asmdef("Packages/com.unity.test-framework/Editor/UnityEditor.TestRunner.asmdef", "UnityEditor.TestRunner",
            "\"references\": [\"UnityEngine.TestRunner\"], \"includePlatforms\": [\"Editor\"], \"autoReferenced\": false, \"defineConstraints\": [\"UNITY_TESTS_FRAMEWORK\"], \"versionDefines\": [{ \"name\": \"com.unity.test-framework\", \"expression\": \"\", \"define\": \"UNITY_TESTS_FRAMEWORK\" }]")
        .Plugin("Packages/com.unity.ext.nunit/nunit.framework.dll", Metas.Plugin(Metas.AnyPlatformData(), explicitlyReferenced: true))
        .Scripts("Packages/com.unity.test-framework/Runtime/R.cs", "Packages/com.unity.test-framework/Editor/E.cs");

    [Fact]
    public void Editor_only_assemblies_get_the_test_runners_and_nunit()
    {
        var g = TestFramework()
            .Asmdef("Assets/Tools/Tools.asmdef", "Tools", "\"includePlatforms\": [\"Editor\"]")
            .Asmdef("Assets/Game/Game.asmdef", "Game")
            .Scripts("Assets/Tools/T.cs", "Assets/Game/G.cs", "Assets/Editor/E.cs")
            .Editor();
        Assert.Equal(["UnityEditor.TestRunner", "UnityEngine.TestRunner"], g.Find("Tools")!.References);
        Assert.Equal(["Packages/com.unity.ext.nunit/nunit.framework.dll"], g.Find("Tools")!.PrecompiledReferences);
        Assert.Empty(g.Find("Game")!.References);
        Assert.Empty(g.Find("Game")!.PrecompiledReferences);
        Assert.Contains("UnityEditor.TestRunner", g.Find("Assembly-CSharp-Editor")!.References);
        Assert.Equal(["Packages/com.unity.ext.nunit/nunit.framework.dll"], g.Find("Assembly-CSharp-Editor")!.PrecompiledReferences);
        Assert.Equal(["UnityEngine.TestRunner"], g.Find("UnityEditor.TestRunner")!.References);
    }

    [Fact]
    public void PlayMode_tests_for_all_assemblies_extends_them_to_runtime_assemblies()
    {
        var g = TestFramework()
            .Settings("PlayerSettings:\n  playModeTestRunnerEnabled: 1\n")
            .Asmdef("Assets/Game/Game.asmdef", "Game")
            .Scripts("Assets/Game/G.cs")
            .Editor();
        Assert.Equal(["UnityEditor.TestRunner", "UnityEngine.TestRunner"], g.Find("Game")!.References);
        Assert.Equal(["Packages/com.unity.ext.nunit/nunit.framework.dll"], g.Find("Game")!.PrecompiledReferences);
    }

    [Fact]
    public void Test_framework_assemblies_leave_a_player_unless_tests_are_included()
    {
        var p = TestFramework()
            .Asmdef("Assets/Fixture/Fixture.asmdef", "Fixture", "\"defineConstraints\": [\"UNITY_TESTS_FRAMEWORK\"], \"versionDefines\": [{ \"name\": \"com.unity.test-framework\", \"expression\": \"\", \"define\": \"UNITY_TESTS_FRAMEWORK\" }]")
            .Scripts("Assets/Fixture/F.cs");
        Assert.NotNull(p.Editor().Find("Fixture"));
        Assert.Contains("--include-tests", p.Player().Excluded["Fixture"]);
        Assert.Contains("--include-tests", p.Player().Excluded["UnityEngine.TestRunner"]);
        var included = p.Graph(Cells.Player(includeTests: true));
        Assert.NotNull(included.Find("Fixture"));
        Assert.NotNull(included.Find("UnityEngine.TestRunner"));
    }

    [Fact]
    public void Package_tests_compile_only_when_the_package_is_embedded_or_testable()
    {
        const string Tests = "\"defineConstraints\": [\"UNITY_INCLUDE_TESTS\"], \"includePlatforms\": [\"Editor\"]";
        var p = TestFramework()
            .Package("com.example.cached", "1.0.0")
            .Package("com.example.listed", "1.0.0")
            .Package("com.example.local", "1.0.0", "embedded")
            .Asmdef("Packages/com.example.cached/Tests/Cached.Tests.asmdef", "Cached.Tests", Tests)
            .Asmdef("Packages/com.example.listed/Tests/Listed.Tests.asmdef", "Listed.Tests", Tests)
            .Asmdef("Packages/com.example.local/Tests/Local.Tests.asmdef", "Local.Tests", Tests)
            .Asmdef("Packages/com.example.cached/Legacy/Legacy.Tests.asmdef", "Legacy.Tests", "\"optionalUnityReferences\": [\"TestAssemblies\"]")
            .Asmdef("Assets/Tests/Game.Tests.asmdef", "Game.Tests", Tests)
            .Scripts("Packages/com.example.cached/Tests/A.cs", "Packages/com.example.listed/Tests/B.cs", "Packages/com.example.local/Tests/C.cs",
                "Packages/com.example.cached/Legacy/D.cs", "Assets/Tests/E.cs")
            .Testables("com.example.listed");
        var g = p.Editor();
        Assert.Contains("not testable", g.Excluded["Cached.Tests"]);
        Assert.Contains("not testable", g.Excluded["Legacy.Tests"]);
        Assert.NotNull(g.Find("Listed.Tests"));
        Assert.NotNull(g.Find("Local.Tests"));
        Assert.NotNull(g.Find("Game.Tests"));
    }

    [Fact]
    public void Legacy_test_assemblies_reference_the_runners_and_only_nunit()
    {
        var g = TestFramework()
            .Plugin("Assets/Plugins/Vendor.dll")
            .Asmdef("Assets/Tests/Old.asmdef", "Old", "\"optionalUnityReferences\": [\"TestAssemblies\"]")
            .Scripts("Assets/Tests/T.cs")
            .Editor();
        Assert.Equal(["UnityEditor.TestRunner", "UnityEngine.TestRunner"], g.Find("Old")!.References);
        Assert.Equal(["Packages/com.unity.ext.nunit/nunit.framework.dll"], g.Find("Old")!.PrecompiledReferences);
    }

    [Fact]
    public void One_precompiled_DLL_per_file_name_the_highest_version_wins()
    {
        const string Unsafe = "System.Runtime.CompilerServices.Unsafe.dll";
        var g = new InventoryBuilder()
            .Plugin($"Packages/a/{Unsafe}", Metas.Plugin(Metas.AnyPlatformData(), explicitlyReferenced: true))
            .Plugin($"Packages/b/{Unsafe}")
            .Plugin($"Packages/c/{Unsafe}")
            .PluginVersion($"Packages/a/{Unsafe}", "4.0.4.1")
            .PluginVersion($"Packages/b/{Unsafe}", "6.0.1.0")
            .PluginVersion($"Packages/c/{Unsafe}", "6.0.0.0")
            .Asmdef("Assets/Pipe/Pipe.asmdef", "Pipe", $"\"overrideReferences\": true, \"precompiledReferences\": [\"{Unsafe}\"]")
            .Scripts("Assets/A.cs", "Assets/Pipe/P.cs")
            .Editor();
        Assert.Equal([$"Packages/b/{Unsafe}"], g.Find("Assembly-CSharp")!.PrecompiledReferences);
        Assert.Equal([$"Packages/b/{Unsafe}"], g.Find("Pipe")!.PrecompiledReferences);
        Assert.Equal([$"Packages/a/{Unsafe}", $"Packages/c/{Unsafe}"],
            g.Diagnostics.Where(d => d.Id == ProblemIds.ShadowedPrecompiledReference).Select(d => d.File!).ToList());
        Assert.All(g.Diagnostics, d => Assert.Equal(Severity.Info, d.Severity));
    }

    [Fact]
    public void Same_name_DLLs_without_versions_keep_the_first_path()
    {
        var g = new InventoryBuilder()
            .Plugin("Packages/b/X.dll")
            .Plugin("Assets/Plugins/X.dll")
            .Scripts("Assets/A.cs")
            .Editor();
        Assert.Equal(["Assets/Plugins/X.dll"], g.Find("Assembly-CSharp")!.PrecompiledReferences);
        Assert.Contains("no version", g.Diagnostics.Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public void E02_UNITY_EDITOR_ONLY_COMPILATION_only_for_Editor_only_assemblies()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Tools/Tools.asmdef", "Tools", "\"includePlatforms\": [\"Editor\"]")
            .Asmdef("Assets/Game/Game.asmdef", "Game")
            .Scripts("Assets/Tools/T.cs", "Assets/Game/G.cs", "Assets/A.cs", "Assets/Editor/E.cs")
            .Editor();
        Assert.Equal("E02", g.Find("Tools")!.Defines.Reasons[BuiltInDefines.EditorOnlyCompilation]);
        Assert.Equal("E02", g.Find("Assembly-CSharp-Editor")!.Defines.Reasons[BuiltInDefines.EditorOnlyCompilation]);
        Assert.False(g.Find("Game")!.Defines.Contains(BuiltInDefines.EditorOnlyCompilation));
        Assert.False(g.Find("Assembly-CSharp")!.Defines.Contains(BuiltInDefines.EditorOnlyCompilation));
    }

    [Fact]
    public void Analyzer_owned_through_an_asmref_folder_belongs_to_the_asmref_target()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Core/Core.asmdef", "Core", "\"autoReferenced\": false")
            .Asmref("Assets/Extra/Extra.asmref", "Core")
            .Plugin("Assets/Extra/Analyzers/X.dll", Metas.Analyzer())
            .Asmdef("Assets/Other/Other.asmdef", "Other", "\"autoReferenced\": false")
            .Scripts("Assets/Core/C.cs", "Assets/Other/O.cs", "Assets/A.cs")
            .Editor();
        Assert.Equal(["Assets/Extra/Analyzers/X.dll"], g.Find("Core")!.Analyzers);
        Assert.Empty(g.Find("Other")!.Analyzers);
        Assert.Empty(g.Find("Assembly-CSharp")!.Analyzers);
    }

    [Theory]
    [InlineData("Unity.Burst.CodeGen", true, true)]
    [InlineData("unity.entities.compiler", true, true)]
    [InlineData("Unity.Burst.CodeGen.Tests", false, true)]
    [InlineData("Unity.Entities.Compiler.Client", false, true)]
    [InlineData("Game.CodeGen", false, false)]
    [InlineData("Unity.Burst", false, false)]
    public void Code_gen_assembly_names(string name, bool codeGen, bool pipeline)
    {
        Assert.Equal(codeGen, CodeGenAssemblies.IsCodeGen(name));
        Assert.Equal(pipeline, CodeGenAssemblies.UsesCompilationPipeline(name));
    }
}
