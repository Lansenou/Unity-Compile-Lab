namespace Ucl.Core.Model;

/// <summary>The names one platform has in Unity's file formats (see docs/platforms.md).</summary>
/// <param name="Platform">The platform.</param>
/// <param name="AsmdefName">Name in asmdef <c>includePlatforms</c>/<c>excludePlatforms</c>.</param>
/// <param name="PluginKey">Key in a plugin <c>.meta</c> <c>platformData</c> entry.</param>
/// <param name="TargetGroup">Build target group name used by <c>scriptingDefineSymbols</c> and similar maps.</param>
/// <param name="TargetGroupNumber">Legacy numeric build target group.</param>
/// <param name="DefaultBackend">Backend when ProjectSettings does not say.</param>
/// <param name="Is64Bit">Whether <c>UNITY_64</c> is defined.</param>
public sealed record PlatformInfo(
    BuildPlatform Platform,
    string AsmdefName,
    string PluginKey,
    string TargetGroup,
    int TargetGroupNumber,
    ScriptingBackend DefaultBackend,
    bool Is64Bit)
{
    /// <summary>The asmdef platform name of the Editor.</summary>
    public const string EditorAsmdefName = "Editor";

    /// <summary>The plugin <c>.meta</c> key of the Editor.</summary>
    public const string EditorPluginKey = "Editor";

    /// <summary>All supported platforms in enum order.</summary>
    public static IReadOnlyList<PlatformInfo> All { get; } =
    [
        new(BuildPlatform.StandaloneWindows64, "WindowsStandalone64", "Win64", "Standalone", 1, ScriptingBackend.Mono, true),
        new(BuildPlatform.StandaloneOSX, "macOSStandalone", "OSXUniversal", "Standalone", 1, ScriptingBackend.Mono, true),
        new(BuildPlatform.StandaloneLinux64, "LinuxStandalone64", "Linux64", "Standalone", 1, ScriptingBackend.Mono, true),
        new(BuildPlatform.iOS, "iOS", "iOS", "iOS", 4, ScriptingBackend.IL2CPP, true),
        new(BuildPlatform.Android, "Android", "Android", "Android", 7, ScriptingBackend.IL2CPP, false),
        new(BuildPlatform.WebGL, "WebGL", "WebGL", "WebGL", 13, ScriptingBackend.IL2CPP, false),
    ];

    /// <summary>Looks up a platform.</summary>
    public static PlatformInfo Of(BuildPlatform platform) => All[(int)platform];

    /// <summary>Parses a <c>--platform</c> value (case-insensitive).</summary>
    public static Result<BuildPlatform> Parse(string text)
    {
        foreach (var p in All)
        {
            if (string.Equals(p.Platform.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                return Result<BuildPlatform>.Success(p.Platform);
            }
        }

        return Result<BuildPlatform>.Failure(
            $"unknown platform '{text}' (supported: {string.Join(", ", All.Select(p => p.Platform))})");
    }
}
