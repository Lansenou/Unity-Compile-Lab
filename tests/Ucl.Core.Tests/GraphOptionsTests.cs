using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Parsing;

namespace Ucl.Core.Tests;

/// <summary>Compiler options (C01-C08) and response files (D51, D52, UCL1020).</summary>
public class GraphOptionsTests
{
    private static InventoryBuilder Basic() => new InventoryBuilder()
        .Asmdef("Assets/Mod/Mod.asmdef", "Mod")
        .Scripts("Assets/A.cs", "Assets/Mod/M.cs");

    private static ProjectSettingsData Args(string group, params string[] args) =>
        new() { AdditionalCompilerArguments = new Dictionary<string, IReadOnlyList<string>> { [group] = args } };

    [Fact]
    public void C01_language_version_is_9_0()
    {
        var g = Basic().Editor();
        Assert.All(g.Assemblies, a => Assert.Equal("9.0", a.LangVersion));
    }

    [Fact]
    public void C02_nullable_disabled_unless_rsp()
    {
        Assert.All(Basic().Editor().Assemblies, a => Assert.Equal("disable", a.Nullable));
        var g = Basic().Rsp("Assets/csc.rsp", "-nullable:enable").Editor();
        Assert.All(g.Assemblies, a => Assert.Equal("enable", a.Nullable));
    }

    [Fact]
    public void C03_C07_plan_carries_no_warning_level_or_output_kind_overrides()
    {
        // Warning level 4 and deterministic DLL output are fixed by Ucl.Compilation; the plan exposes nothing that changes them.
        var plan = Basic().Editor().Find("Mod")!;
        Assert.Equal(AssemblyKind.Asmdef, plan.Kind);
        Assert.Null(plan.ResponseFile);
        Assert.False(plan.WarnAsErrorAll);
    }

    [Fact]
    public void C04_CS0169_CS0649_suppressed_when_suppressCommonWarnings()
    {
        var on = Basic().Editor();
        Assert.All(on.Assemblies, a => Assert.Equal(["CS0169", "CS0649", "CS1701", "CS1702"], a.NoWarn));

        var off = Basic().Settings(new ProjectSettingsData { SuppressCommonWarnings = false }).Editor();
        Assert.All(off.Assemblies, a =>
        {
            Assert.DoesNotContain("CS0169", a.NoWarn);
            Assert.DoesNotContain("CS0649", a.NoWarn);
        });

        var parsed = Basic().Settings(ProjectSettingsParserTests.Unity6Asset).Editor();
        Assert.DoesNotContain("CS0649", parsed.Find("Mod")!.NoWarn);
    }

    [Fact]
    public void C05_CS1701_CS1702_always_suppressed()
    {
        var g = Basic().Settings(new ProjectSettingsData { SuppressCommonWarnings = false }).Player();
        Assert.All(g.Assemblies, a => Assert.Equal(["CS1701", "CS1702"], a.NoWarn));
    }

    [Fact]
    public void C04_nowarn_accumulates_from_args_and_rsp_with_normalisation()
    {
        var g = Basic()
            .Settings(Args("Standalone", "-nowarn:1234"))
            .Rsp("Assets/csc.rsp", "-nowarn:618 -nowarn:CS0414")
            .Editor();
        Assert.Equal(["CS0169", "CS0414", "CS0618", "CS0649", "CS1234", "CS1701", "CS1702"], g.Find("Assembly-CSharp")!.NoWarn);
        Assert.Equal(["CS0169", "CS0414", "CS0618", "CS0649", "CS1234", "CS1701", "CS1702"], g.Find("Mod")!.NoWarn);
    }

    [Fact]
    public void C06_unsafe_from_asmdef()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Fast/Fast.asmdef", "Fast", "\"allowUnsafeCode\": true")
            .Asmdef("Assets/Safe/Safe.asmdef", "Safe")
            .Editor();
        Assert.True(g.Find("Fast")!.AllowUnsafe);
        Assert.False(g.Find("Safe")!.AllowUnsafe);
    }

    [Fact]
    public void C06_unsafe_for_predefined_from_player_settings()
    {
        var on = Basic().Settings(new ProjectSettingsData { AllowUnsafeCode = true }).Editor();
        Assert.True(on.Find("Assembly-CSharp")!.AllowUnsafe);
        Assert.False(on.Find("Mod")!.AllowUnsafe);
        Assert.False(Basic().Editor().Find("Assembly-CSharp")!.AllowUnsafe);
    }

    [Fact]
    public void C06_unsafe_from_rsp_overrides()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Fast/Fast.asmdef", "Fast", "\"allowUnsafeCode\": true")
            .Rsp("Assets/Fast/csc.rsp", "-unsafe-")
            .Scripts("Assets/A.cs")
            .Rsp("Assets/csc.rsp", "-unsafe")
            .Editor();
        Assert.False(g.Find("Fast")!.AllowUnsafe);
        Assert.True(g.Find("Assembly-CSharp")!.AllowUnsafe);
    }

    [Fact]
    public void C08_warnings_as_errors_off_by_default_on_with_rsp()
    {
        Assert.All(Basic().Editor().Assemblies, a => Assert.False(a.WarnAsErrorAll));
        var g = Basic().Rsp("Assets/csc.rsp", "-warnaserror+ -warnaserror-:CS0618,0618 -warnaserror:CS0168").Editor();
        var plan = g.Find("Assembly-CSharp")!;
        Assert.True(plan.WarnAsErrorAll);
        Assert.Equal(["CS0618"], plan.WarnNotAsErrorIds);
        Assert.Equal(["CS0168"], plan.WarnAsErrorIds);
    }

    [Fact]
    public void C08_additionalCompilerArguments_warnaserror_can_be_turned_off_by_rsp()
    {
        var g = Basic()
            .Settings(Args("Standalone", "-warnaserror"))
            .Rsp("Assets/Mod/csc.rsp", "-warnaserror-")
            .Editor();
        Assert.True(g.Find("Assembly-CSharp")!.WarnAsErrorAll);
        Assert.False(g.Find("Mod")!.WarnAsErrorAll);
    }

    [Fact]
    public void D51_additionalCompilerArguments_come_first_and_rsp_wins_for_single_values()
    {
        var g = Basic()
            .Settings(Args("Standalone", "-langversion:8.0", "-nullable:warnings", "-define:SHARED;ARGS_ONLY"))
            .Rsp("Assets/csc.rsp", "-langversion:10.0 -define:SHARED;RSP_ONLY")
            .Editor();
        var plan = g.Find("Assembly-CSharp")!;
        Assert.Equal("10.0", plan.LangVersion);
        Assert.Equal("warnings", plan.Nullable);
        Assert.Equal("D51", plan.Defines.Reasons["SHARED"]);
        Assert.Equal("D51", plan.Defines.Reasons["ARGS_ONLY"]);
        Assert.Equal("Assets/csc.rsp", plan.Defines.Reasons["RSP_ONLY"]);
        Assert.Equal("D51", g.BaseDefines.Reasons["ARGS_ONLY"]);
    }

    [Fact]
    public void D51_arguments_of_other_groups_are_ignored()
    {
        var g = Basic().Settings(Args("Android", "-langversion:7.3", "-define:DROID")).Editor(BuildPlatform.StandaloneWindows64);
        Assert.Equal("9.0", g.Find("Mod")!.LangVersion);
        Assert.False(g.Find("Mod")!.Defines.Contains("DROID"));
        var droid = Basic().Settings(Args("Android", "-langversion:7.3", "-define:DROID")).Player(BuildPlatform.Android);
        Assert.Equal("7.3", droid.Find("Mod")!.LangVersion);
        Assert.True(droid.Find("Mod")!.Defines.Contains("DROID"));
    }

    [Fact]
    public void D52_global_rsp_defines_apply_to_predefined_and_asmdefs_without_own_rsp()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/Mod/Mod.asmdef", "Mod")
            .Asmdef("Assets/Own/Own.asmdef", "Own")
            .Rsp("Assets/csc.rsp", "# global options\n-define:GLOBAL_RSP\n")
            .Rsp("Assets/Own/csc.rsp", "-define:OWN_RSP")
            .Scripts("Assets/A.cs", "Assets/Editor/E.cs")
            .Editor();

        foreach (var name in new[] { "Assembly-CSharp", "Assembly-CSharp-Editor", "Mod" })
        {
            var plan = g.Find(name)!;
            Assert.Equal("Assets/csc.rsp", plan.Defines.Reasons["GLOBAL_RSP"]);
            Assert.Equal("Assets/csc.rsp", plan.ResponseFile);
            Assert.False(plan.Defines.Contains("OWN_RSP"));
        }

        var own = g.Find("Own")!;
        Assert.Equal("Assets/Own/csc.rsp", own.Defines.Reasons["OWN_RSP"]);
        Assert.False(own.Defines.Contains("GLOBAL_RSP"));
        Assert.Equal("Assets/Own/csc.rsp", own.ResponseFile);
        Assert.False(g.BaseDefines.Contains("GLOBAL_RSP"));
    }

    [Fact]
    public void D52_rsp_in_a_folder_without_asmdef_is_ignored()
    {
        var g = Basic().Rsp("Assets/Mod/Sub/csc.rsp", "-define:SUB").Rsp("Assets/Scripts/csc.rsp", "-define:SCRIPTS").Editor();
        Assert.All(g.Assemblies, a =>
        {
            Assert.False(a.Defines.Contains("SUB"));
            Assert.False(a.Defines.Contains("SCRIPTS"));
            Assert.Null(a.ResponseFile);
        });
    }

    [Fact]
    public void D52_rsp_beside_asmdef_replaces_global_options()
    {
        var g = Basic()
            .Rsp("Assets/csc.rsp", "-nowarn:0414 -langversion:latest -warnaserror")
            .Rsp("Assets/Mod/csc.rsp", "-nullable:enable")
            .Editor();
        var mod = g.Find("Mod")!;
        Assert.DoesNotContain("CS0414", mod.NoWarn);
        Assert.Equal("9.0", mod.LangVersion);
        Assert.Equal("enable", mod.Nullable);
        Assert.False(mod.WarnAsErrorAll);
        var main = g.Find("Assembly-CSharp")!;
        Assert.Contains("CS0414", main.NoWarn);
        Assert.Equal("latest", main.LangVersion);
        Assert.True(main.WarnAsErrorAll);
    }

    [Fact]
    public void D52_per_assembly_defines_do_not_leak()
    {
        var g = new InventoryBuilder()
            .Asmdef("Assets/X/X.asmdef", "X")
            .Asmdef("Assets/Y/Y.asmdef", "Y")
            .Rsp("Assets/X/csc.rsp", "-define:ONLY_X")
            .Editor();
        Assert.True(g.Find("X")!.Defines.Contains("ONLY_X"));
        Assert.False(g.Find("Y")!.Defines.Contains("ONLY_X"));
    }

    [Fact]
    public void Unsupported_rsp_option_is_UCL1020_warning()
    {
        var g = Basic().Rsp("Assets/csc.rsp", "-optimize+ -define:OK /debug:full").Editor();
        Assert.Equal(2, g.Diagnostics.Count);
        Assert.All(g.Diagnostics, d =>
        {
            Assert.Equal("UCL1020", d.Id);
            Assert.Equal(Severity.Warning, d.Severity);
            Assert.Equal("Assets/csc.rsp", d.File);
            Assert.Null(d.Assembly);
        });
        Assert.Contains(g.Diagnostics, d => d.Message.Contains("'-optimize+'", StringComparison.Ordinal));
        Assert.True(g.Find("Mod")!.Defines.Contains("OK"));
    }

    [Fact]
    public void Additional_files_analyzer_configs_and_rulesets()
    {
        var g = Basic()
            .AnalyzerConfig("Assets/.editorconfig")
            .AnalyzerConfig("Assets/Mod/.globalconfig")
            .RuleSet("Assets/Default.ruleset")
            .RuleSet("Assets/Mod/Mod.ruleset")
            .Rsp("Assets/csc.rsp", "-additionalfile:Assets/b.txt;Assets/a.txt;Assets/a.txt -analyzerconfig:Assets/extra.globalconfig")
            .Editor();
        var main = g.Find("Assembly-CSharp")!;
        Assert.Equal(["Assets/a.txt", "Assets/b.txt"], main.AdditionalFiles);
        Assert.Equal(["Assets/.editorconfig", "Assets/Mod/.globalconfig", "Assets/extra.globalconfig"], main.AnalyzerConfigs);
        Assert.Equal("Assets/Default.ruleset", main.RuleSet);
        Assert.Equal("Assets/Mod/Mod.ruleset", g.Find("Mod")!.RuleSet);
    }

    [Fact]
    public void Rsp_ruleset_wins_and_no_ruleset_is_null()
    {
        Assert.Null(Basic().Editor().Find("Mod")!.RuleSet);
        var g = Basic().RuleSet("Assets/Mod/Mod.ruleset").Rsp("Assets/Mod/csc.rsp", "-ruleset:Assets/Custom.ruleset").Editor();
        Assert.Equal("Assets/Custom.ruleset", g.Find("Mod")!.RuleSet);
        var asmdefFallback = Basic().RuleSet("Assets/Default.ruleset").Editor();
        Assert.Equal("Assets/Default.ruleset", asmdefFallback.Find("Mod")!.RuleSet);
    }

    [Fact]
    public void Plan_defines_include_the_cell_wide_defines()
    {
        var g = Basic().Player(BuildPlatform.Android, development: true);
        foreach (var plan in g.Assemblies)
        {
            Assert.True(plan.Defines.Contains("UNITY_ANDROID"));
            Assert.True(plan.Defines.Contains("DEVELOPMENT_BUILD"));
            Assert.Equal(g.BaseDefines.Symbols, plan.Defines.Symbols);
        }
    }
}
