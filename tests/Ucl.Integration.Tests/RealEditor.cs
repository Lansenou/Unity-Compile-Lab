using Ucl.Core.Model;

namespace Ucl.Integration.Tests;

/// <summary>
/// The real-editor test mode (docs/oracle.md): with <c>UCL_REAL_EDITOR=1</c> and <c>UNITY_EDITOR_PATH</c> set
/// to a licensed Unity 6 install, every cell for that editor's version runs against the real DLLs, including
/// cells marked <c>realEditor</c>. Without it, only the stub-editor cells run.
/// </summary>
public static class RealEditor
{
    /// <summary>The real editor install folder, or null in the default (stub) mode.</summary>
    public static string? Path { get; } =
        Environment.GetEnvironmentVariable("UCL_REAL_EDITOR") == "1" ? Environment.GetEnvironmentVariable("UNITY_EDITOR_PATH") : null;

    /// <summary>The real editor's version, from its install folder name (Unity Hub layout), or null.</summary>
    public static UnityVersion? Version { get; } =
        Path is null ? null : UnityVersion.Parse(System.IO.Path.GetFileName(Path.TrimEnd('/', '\\'))).Value;

    /// <summary>Whether a manifest cell runs in this mode.</summary>
    public static bool Runs(FixtureCell cell) => Path is null
        ? !cell.RealEditor
        : Version is null || cell.UnityVersion == Version.ToString() || !UnityVersion.Parse(cell.UnityVersion).Ok || !UnityVersion.Parse(cell.UnityVersion).Value!.IsUnity6;
}
