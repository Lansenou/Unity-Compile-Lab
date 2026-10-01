namespace Ucl.Core.Rules;

/// <summary>
/// Maps editor module DLLs to <c>com.unity.modules.*</c> packages. A module DLL whose name matches a module
/// package is referenced only when that package is resolved; a module with no package (CoreModule,
/// SharedInternalsModule, ...) is always referenced.
/// </summary>
public static class BuiltInModules
{
    /// <summary>Module package suffixes of Unity 6, used when the editor does not list its built-in packages.</summary>
    public static IReadOnlyList<string> KnownPackages { get; } =
    [
        "accessibility", "adaptiveperformance", "ai", "androidjni", "animation", "assetbundle", "audio", "cloth",
        "director", "hierarchycore", "imageconversion", "imgui", "jsonserialize", "nvidia", "particlesystem",
        "physics", "physics2d", "screencapture", "subsystems", "terrain", "terrainphysics", "tilemap", "ui",
        "uielements", "umbra", "unityanalytics", "unitywebrequest", "unitywebrequestassetbundle",
        "unitywebrequestaudio", "unitywebrequesttexture", "unitywebrequestwww", "vectorgraphics", "vehicles",
        "video", "vr", "wind", "xr",
    ];

    /// <summary>The module key of a DLL file name: <c>UnityEngine.PhysicsModule.dll</c> gives <c>physics</c>; null for non-module DLLs.</summary>
    public static string? ModuleKey(string fileName)
    {
        const string prefix = "UnityEngine.";
        const string suffix = "Module.dll";
        if (!fileName.StartsWith(prefix, StringComparison.Ordinal) || !fileName.EndsWith(suffix, StringComparison.Ordinal) || fileName.Length <= prefix.Length + suffix.Length)
        {
            return null;
        }

        return fileName[prefix.Length..^suffix.Length].ToLowerInvariant();
    }

    /// <summary>True when the engine DLL is referenced given the enabled module packages.</summary>
    /// <param name="fileName">DLL file name.</param>
    /// <param name="enabledModules">Enabled module suffixes (<c>physics</c>).</param>
    /// <param name="modulePackages">Module suffixes that exist as packages in this editor.</param>
    public static bool IsReferenced(string fileName, IReadOnlyCollection<string> enabledModules, IReadOnlyCollection<string> modulePackages)
    {
        var key = ModuleKey(fileName);
        return key is null || !modulePackages.Contains(key) || enabledModules.Contains(key);
    }
}
