using Ucl.Core.Model;

namespace Ucl.Core.Rules;

/// <summary>
/// The symbols Unity 6 adds that its manual does not list: rows E01-E15 of docs/defines.md ("Built-in symbols"),
/// observed in public Unity-generated project files and in UnityCsReference. Each list is the observed set, sorted.
/// </summary>
public static class BuiltInDefines
{
    /// <summary>E01: every assembly (UnityCsReference <c>EditorBuildRules.s_CSharpVersionDefines</c>).</summary>
    public const string CSharp7OrLater = "CSHARP_7_OR_LATER";

    /// <summary>E02: Editor-only assemblies (UnityCsReference <c>EditorBuildRules.ToScriptAssemblies</c>).</summary>
    public const string EditorOnlyCompilation = "UNITY_EDITOR_ONLY_COMPILATION";

    /// <summary>E04: editor cells and development players.</summary>
    public static IReadOnlyList<string> Diagnostics { get; } = ["ENABLE_PROFILER", "ENABLE_UNITY_COLLECTIONS_CHECKS"];

    /// <summary>E16: the symbol the editor's engine build is made with, for player cells compiled against it.</summary>
    public const string CollectionsChecks = "ENABLE_UNITY_COLLECTIONS_CHECKS";

    /// <summary>E05: editor cells only.</summary>
    public static IReadOnlyList<string> EditorServices { get; } =
    [
        "EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED", "ENABLE_ACCELERATOR_CLIENT_DEBUGGING", "ENABLE_BURST_AOT", "ENABLE_CLOUD_LICENSE",
        "ENABLE_EDITOR_GAME_SERVICES", "ENABLE_EDITOR_HUB_LICENSE", "ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API",
        "ENABLE_MARSHALLING_TESTS", "UNITY_TEAM_LICENSE",
    ];

    /// <summary>E06: every cell, every platform.</summary>
    public static IReadOnlyList<string> EngineFeatures { get; } =
    [
        "ENABLE_AUDIO", "ENABLE_CLOTH", "ENABLE_CLOUD_SERVICES", "ENABLE_CLOUD_SERVICES_ADS", "ENABLE_CLOUD_SERVICES_ANALYTICS",
        "ENABLE_CLOUD_SERVICES_BUILD", "ENABLE_CLOUD_SERVICES_CRASH_REPORTING", "ENABLE_CLOUD_SERVICES_PURCHASING",
        "ENABLE_CLOUD_SERVICES_USE_WEBREQUEST", "ENABLE_CRUNCH_TEXTURE_COMPRESSION", "ENABLE_CUSTOM_RENDER_TEXTURE", "ENABLE_DIRECTOR",
        "ENABLE_DIRECTOR_AUDIO", "ENABLE_DIRECTOR_TEXTURE", "ENABLE_LOCALIZATION", "ENABLE_MANAGED_ANIMATION_JOBS", "ENABLE_MANAGED_AUDIO_JOBS",
        "ENABLE_MANAGED_JOBS", "ENABLE_MANAGED_TRANSFORM_JOBS", "ENABLE_MANAGED_UNITYTLS", "ENABLE_MULTIPLE_DISPLAYS",
        "ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK", "ENABLE_PHYSICS", "ENABLE_SPRITES", "ENABLE_TERRAIN", "ENABLE_TEXTURE_STREAMING",
        "ENABLE_TILEMAP", "ENABLE_TIMELINE", "ENABLE_UNITYEVENTS", "ENABLE_UNITYWEBREQUEST", "ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT",
        "ENABLE_VIDEO", "ENABLE_VR", "ENABLE_WEBCAM", "ENABLE_WEBSOCKET_CLIENT", "ENABLE_WWW", "TEXTCORE_1_0_OR_NEWER",
        "TEXTCORE_FONT_ENGINE_1_5_OR_NEWER", "TEXTCORE_TEXT_ENGINE_1_5_OR_NEWER",
    ];

    /// <summary>E07: every cell from Unity 6000.0.76 (the lowest version observed with them; 6000.0.68 has neither).</summary>
    public static IReadOnlyList<string> Consent { get; } = ["ENABLE_UNITY_CLOUD_IDENTIFIERS", "ENABLE_UNITY_CONSENT"];

    /// <summary>E08: every cell from Unity 6000.3.</summary>
    public static IReadOnlyList<string> Unity60003 { get; } = ["TEXTCORE_FONT_ENGINE_1_6_OR_NEWER"];

    /// <summary>E08: every cell of Unity 6000.3 before 6000.3.19 (observed on 6000.3.5 and 6000.3.10, absent on 6000.3.19).</summary>
    public const string AudioScriptablePipeline = "ENABLE_AUDIO_SCRIPTABLE_PIPELINE";

    /// <summary>E17: editor cells from Unity 6000.3.19.</summary>
    public const string ProfilerAssistant = "ENABLE_PROFILER_ASSISTANT_INTEGRATION";

    /// <summary>E10: every Standalone platform.</summary>
    public static IReadOnlyList<string> Standalone { get; } =
    [
        "ENABLE_CACHING", "ENABLE_CLUSTERINPUT", "ENABLE_CLUSTER_SYNC", "ENABLE_LZMA", "ENABLE_MICROPHONE", "ENABLE_MOVIES", "ENABLE_NETWORK",
        "ENABLE_RUNTIME_GI", "ENABLE_SCRIPTING_GC_WBARRIERS", "ENABLE_VIRTUALTEXTURING", "INCLUDE_DYNAMIC_GI", "PLATFORM_ARCH_64",
        "PLATFORM_SUPPORTS_MONO", "RENDER_SOFTWARE_CURSOR",
    ];

    /// <summary>E11-E15: the rest of each platform's set (iOS: nothing observed).</summary>
    public static IReadOnlyList<string> ForPlatform(BuildPlatform platform) => platform switch
    {
        BuildPlatform.StandaloneWindows64 =>
        [
            "ENABLE_ACCESSIBILITY_SCREEN_READER", "ENABLE_AMD", "ENABLE_AR", "ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS",
            "ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING", "ENABLE_EVENT_QUEUE", "ENABLE_NVIDIA", "ENABLE_OUT_OF_PROCESS_CRASH_HANDLER",
            "GFXDEVICE_WAITFOREVENT_MESSAGEPUMP", "PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS", "PLATFORM_SUPPORTS_WAIT_FOR_PRESENTATION",
            "PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP", "PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER",
        ],
        BuildPlatform.StandaloneLinux64 =>
        [
            "ENABLE_MODULAR_UNITYENGINE_ASSEMBLIES", "ENABLE_SPATIALTRACKING", "PLATFORM_SUPPORTS_DISPLAYINFO_API",
            "PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS", "PLATFORM_USES_EXPLICIT_MEMORY_MANAGER_INITIALIZER", "UNITY_STANDALONE_LINUX_API",
        ],
        BuildPlatform.StandaloneOSX =>
        [
            "ENABLE_AR", "ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING", "ENABLE_GAMECENTER", "ENABLE_SPATIALTRACKING", "PLATFORM_HAS_CUSTOM_MUTEX",
            "PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP",
        ],
        BuildPlatform.WebGL =>
        [
            "ENABLE_ENGINE_CODE_STRIPPING", "ENABLE_ONSCREEN_KEYBOARD", "ENABLE_SPATIALTRACKING", "RENDER_SOFTWARE_CURSOR",
            "UNITY_DISABLE_WEB_VERIFICATION", "UNITY_GFX_USE_PLATFORM_VSYNC", "UNITY_WEBGL_API",
        ],
        BuildPlatform.Android =>
        [
            "ENABLE_ACCESSIBILITY", "ENABLE_ANDROID_ADVERTISING_IDS", "ENABLE_ANDROID_APP_SET_ID", "ENABLE_AR", "ENABLE_CACHING",
            "ENABLE_CLOUD_SERVICES_ENGINE_DIAGNOSTICS", "ENABLE_CLOUD_SERVICES_NATIVE_CRASH_REPORTING", "ENABLE_EGL", "ENABLE_ENGINE_CODE_STRIPPING",
            "ENABLE_ETC_COMPRESSION", "ENABLE_EVENT_QUEUE", "ENABLE_FIREBASE_IDENTIFIERS", "ENABLE_INSIGHTS_PLATFORM_SPECIFIC_RESOURCES",
            "ENABLE_LZMA", "ENABLE_MICROPHONE", "ENABLE_NETWORK", "ENABLE_ONSCREEN_KEYBOARD", "ENABLE_RUNTIME_GI", "ENABLE_SCRIPTING_GC_WBARRIERS",
            "ENABLE_SPATIALTRACKING", "ENABLE_UNITYADS_RUNTIME", "INCLUDE_DYNAMIC_GI", "PLATFORM_EXTENDS_VULKAN_DEVICE",
            "PLATFORM_EXTENDS_VULKAN_PIPELINE_CACHE", "PLATFORM_HAS_ADDITIONAL_API_CHECKS", "PLATFORM_HAS_BUGGY_MSAA_RESOLVE",
            "PLATFORM_HAS_MULTIPLE_SWAPCHAINS", "PLATFORM_IMPLEMENTS_INSIGHTS_ANR", "PLATFORM_REQUIRES_TETHERED_VULKAN_COMMAND_POOL",
            "PLATFORM_SUPPORTS_INSIGHTS_DEVICE_INFO", "PLATFORM_SUPPORTS_MONO", "PLATFORM_SUPPORTS_SPLIT_GRAPHICS_JOBS",
            "PLATFORM_UPDATES_TIME_OUTSIDE_OF_PLAYER_LOOP", "UNITY_ANDROID_API", "UNITY_ANDROID_SUPPORTS_SHADOWFILES", "UNITY_CAN_SHOW_SPLASH_SCREEN",
            "UNITY_HAS_GOOGLEVR", "UNITY_HAS_TANGO", "UNITY_UNITYADS_API",
        ],
        _ => [],
    };

    /// <summary>Adds rows E04-E17 for <paramref name="cell"/> to <paramref name="defines"/>.</summary>
    public static void Add(DefineSet defines, CompileCell cell)
    {
        var v = cell.UnityVersion;
        if (cell.IsEditor || cell.Development)
        {
            AddAll(defines, Diagnostics, "E04");
        }
        else if (!cell.PlatformEngine)
        {
            // ucl references the editor's engine DLLs, whose NativeArray<T>.ReadOnly constructor takes the safety handle.
            defines.Add(CollectionsChecks, "E16");
        }

        if (cell.IsEditor)
        {
            AddAll(defines, EditorServices, "E05");
        }

        AddAll(defines, EngineFeatures, "E06");
        if (v.Minor > 0 || v.Patch >= 76)
        {
            AddAll(defines, Consent, "E07");
        }

        if (v.Minor >= 3)
        {
            AddAll(defines, Unity60003, "E08");
        }

        var from60003_19 = v.Minor > 3 || (v.Minor == 3 && v.Patch >= 19);
        if (v.Minor == 3 && !from60003_19)
        {
            defines.Add(AudioScriptablePipeline, "E08");
        }

        if (cell.IsEditor && from60003_19)
        {
            defines.Add(ProfilerAssistant, "E17");
        }

        if (cell.Platform is BuildPlatform.StandaloneWindows64 or BuildPlatform.StandaloneLinux64 or BuildPlatform.StandaloneOSX)
        {
            AddAll(defines, Standalone, "E10");
        }

        AddAll(defines, ForPlatform(cell.Platform), cell.Platform switch
        {
            BuildPlatform.StandaloneWindows64 => "E11",
            BuildPlatform.StandaloneLinux64 => "E12",
            BuildPlatform.StandaloneOSX => "E13",
            BuildPlatform.WebGL => "E14",
            _ => "E15",
        });
    }

    private static void AddAll(DefineSet defines, IEnumerable<string> symbols, string row)
    {
        foreach (var s in symbols)
        {
            defines.Add(s, row);
        }
    }
}
