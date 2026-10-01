using Ucl.Core.Model;
using Ucl.Core.Parsing;

namespace Ucl.Core.Rules;

/// <summary>
/// The cell-wide define table of docs/defines.md. Each symbol's reason is its row id (D01...) so
/// <c>ucl explain</c> and the tests can point at the documented rule.
/// </summary>
public static class DefineTable
{
    // D05: the historical _OR_NEWER series Unity 6 still defines (observed in Bee rsp files).
    private static readonly string[] Historical =
    [
        "5_3", "5_4", "5_5", "5_6",
        "2017_1", "2017_2", "2017_3", "2017_4",
        "2018_1", "2018_2", "2018_3", "2018_4",
        "2019_1", "2019_2", "2019_3", "2019_4",
        "2020_1", "2020_2", "2020_3",
        "2021_1", "2021_2", "2021_3",
        "2022_1", "2022_2", "2022_3",
        "2023_1", "2023_2", "2023_3",
    ];

    /// <summary>The scripting backend of a cell: <c>--backend</c>, else ProjectSettings, else the platform default.</summary>
    public static ScriptingBackend BackendOf(CompileCell cell, ProjectSettingsData settings)
    {
        var info = PlatformInfo.Of(cell.Platform);
        if (cell.Backend is { } explicitBackend)
        {
            return explicitBackend;
        }

        // iOS and WebGL support only IL2CPP whatever the settings say.
        if (cell.Platform is BuildPlatform.iOS or BuildPlatform.WebGL)
        {
            return ScriptingBackend.IL2CPP;
        }

        return settings.ScriptingBackend.TryGetValue(info.TargetGroup, out var b) ? b : info.DefaultBackend;
    }

    /// <summary>Computes rows D01-D51 and D60 for a cell. Per-assembly rows (D52, D53) are added by the graph builder.</summary>
    /// <param name="cell">The compile cell.</param>
    /// <param name="settings">Player settings.</param>
    /// <param name="testFrameworkPresent">Whether <c>com.unity.test-framework</c> is resolved.</param>
    /// <param name="constraintOnly">Receives constraint-only symbols (D60), which are not passed to the compiler.</param>
    public static DefineSet Compute(CompileCell cell, ProjectSettingsData settings, bool testFrameworkPresent, out DefineSet constraintOnly)
    {
        var d = new DefineSet();
        var v = cell.UnityVersion;
        var info = PlatformInfo.Of(cell.Platform);

        d.Add($"UNITY_{v.Major}", "D01");
        d.Add($"UNITY_{v.Major}_{v.Minor}", "D02");
        d.Add($"UNITY_{v.Major}_{v.Minor}_{v.Patch}", "D03");
        for (var k = 0; k <= v.Minor; k++)
        {
            d.Add($"UNITY_{v.Major}_{k}_OR_NEWER", "D04");
        }

        foreach (var h in Historical)
        {
            d.Add($"UNITY_{h}_OR_NEWER", "D05");
        }

        if (cell.IsEditor)
        {
            d.Add("UNITY_EDITOR", "D10");
            d.Add(cell.EditorOs switch
            {
                HostOs.Windows => "UNITY_EDITOR_WIN",
                HostOs.MacOS => "UNITY_EDITOR_OSX",
                _ => "UNITY_EDITOR_LINUX",
            }, "D11");
            d.Add("UNITY_EDITOR_64", "D12");
        }

        switch (cell.Platform)
        {
            case BuildPlatform.StandaloneWindows64:
                Standalone(d, "WIN", "D14");
                break;
            case BuildPlatform.StandaloneOSX:
                Standalone(d, "OSX", "D15");
                break;
            case BuildPlatform.StandaloneLinux64:
                Standalone(d, "LINUX", "D16");
                break;
            case BuildPlatform.iOS:
                d.Add("UNITY_IOS", "D17");
                d.Add("PLATFORM_IOS", "D20");
                break;
            case BuildPlatform.Android:
                d.Add("UNITY_ANDROID", "D18");
                d.Add("PLATFORM_ANDROID", "D20");
                break;
            case BuildPlatform.WebGL:
                d.Add("UNITY_WEBGL", "D19");
                d.Add("PLATFORM_WEBGL", "D20");
                break;
        }

        if (info.Is64Bit)
        {
            d.Add("UNITY_64", "D21");
        }

        d.Add("CSHARP_7_3_OR_NEWER", "D30");
        d.Add(BackendOf(cell, settings) == ScriptingBackend.IL2CPP ? "ENABLE_IL2CPP" : "ENABLE_MONO", BackendOf(cell, settings) == ScriptingBackend.IL2CPP ? "D32" : "D31");
        AddProfile(d, settings.IsNetFramework(info.TargetGroup));

        if (settings.ActiveInputHandler is 0 or 2)
        {
            d.Add("ENABLE_LEGACY_INPUT_MANAGER", "D35");
        }

        if (settings.ActiveInputHandler is 1 or 2)
        {
            d.Add("ENABLE_INPUT_SYSTEM", "D36");
        }

        var debug = cell.IsEditor || cell.Development;
        if (!cell.IsEditor && cell.Development)
        {
            d.Add("DEVELOPMENT_BUILD", "D37");
        }

        if (debug)
        {
            d.Add("DEBUG", "D38");
            d.Add("TRACE", "D38");
            d.Add("UNITY_ASSERTIONS", "D39");
        }

        foreach (var s in settings.ScriptingDefineSymbols.GetValueOrDefault(info.TargetGroup) ?? [])
        {
            d.Add(s, "D50");
        }

        foreach (var s in RspParser.Parse(settings.AdditionalCompilerArguments.GetValueOrDefault(info.TargetGroup) ?? []).Defines)
        {
            d.Add(s, "D51");
        }

        constraintOnly = new DefineSet();
        if ((cell.IsEditor && testFrameworkPresent) || cell.IncludeTests)
        {
            constraintOnly.Add("UNITY_INCLUDE_TESTS", "D60");
        }

        return d;
    }

    /// <summary>
    /// The defines of one assembly: <paramref name="cellDefines"/> (computed for the build target group's API
    /// compatibility level) with the profile rows D33/D34 replaced when the assembly compiles against the other profile.
    /// </summary>
    public static DefineSet ForProfile(DefineSet cellDefines, bool netFramework)
    {
        var d = cellDefines.Copy();
        d.RemoveWithReason("D33");
        d.RemoveWithReason("D34");
        AddProfile(d, netFramework);
        return d;
    }

    private static void AddProfile(DefineSet d, bool netFramework)
    {
        if (netFramework)
        {
            d.Add("NET_4_6", "D34");
            d.Add("NET_UNITY_4_8", "D34");
        }
        else
        {
            foreach (var s in new[] { "NET_STANDARD_2_0", "NET_STANDARD_2_1", "NET_STANDARD", "NETSTANDARD2_1", "NETSTANDARD" })
            {
                d.Add(s, "D33");
            }
        }
    }

    private static void Standalone(DefineSet d, string os, string row)
    {
        d.Add("UNITY_STANDALONE", "D13");
        d.Add($"UNITY_STANDALONE_{os}", row);
        d.Add("PLATFORM_STANDALONE", "D20");
        d.Add($"PLATFORM_STANDALONE_{os}", "D20");
    }
}
