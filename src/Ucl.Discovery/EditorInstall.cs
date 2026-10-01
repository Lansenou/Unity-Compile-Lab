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

    /// <summary>.NET Standard 2.1 reference assemblies (<see cref="Ucl.Core.Rules.ReferenceProfiles"/>: <c>netstandard.dll</c>, the shims and extensions).</summary>
    public IReadOnlyList<string> NetStandardReferences { get; init; } = [];

    /// <summary><c>NetStandard/EditorExtensions</c> DLLs, referenced by Editor-only assemblies compiled against .NET Standard.</summary>
    public IReadOnlyList<string> NetStandardEditorExtensions { get; init; } = [];

    /// <summary>.NET Framework 4.8 reference assemblies (<c>unity-4.8-api</c>: the listed core libraries and every facade).</summary>
    public IReadOnlyList<string> NetFrameworkReferences { get; init; } = [];
}
