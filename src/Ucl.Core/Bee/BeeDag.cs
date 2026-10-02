using Ucl.Core.Model;

namespace Ucl.Core.Bee;

/// <summary>
/// What a Bee dag folder compiled, read from the defines of one of its response files. The folder name's prefix
/// is not documented, so the content decides: <c>UNITY_EDITOR</c> (editor), <c>DEVELOPMENT_BUILD</c>, the platform
/// symbols (D13-D19), the editor host (D11), the backend (D31, D32) and the version (D03).
/// </summary>
public static class BeeDag
{
    /// <summary>The cell a response file's defines describe, or a failure naming what is missing.</summary>
    public static Result<CompileCell> CellOf(IReadOnlyCollection<string> defines, UnityVersion editorVersion)
    {
        ArgumentNullException.ThrowIfNull(defines);
        ArgumentNullException.ThrowIfNull(editorVersion);
        bool Has(string s) => defines.Contains(s);
        var editor = Has("UNITY_EDITOR");
        BuildPlatform? platform =
            Has("UNITY_STANDALONE_WIN") ? BuildPlatform.StandaloneWindows64
            : Has("UNITY_STANDALONE_OSX") ? BuildPlatform.StandaloneOSX
            : Has("UNITY_STANDALONE_LINUX") ? BuildPlatform.StandaloneLinux64
            : Has("UNITY_IOS") ? BuildPlatform.iOS
            : Has("UNITY_ANDROID") ? BuildPlatform.Android
            : Has("UNITY_WEBGL") ? BuildPlatform.WebGL
            : null;
        if (platform is null)
        {
            return Result<CompileCell>.Failure("no platform define (UNITY_STANDALONE_WIN/OSX/LINUX, UNITY_IOS, UNITY_ANDROID or UNITY_WEBGL) that ucl supports");
        }

        var os = Has("UNITY_EDITOR_WIN") ? HostOs.Windows : Has("UNITY_EDITOR_OSX") ? HostOs.MacOS : HostOs.Linux;
        ScriptingBackend? backend = Has("ENABLE_IL2CPP") ? ScriptingBackend.IL2CPP : Has("ENABLE_MONO") ? ScriptingBackend.Mono : null;
        var version = VersionOf(defines) is var (minor, patch) && (minor, patch) != (editorVersion.Minor, editorVersion.Patch)
            ? new UnityVersion(editorVersion.Major, minor, patch, string.Empty)
            : editorVersion;
        return Result<CompileCell>.Success(new CompileCell(
            version,
            editor ? TargetKind.Editor : TargetKind.Player,
            platform.Value,
            backend,
            !editor && Has("DEVELOPMENT_BUILD"),
            os,
            IncludeTests: !editor && Has("UNITY_INCLUDE_TESTS")));
    }

    // UNITY_6000_<M>_<P> (D03).
    private static (int Minor, int Patch)? VersionOf(IEnumerable<string> defines)
    {
        foreach (var d in defines)
        {
            var parts = d.Split('_');
            if (parts is ["UNITY", "6000", var m, var p] && int.TryParse(m, out var minor) && int.TryParse(p, out var patch))
            {
                return (minor, patch);
            }
        }

        return null;
    }
}
