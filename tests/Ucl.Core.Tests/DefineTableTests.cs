using Ucl.Core.Model;
using Ucl.Core.Parsing;
using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

/// <summary>One test (at least) per row of docs/defines.md; the row id is in the test name.</summary>
public class DefineTableTests
{
    private static readonly string[] Standalone = ["UNITY_STANDALONE", "UNITY_STANDALONE_WIN", "UNITY_STANDALONE_OSX", "UNITY_STANDALONE_LINUX", "PLATFORM_STANDALONE", "PLATFORM_STANDALONE_WIN", "PLATFORM_STANDALONE_OSX", "PLATFORM_STANDALONE_LINUX"];

    private static DefineSet Compute(CompileCell cell, ProjectSettingsData? settings = null, bool testFramework = false) =>
        DefineTable.Compute(cell, settings ?? ProjectSettingsData.Default, testFramework);

    private static CompileCell WithVersion(string version) => Cells.Editor() with { UnityVersion = UnityVersion.Parse(version).Value! };

    [Fact]
    public void D01_UNITY_6000_always()
    {
        Assert.Equal("D01", Compute(Cells.Editor()).Reasons["UNITY_6000"]);
        Assert.Equal("D01", Compute(Cells.Player(BuildPlatform.WebGL)).Reasons["UNITY_6000"]);
    }

    [Fact]
    public void D02_UNITY_6000_minor()
    {
        var d = Compute(WithVersion("6000.2.5f1"));
        Assert.Equal("D02", d.Reasons["UNITY_6000_2"]);
        Assert.False(d.Contains("UNITY_6000_0"));
    }

    [Fact]
    public void D03_UNITY_6000_minor_patch_uses_numeric_patch()
    {
        var d = Compute(WithVersion("6000.0.33f1"));
        Assert.Equal("D03", d.Reasons["UNITY_6000_0_33"]);
        Assert.DoesNotContain(d.Symbols, s => s.Contains("f1", StringComparison.Ordinal));
    }

    [Fact]
    public void D04_OR_NEWER_for_every_minor_up_to_current()
    {
        var d = Compute(WithVersion("6000.3.1f1"));
        foreach (var k in new[] { 0, 1, 2, 3 })
        {
            Assert.Equal("D04", d.Reasons[$"UNITY_6000_{k}_OR_NEWER"]);
        }

        Assert.False(d.Contains("UNITY_6000_4_OR_NEWER"));
        Assert.Equal(["UNITY_6000_0_OR_NEWER"], Compute(Cells.Editor()).Symbols.Where(s => s.StartsWith("UNITY_6000_", StringComparison.Ordinal) && s.EndsWith("_OR_NEWER", StringComparison.Ordinal)));
    }

    [Fact]
    public void D05_historical_OR_NEWER_series()
    {
        var d = Compute(Cells.Player());
        var expected = new List<string> { "UNITY_5_3_OR_NEWER", "UNITY_5_4_OR_NEWER", "UNITY_5_5_OR_NEWER", "UNITY_5_6_OR_NEWER" };
        foreach (var year in new[] { 2017, 2018, 2019 })
        {
            expected.AddRange(Enumerable.Range(1, 4).Select(m => $"UNITY_{year}_{m}_OR_NEWER"));
        }

        foreach (var year in new[] { 2020, 2021, 2022, 2023 })
        {
            expected.AddRange(Enumerable.Range(1, 3).Select(m => $"UNITY_{year}_{m}_OR_NEWER"));
        }

        Assert.Equal(28, expected.Count);
        Assert.All(expected, s => Assert.Equal("D05", d.Reasons[s]));
        Assert.Equal(expected.Order(StringComparer.Ordinal), d.Reasons.Where(r => r.Value == "D05").Select(r => r.Key));
        Assert.False(d.Contains("UNITY_2023_4_OR_NEWER"));
        Assert.False(d.Contains("UNITY_2020_4_OR_NEWER"));
    }

    [Fact]
    public void D10_UNITY_EDITOR_only_in_editor_cells()
    {
        Assert.Equal("D10", Compute(Cells.Editor()).Reasons["UNITY_EDITOR"]);
        Assert.False(Compute(Cells.Player()).Contains("UNITY_EDITOR"));
        Assert.False(Compute(Cells.Player(development: true)).Contains("UNITY_EDITOR"));
    }

    [Theory]
    [InlineData(HostOs.Windows, "UNITY_EDITOR_WIN")]
    [InlineData(HostOs.MacOS, "UNITY_EDITOR_OSX")]
    [InlineData(HostOs.Linux, "UNITY_EDITOR_LINUX")]
    public void D11_editor_host_os(HostOs os, string symbol)
    {
        var d = Compute(Cells.Editor(os: os));
        Assert.Equal("D11", d.Reasons[symbol]);
        Assert.Single(d.Symbols, s => s is "UNITY_EDITOR_WIN" or "UNITY_EDITOR_OSX" or "UNITY_EDITOR_LINUX");
        Assert.DoesNotContain(Compute(Cells.Player()).Symbols, s => s.StartsWith("UNITY_EDITOR_", StringComparison.Ordinal));
    }

    [Fact]
    public void D12_UNITY_EDITOR_64_in_editor_cells()
    {
        Assert.Equal("D12", Compute(Cells.Editor(BuildPlatform.Android)).Reasons["UNITY_EDITOR_64"]);
        Assert.False(Compute(Cells.Player()).Contains("UNITY_EDITOR_64"));
    }

    [Theory]
    [InlineData(BuildPlatform.StandaloneWindows64, true)]
    [InlineData(BuildPlatform.StandaloneOSX, true)]
    [InlineData(BuildPlatform.StandaloneLinux64, true)]
    [InlineData(BuildPlatform.iOS, false)]
    [InlineData(BuildPlatform.Android, false)]
    [InlineData(BuildPlatform.WebGL, false)]
    public void D13_UNITY_STANDALONE_for_desktop_platforms(BuildPlatform p, bool expected)
    {
        Assert.Equal(expected, Compute(Cells.Player(p)).Contains("UNITY_STANDALONE"));
        Assert.Equal(expected, Compute(Cells.Editor(p)).Contains("UNITY_STANDALONE"));
        if (expected)
        {
            Assert.Equal("D13", Compute(Cells.Player(p)).Reasons["UNITY_STANDALONE"]);
        }
    }

    [Fact]
    public void D14_StandaloneWindows64_defines_UNITY_STANDALONE_WIN()
    {
        var d = Compute(Cells.Player(BuildPlatform.StandaloneWindows64));
        Assert.Equal("D14", d.Reasons["UNITY_STANDALONE_WIN"]);
        Assert.Equal(["PLATFORM_STANDALONE", "PLATFORM_STANDALONE_WIN", "UNITY_STANDALONE", "UNITY_STANDALONE_WIN"], d.Symbols.Where(Standalone.Contains));
    }

    [Fact]
    public void D15_StandaloneOSX_defines_UNITY_STANDALONE_OSX()
    {
        var d = Compute(Cells.Player(BuildPlatform.StandaloneOSX));
        Assert.Equal("D15", d.Reasons["UNITY_STANDALONE_OSX"]);
        Assert.Equal(["PLATFORM_STANDALONE", "PLATFORM_STANDALONE_OSX", "UNITY_STANDALONE", "UNITY_STANDALONE_OSX"], d.Symbols.Where(Standalone.Contains));
    }

    [Fact]
    public void D16_StandaloneLinux64_defines_UNITY_STANDALONE_LINUX()
    {
        var d = Compute(Cells.Player(BuildPlatform.StandaloneLinux64));
        Assert.Equal("D16", d.Reasons["UNITY_STANDALONE_LINUX"]);
        Assert.Equal(["PLATFORM_STANDALONE", "PLATFORM_STANDALONE_LINUX", "UNITY_STANDALONE", "UNITY_STANDALONE_LINUX"], d.Symbols.Where(Standalone.Contains));
    }

    [Fact]
    public void D17_iOS_defines_UNITY_IOS()
    {
        var d = Compute(Cells.Player(BuildPlatform.iOS));
        Assert.Equal("D17", d.Reasons["UNITY_IOS"]);
        Assert.False(d.Contains("UNITY_ANDROID"));
        Assert.DoesNotContain(d.Symbols, Standalone.Contains);
    }

    [Fact]
    public void D18_Android_defines_UNITY_ANDROID()
    {
        var d = Compute(Cells.Player(BuildPlatform.Android));
        Assert.Equal("D18", d.Reasons["UNITY_ANDROID"]);
        Assert.False(d.Contains("UNITY_IOS"));
        Assert.Equal("D18", Compute(Cells.Editor(BuildPlatform.Android)).Reasons["UNITY_ANDROID"]);
    }

    [Fact]
    public void D19_WebGL_defines_UNITY_WEBGL()
    {
        var d = Compute(Cells.Player(BuildPlatform.WebGL));
        Assert.Equal("D19", d.Reasons["UNITY_WEBGL"]);
        Assert.False(d.Contains("UNITY_ANDROID"));
    }

    [Theory]
    [InlineData(BuildPlatform.StandaloneWindows64, new[] { "PLATFORM_STANDALONE", "PLATFORM_STANDALONE_WIN" })]
    [InlineData(BuildPlatform.StandaloneOSX, new[] { "PLATFORM_STANDALONE", "PLATFORM_STANDALONE_OSX" })]
    [InlineData(BuildPlatform.StandaloneLinux64, new[] { "PLATFORM_STANDALONE", "PLATFORM_STANDALONE_LINUX" })]
    [InlineData(BuildPlatform.iOS, new[] { "PLATFORM_IOS" })]
    [InlineData(BuildPlatform.Android, new[] { "PLATFORM_ANDROID" })]
    [InlineData(BuildPlatform.WebGL, new[] { "PLATFORM_WEBGL" })]
    public void D20_PLATFORM_symbols_mirror_UNITY_platform_symbols(BuildPlatform p, string[] expected)
    {
        var d = Compute(Cells.Player(p));
        Assert.Equal(expected, d.Reasons.Where(r => r.Value == "D20").Select(r => r.Key));
    }

    [Theory]
    [InlineData(BuildPlatform.StandaloneWindows64, true)]
    [InlineData(BuildPlatform.StandaloneOSX, true)]
    [InlineData(BuildPlatform.StandaloneLinux64, true)]
    [InlineData(BuildPlatform.iOS, true)]
    [InlineData(BuildPlatform.Android, false)]
    [InlineData(BuildPlatform.WebGL, false)]
    public void D21_UNITY_64_on_64_bit_platforms(BuildPlatform p, bool expected)
    {
        var d = Compute(Cells.Player(p));
        Assert.Equal(expected, d.Contains("UNITY_64"));
        if (expected)
        {
            Assert.Equal("D21", d.Reasons["UNITY_64"]);
        }
    }

    [Fact]
    public void D30_CSHARP_7_3_OR_NEWER_always()
    {
        Assert.Equal("D30", Compute(Cells.Editor()).Reasons["CSHARP_7_3_OR_NEWER"]);
        Assert.Equal("D30", Compute(Cells.Player(BuildPlatform.iOS)).Reasons["CSHARP_7_3_OR_NEWER"]);
    }

    [Fact]
    public void D31_ENABLE_MONO_for_mono_backend()
    {
        var d = Compute(Cells.Player(BuildPlatform.StandaloneLinux64));
        Assert.Equal("D31", d.Reasons["ENABLE_MONO"]);
        Assert.False(d.Contains("ENABLE_IL2CPP"));
        Assert.Equal("D31", Compute(Cells.Player(BuildPlatform.Android, backend: ScriptingBackend.Mono)).Reasons["ENABLE_MONO"]);
        var settings = new ProjectSettingsData { ScriptingBackend = new Dictionary<string, ScriptingBackend> { ["Android"] = ScriptingBackend.Mono } };
        Assert.Equal("D31", Compute(Cells.Player(BuildPlatform.Android), settings).Reasons["ENABLE_MONO"]);
    }

    [Fact]
    public void D32_ENABLE_IL2CPP_for_il2cpp_backend()
    {
        var d = Compute(Cells.Player(BuildPlatform.Android));
        Assert.Equal("D32", d.Reasons["ENABLE_IL2CPP"]);
        Assert.False(d.Contains("ENABLE_MONO"));
        Assert.True(Compute(Cells.Player(BuildPlatform.StandaloneWindows64, backend: ScriptingBackend.IL2CPP)).Contains("ENABLE_IL2CPP"));
        var settings = ProjectSettingsParser.Parse("PlayerSettings:\n  scriptingBackend:\n    Standalone: 1\n");
        Assert.Equal("D32", Compute(Cells.Player(), settings).Reasons["ENABLE_IL2CPP"]);
    }

    [Fact]
    public void D31_editor_cells_always_ENABLE_MONO_whatever_the_backend()
    {
        var settings = ProjectSettingsParser.Parse("PlayerSettings:\n  scriptingBackend:\n    Standalone: 1\n");
        foreach (var d in new[] { Compute(Cells.Editor(BuildPlatform.WebGL)), Compute(Cells.Editor(), settings), Compute(Cells.Editor(BuildPlatform.iOS, backend: ScriptingBackend.IL2CPP)) })
        {
            Assert.Equal("D31", d.Reasons["ENABLE_MONO"]);
            Assert.False(d.Contains("ENABLE_IL2CPP"));
        }
    }

    [Fact]
    public void D33_NET_STANDARD_symbols_by_default()
    {
        var d = Compute(Cells.Player());
        foreach (var s in new[] { "NET_STANDARD_2_0", "NET_STANDARD_2_1", "NET_STANDARD", "NETSTANDARD2_1", "NETSTANDARD" })
        {
            Assert.Equal("D33", d.Reasons[s]);
        }

        Assert.False(d.Contains("NET_4_6"));
    }

    [Fact]
    public void D34_NET_4_6_for_net_framework()
    {
        var global = new ProjectSettingsData { ApiCompatibilityLevel = 3 };
        var d = Compute(Cells.Player(), global);
        Assert.Equal("D34", d.Reasons["NET_4_6"]);
        Assert.Equal("D34", d.Reasons["NET_UNITY_4_8"]);
        Assert.False(d.Contains("NET_STANDARD"));

        var perGroup = ProjectSettingsParser.Parse("PlayerSettings:\n  apiCompatibilityLevelPerPlatform:\n    Android: 3\n");
        Assert.True(Compute(Cells.Player(BuildPlatform.Android), perGroup).Contains("NET_4_6"));
        Assert.False(Compute(Cells.Player(BuildPlatform.StandaloneWindows64), perGroup).Contains("NET_4_6"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void A03_only_level_3_is_NET_Framework(int level)
    {
        var settings = new ProjectSettingsData { ApiCompatibilityLevel = level };
        Assert.False(settings.IsNetFramework("Standalone"));
        Assert.True(new ProjectSettingsData { ApiCompatibilityLevel = 3 }.IsNetFramework("Standalone"));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void A02_A04_D33_D34_editor_only_assemblies_follow_editorAssembliesCompatibilityLevel(int level, bool netFramework)
    {
        var settings = new ProjectSettingsData { EditorAssembliesCompatibilityLevel = level };
        Assert.Equal(netFramework, settings.IsNetFrameworkFor("Standalone", editorOnly: true));
        Assert.False(settings.IsNetFrameworkFor("Standalone", editorOnly: false));
        var d = DefineTable.ForProfile(Compute(Cells.Editor(), settings), settings.IsNetFrameworkFor("Standalone", editorOnly: true));
        Assert.Equal(netFramework, d.Contains("NET_4_6"));
        Assert.Equal(netFramework, d.Contains("NET_UNITY_4_8"));
        Assert.Equal(!netFramework, d.Contains("NETSTANDARD2_1"));
        Assert.Equal(netFramework ? "D34" : "D33", d.Reasons[netFramework ? "NET_UNITY_4_8" : "NET_STANDARD"]);
    }

    [Fact]
    public void D33_D34_profile_switch_keeps_other_rows_and_project_symbols()
    {
        var settings = ProjectSettingsParser.Parse("PlayerSettings:\n  scriptingDefineSymbols:\n    Standalone: NET_STANDARD;MY_GAME\n");
        var cell = Compute(Cells.Editor(), settings);
        var netfx = DefineTable.ForProfile(cell, netFramework: true);
        Assert.True(netfx.Contains("UNITY_EDITOR"));
        Assert.True(netfx.Contains("MY_GAME"));
        Assert.True(netfx.Contains("NET_4_6"));
        Assert.Equal(cell.Symbols, DefineTable.ForProfile(netfx, netFramework: false).Symbols);
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(1, false, true)]
    [InlineData(2, true, true)]
    [InlineData(7, false, false)]
    public void D35_D36_input_handler(int handler, bool legacy, bool inputSystem)
    {
        var d = Compute(Cells.Player(), new ProjectSettingsData { ActiveInputHandler = handler });
        Assert.Equal(legacy, d.Contains("ENABLE_LEGACY_INPUT_MANAGER"));
        Assert.Equal(inputSystem, d.Contains("ENABLE_INPUT_SYSTEM"));
    }

    [Fact]
    public void D35_ENABLE_LEGACY_INPUT_MANAGER_when_activeInputHandler_absent()
    {
        var d = Compute(Cells.Player(), ProjectSettingsParser.Parse("PlayerSettings:\n  productName: x\n"));
        Assert.Equal("D35", d.Reasons["ENABLE_LEGACY_INPUT_MANAGER"]);
    }

    [Fact]
    public void D36_ENABLE_INPUT_SYSTEM_for_handler_1_or_2()
    {
        Assert.Equal("D36", Compute(Cells.Player(), ProjectSettingsParser.Parse(ProjectSettingsParserTests.Unity6Asset)).Reasons["ENABLE_INPUT_SYSTEM"]);
        Assert.Equal("D36", Compute(Cells.Player(), new ProjectSettingsData { ActiveInputHandler = 1 }).Reasons["ENABLE_INPUT_SYSTEM"]);
    }

    [Fact]
    public void D37_DEVELOPMENT_BUILD_only_for_development_players()
    {
        Assert.Equal("D37", Compute(Cells.Player(development: true)).Reasons["DEVELOPMENT_BUILD"]);
        Assert.False(Compute(Cells.Player()).Contains("DEVELOPMENT_BUILD"));
        Assert.False(Compute(Cells.Editor() with { Development = true }).Contains("DEVELOPMENT_BUILD"));
    }

    [Fact]
    public void D38_DEBUG_TRACE_for_editor_or_development()
    {
        foreach (var cell in new[] { Cells.Editor(), Cells.Player(development: true) })
        {
            var d = Compute(cell);
            Assert.Equal("D38", d.Reasons["DEBUG"]);
            Assert.Equal("D38", d.Reasons["TRACE"]);
        }

        var release = Compute(Cells.Player());
        Assert.False(release.Contains("DEBUG"));
        Assert.False(release.Contains("TRACE"));
    }

    [Fact]
    public void D39_UNITY_ASSERTIONS_for_editor_or_development()
    {
        Assert.Equal("D39", Compute(Cells.Editor()).Reasons["UNITY_ASSERTIONS"]);
        Assert.Equal("D39", Compute(Cells.Player(BuildPlatform.Android, development: true)).Reasons["UNITY_ASSERTIONS"]);
        Assert.False(Compute(Cells.Player(BuildPlatform.Android)).Contains("UNITY_ASSERTIONS"));
    }

    [Fact]
    public void D50_scriptingDefineSymbols_of_the_cell_group()
    {
        var settings = ProjectSettingsParser.Parse(ProjectSettingsParserTests.Unity6Asset);
        var win = Compute(Cells.Player(BuildPlatform.StandaloneWindows64), settings);
        Assert.Equal("D50", win.Reasons["MY_GAME"]);
        Assert.Equal("D50", win.Reasons["STEAM"]);
        Assert.False(win.Contains("ANDROID_ONLY"));
        Assert.True(Compute(Cells.Editor(BuildPlatform.StandaloneOSX), settings).Contains("STEAM"));
        var android = Compute(Cells.Editor(BuildPlatform.Android), settings);
        Assert.Equal("D50", android.Reasons["ANDROID_ONLY"]);
        Assert.False(android.Contains("STEAM"));
        Assert.Equal("D50", Compute(Cells.Player(BuildPlatform.iOS), settings).Reasons["IOS_LEGACY"]);
        Assert.False(Compute(Cells.Player(BuildPlatform.WebGL), settings).Contains("MY_GAME"));
    }

    [Theory]
    [InlineData("1", BuildPlatform.StandaloneLinux64)]
    [InlineData("4", BuildPlatform.iOS)]
    [InlineData("7", BuildPlatform.Android)]
    [InlineData("13", BuildPlatform.WebGL)]
    public void D50_numeric_group_keys(string key, BuildPlatform platform)
    {
        var settings = ProjectSettingsParser.Parse($"PlayerSettings:\n  scriptingDefineSymbols:\n    {key}: LEGACY_KEY;OTHER\n");
        var d = Compute(Cells.Player(platform), settings);
        Assert.Equal("D50", d.Reasons["LEGACY_KEY"]);
        Assert.Equal("D50", d.Reasons["OTHER"]);
    }

    [Fact]
    public void D50_invalid_symbols_are_dropped()
    {
        var settings = ProjectSettingsParser.Parse("PlayerSettings:\n  scriptingDefineSymbols:\n    Standalone: GOOD;1BAD;ALSO-BAD\n");
        var d = Compute(Cells.Player(), settings);
        Assert.True(d.Contains("GOOD"));
        Assert.False(d.Contains("1BAD"));
        Assert.False(d.Contains("ALSO-BAD"));
    }

    [Fact]
    public void D51_additionalCompilerArguments_defines()
    {
        var settings = ProjectSettingsParser.Parse("PlayerSettings:\n  additionalCompilerArguments:\n    Standalone:\n    - -define:ARG_ONE;ARG_TWO\n    - -d:ARG_THREE\n    - -nowarn:1234\n    Android:\n    - -define:ARG_ANDROID\n");
        var d = Compute(Cells.Player(), settings);
        Assert.Equal("D51", d.Reasons["ARG_ONE"]);
        Assert.Equal("D51", d.Reasons["ARG_TWO"]);
        Assert.Equal("D51", d.Reasons["ARG_THREE"]);
        Assert.False(d.Contains("ARG_ANDROID"));
        Assert.Equal("D51", Compute(Cells.Editor(BuildPlatform.Android), settings).Reasons["ARG_ANDROID"]);
    }

    [Fact]
    public void D50_wins_over_D51_for_the_same_symbol()
    {
        var settings = ProjectSettingsParser.Parse("PlayerSettings:\n  scriptingDefineSymbols:\n    Standalone: SAME\n  additionalCompilerArguments:\n    Standalone:\n    - -define:SAME\n");
        Assert.Equal("D50", Compute(Cells.Player(), settings).Reasons["SAME"]);
    }

    [Fact]
    public void D60_UNITY_INCLUDE_TESTS_in_the_Editor_with_the_test_framework_or_with_include_tests()
    {
        Assert.Equal("D60", Compute(Cells.Editor(), testFramework: true).Reasons["UNITY_INCLUDE_TESTS"]);
        Assert.False(Compute(Cells.Editor()).Contains("UNITY_INCLUDE_TESTS"));
        Assert.False(Compute(Cells.Player(), testFramework: true).Contains("UNITY_INCLUDE_TESTS"));
        Assert.Equal("D60", Compute(Cells.Player(includeTests: true)).Reasons["UNITY_INCLUDE_TESTS"]);
    }

    [Fact]
    public void E01_CSHARP_7_OR_LATER_always()
    {
        Assert.Equal("E01", Compute(Cells.Editor()).Reasons["CSHARP_7_OR_LATER"]);
        Assert.Equal("E01", Compute(Cells.Player(BuildPlatform.Android)).Reasons["CSHARP_7_OR_LATER"]);
    }

    [Fact]
    public void E04_profiler_and_collections_checks_in_the_Editor_and_development_players()
    {
        Assert.Equal("E04", Compute(Cells.Editor(BuildPlatform.WebGL)).Reasons["ENABLE_UNITY_COLLECTIONS_CHECKS"]);
        Assert.Equal("E04", Compute(Cells.Player(development: true)).Reasons["ENABLE_PROFILER"]);
        Assert.False(Compute(Cells.Player()).Contains("ENABLE_PROFILER"));
        Assert.False(Compute(Cells.Player() with { PlatformEngine = true }).Contains("ENABLE_UNITY_COLLECTIONS_CHECKS"));
    }

    [Fact]
    public void E16_collections_checks_in_players_compiled_against_the_editor_engine_build()
    {
        Assert.Equal("E16", Compute(Cells.Player()).Reasons["ENABLE_UNITY_COLLECTIONS_CHECKS"]);
        Assert.Equal("E04", Compute(Cells.Player(development: true)).Reasons["ENABLE_UNITY_COLLECTIONS_CHECKS"]);
    }

    [Fact]
    public void E05_editor_services_in_editor_cells_only()
    {
        Assert.Equal("E05", Compute(Cells.Editor()).Reasons["ENABLE_BURST_AOT"]);
        Assert.Equal("E05", Compute(Cells.Editor()).Reasons["UNITY_TEAM_LICENSE"]);
        Assert.False(Compute(Cells.Player(development: true)).Contains("ENABLE_BURST_AOT"));
    }

    [Fact]
    public void E06_engine_features_in_every_cell()
    {
        foreach (var cell in new[] { Cells.Editor(BuildPlatform.WebGL), Cells.Player(BuildPlatform.iOS), Cells.Player(BuildPlatform.Android) })
        {
            Assert.Equal("E06", Compute(cell).Reasons["ENABLE_PHYSICS"]);
            Assert.Equal("E06", Compute(cell).Reasons["TEXTCORE_1_0_OR_NEWER"]);
        }
    }

    [Fact]
    public void E07_consent_symbols_from_6000_0_76()
    {
        Assert.False(Compute(WithVersion("6000.0.68f1")).Contains("ENABLE_UNITY_CONSENT"));
        Assert.Equal("E07", Compute(WithVersion("6000.0.76f1")).Reasons["ENABLE_UNITY_CONSENT"]);
        Assert.Equal("E07", Compute(WithVersion("6000.1.0f1")).Reasons["ENABLE_UNITY_CLOUD_IDENTIFIERS"]);
    }

    [Fact]
    public void E08_6000_3_symbols()
    {
        Assert.False(Compute(WithVersion("6000.2.9f1")).Contains("TEXTCORE_FONT_ENGINE_1_6_OR_NEWER"));
        Assert.Equal("E08", Compute(WithVersion("6000.3.0f1")).Reasons["ENABLE_AUDIO_SCRIPTABLE_PIPELINE"]);
    }

    [Fact]
    public void E10_to_E15_platform_sets()
    {
        Assert.Equal("E10", Compute(Cells.Player(BuildPlatform.StandaloneOSX)).Reasons["ENABLE_MICROPHONE"]);
        Assert.Equal("E11", Compute(Cells.Player(BuildPlatform.StandaloneWindows64)).Reasons["ENABLE_NVIDIA"]);
        Assert.Equal("E12", Compute(Cells.Player(BuildPlatform.StandaloneLinux64)).Reasons["UNITY_STANDALONE_LINUX_API"]);
        Assert.Equal("E13", Compute(Cells.Player(BuildPlatform.StandaloneOSX)).Reasons["ENABLE_GAMECENTER"]);
        Assert.Equal("E14", Compute(Cells.Editor(BuildPlatform.WebGL)).Reasons["UNITY_WEBGL_API"]);
        Assert.Equal("E15", Compute(Cells.Player(BuildPlatform.Android)).Reasons["UNITY_ANDROID_API"]);
        Assert.False(Compute(Cells.Player(BuildPlatform.WebGL)).Contains("ENABLE_MICROPHONE"));
        Assert.Empty(BuiltInDefines.ForPlatform(BuildPlatform.iOS));
    }

    [Fact]
    public void Every_symbol_has_a_row_reason()
    {
        var d = Compute(Cells.Editor(), ProjectSettingsParser.Parse(ProjectSettingsParserTests.Unity6Asset));
        Assert.All(d.Reasons.Values, r => Assert.Matches("^[DE][0-9]{2}$", r));
    }
}
