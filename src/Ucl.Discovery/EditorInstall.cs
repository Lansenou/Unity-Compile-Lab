using Ucl.Core.Model;

namespace Ucl.Discovery;

/// <summary>An installed Unity Editor and the managed DLLs a compile references. All paths are absolute.</summary>
public sealed record EditorInstall
{
    /// <summary>Editor version.</summary>
    public required UnityVersion Version { get; init; }

    /// <summary>Install root (the folder that holds <c>Editor/</c>, or the <c>Unity.app</c> bundle on macOS).</summary>
    public required string Root { get; init; }

    /// <summary><c>Editor/Data</c> (Windows, Linux) or <c>Unity.app/Contents</c> (macOS).</summary>
    public required string DataPath { get; init; }

    /// <summary><c>UnityEngine.*.dll</c> in <c>Managed/UnityEngine/</c>, excluding the <c>UnityEngine.dll</c> facade; sorted.</summary>
    public IReadOnlyList<string> EngineModules { get; init; } = [];

    /// <summary><c>UnityEditor*.dll</c> in <c>Managed/UnityEngine/</c>; sorted.</summary>
    public IReadOnlyList<string> EditorAssemblies { get; init; } = [];

    /// <summary>.NET Standard 2.1 reference assemblies (<c>NetStandard/ref/2.1.0</c> and <c>NetStandard/compat/2.1.0/shims/netfx</c>).</summary>
    public IReadOnlyList<string> NetStandardReferences { get; init; } = [];

    /// <summary>.NET Framework 4.8 reference assemblies (<c>UnityReferenceAssemblies/unity-4.8-api</c> and its <c>Facades</c>).</summary>
    public IReadOnlyList<string> NetFrameworkReferences { get; init; } = [];
}
