using Ucl.Core.Model;

namespace Ucl.Core.Rules;

/// <summary>
/// Editor files a compile references beyond <c>Managed/UnityEngine/*Module.dll</c> and the .NET profile
/// (docs/architecture.md, "Editor references"). Paths are relative to the editor's data folder unless stated.
/// </summary>
public static class EditorFiles
{
    /// <summary>The facade that forwards every engine type to its module; every assembly with engine references gets it.</summary>
    public const string EngineFacade = "Managed/UnityEngine/UnityEngine.dll";

    /// <summary>Referenced by every assembly in editor cells.</summary>
    public const string EditorGraphs = "Managed/UnityEditor.Graphs.dll";

    /// <summary>Referenced by code-gen assemblies (<see cref="CodeGenAssemblies.UsesCompilationPipeline"/>).</summary>
    public const string CompilationPipeline = "Managed/Unity.CompilationPipeline.Common.dll";

    /// <summary>The editor's own source generators: the 6000.3 folder, then the 6000.0 one.</summary>
    public static IReadOnlyList<string> SourceGeneratorFolders { get; } = ["Tools/BuildPipeline/Unity.SourceGenerators", "Tools/Unity.SourceGenerators"];

    /// <summary>
    /// The folder of installed platform support. Under the data folder on Windows and Linux; beside <c>Unity.app</c> on macOS.
    /// </summary>
    public const string PlaybackEngines = "PlaybackEngines";

    /// <summary>Besides <c>UnityEditor.*.Extensions.dll</c>, the Android support's editor libraries.</summary>
    public static IReadOnlyList<string> AndroidEditorLibraries { get; } = ["Unity.Android.Gradle.dll", "Unity.Android.Types.dll"];

    /// <summary>The support folder of a platform under <see cref="PlaybackEngines"/>.</summary>
    public static string SupportFolder(BuildPlatform platform) => platform switch
    {
        BuildPlatform.StandaloneWindows64 => "WindowsStandaloneSupport",
        BuildPlatform.StandaloneOSX => "MacStandaloneSupport",
        BuildPlatform.StandaloneLinux64 => "LinuxStandaloneSupport",
        BuildPlatform.iOS => "iOSSupport",
        BuildPlatform.Android => "AndroidPlayer",
        _ => "WebGLSupport",
    };
}
