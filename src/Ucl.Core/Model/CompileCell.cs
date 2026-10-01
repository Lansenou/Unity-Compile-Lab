namespace Ucl.Core.Model;

/// <summary>One cell of a compile matrix: everything that changes defines and assembly membership.</summary>
/// <param name="UnityVersion">Editor version.</param>
/// <param name="Target">Editor or player.</param>
/// <param name="Platform">Build platform (the active build target for editor cells).</param>
/// <param name="Backend">Scripting backend, or null to read it from ProjectSettings.</param>
/// <param name="Development">Development build (player cells).</param>
/// <param name="EditorOs">Editor host OS (editor cells).</param>
/// <param name="IncludeTests">Treat test assemblies as part of a player build.</param>
public sealed record CompileCell(
    UnityVersion UnityVersion,
    TargetKind Target,
    BuildPlatform Platform,
    ScriptingBackend? Backend,
    bool Development,
    HostOs EditorOs,
    bool IncludeTests = false)
{
    /// <summary>True for editor cells.</summary>
    public bool IsEditor => Target == TargetKind.Editor;

    /// <summary>A short label, such as <c>6000.0.30f1 editor StandaloneWindows64</c>.</summary>
    public string Label => $"{UnityVersion} {(IsEditor ? "editor" : "player")} {Platform}{(Development ? " development" : string.Empty)}";
}
