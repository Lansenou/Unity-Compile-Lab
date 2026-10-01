namespace Ucl.Core.Parsing;

/// <summary>The fields of an <c>.asmdef</c> file that affect compilation.</summary>
public sealed record AsmdefData
{
    /// <summary>Assembly name.</summary>
    public required string Name { get; init; }

    /// <summary>References by name or <c>GUID:</c>.</summary>
    public IReadOnlyList<string> References { get; init; } = [];

    /// <summary>Platforms the assembly is compiled for (empty means all).</summary>
    public IReadOnlyList<string> IncludePlatforms { get; init; } = [];

    /// <summary>Platforms the assembly is excluded from.</summary>
    public IReadOnlyList<string> ExcludePlatforms { get; init; } = [];

    /// <summary>Allows <c>unsafe</c> code.</summary>
    public bool AllowUnsafeCode { get; init; }

    /// <summary>When true only <see cref="PrecompiledReferences"/> are referenced.</summary>
    public bool OverrideReferences { get; init; }

    /// <summary>DLL file names referenced when <see cref="OverrideReferences"/> is set.</summary>
    public IReadOnlyList<string> PrecompiledReferences { get; init; } = [];

    /// <summary>Whether predefined assemblies reference this assembly.</summary>
    public bool AutoReferenced { get; init; } = true;

    /// <summary>Constraints that must hold for the assembly to be compiled.</summary>
    public IReadOnlyList<string> DefineConstraints { get; init; } = [];

    /// <summary>Version defines.</summary>
    public IReadOnlyList<VersionDefine> VersionDefines { get; init; } = [];

    /// <summary>Removes UnityEngine and UnityEditor references.</summary>
    public bool NoEngineReferences { get; init; }

    /// <summary>Legacy <c>optionalUnityReferences</c> (for example <c>TestAssemblies</c>).</summary>
    public IReadOnlyList<string> OptionalUnityReferences { get; init; } = [];
}
