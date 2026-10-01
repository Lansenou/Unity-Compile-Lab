using Ucl.Core.Rules;

namespace Ucl.Core.Graph;

/// <summary>Everything needed to compile one assembly in one cell. Lists are sorted for deterministic output.</summary>
public sealed record AssemblyPlan
{
    /// <summary>Assembly name.</summary>
    public required string Name { get; init; }

    /// <summary>Predefined or asmdef.</summary>
    public required AssemblyKind Kind { get; init; }

    /// <summary>asmdef path, or null for predefined assemblies.</summary>
    public string? DefinitionPath { get; init; }

    /// <summary>Source files.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>Names of referenced assemblies compiled in the same cell.</summary>
    public IReadOnlyList<string> References { get; init; } = [];

    /// <summary>References that exist but are not compiled in this cell, so they were dropped (as Unity does).</summary>
    public IReadOnlyList<string> DroppedReferences { get; init; } = [];

    /// <summary>Precompiled DLL references (project paths).</summary>
    public IReadOnlyList<string> PrecompiledReferences { get; init; } = [];

    /// <summary>Analyzer DLLs (project paths).</summary>
    public IReadOnlyList<string> Analyzers { get; init; } = [];

    /// <summary>Editor DLLs to reference.</summary>
    public EngineReferences Engine { get; init; }

    /// <summary>Preprocessor symbols with reasons.</summary>
    public required DefineSet Defines { get; init; }

    /// <summary>Allow unsafe code.</summary>
    public bool AllowUnsafe { get; init; }

    /// <summary>C# language version.</summary>
    public string LangVersion { get; init; } = "9.0";

    /// <summary>Nullable context option (<c>disable</c>, <c>enable</c>, <c>warnings</c>, <c>annotations</c>).</summary>
    public string Nullable { get; init; } = "disable";

    /// <summary>Suppressed diagnostic ids.</summary>
    public IReadOnlyList<string> NoWarn { get; init; } = [];

    /// <summary>All warnings are errors.</summary>
    public bool WarnAsErrorAll { get; init; }

    /// <summary>Specific warnings that are errors.</summary>
    public IReadOnlyList<string> WarnAsErrorIds { get; init; } = [];

    /// <summary>Specific warnings exempt from <see cref="WarnAsErrorAll"/>.</summary>
    public IReadOnlyList<string> WarnNotAsErrorIds { get; init; } = [];

    /// <summary>Analyzer additional files (project paths).</summary>
    public IReadOnlyList<string> AdditionalFiles { get; init; } = [];

    /// <summary><c>.editorconfig</c>/<c>.globalconfig</c> files (project paths).</summary>
    public IReadOnlyList<string> AnalyzerConfigs { get; init; } = [];

    /// <summary>Rule set file (project path), or null.</summary>
    public string? RuleSet { get; init; }

    /// <summary>Response file applied, or null.</summary>
    public string? ResponseFile { get; init; }

    /// <summary>True for assemblies that only exist in the Editor.</summary>
    public bool IsEditorOnly { get; init; }

    /// <summary>API compatibility of this assembly: true for .NET Framework (<c>unity-4.8-api</c>), false for .NET Standard 2.1.</summary>
    public bool NetFramework { get; init; }
}
