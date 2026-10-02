using Ucl.Core.Graph;
using Ucl.Core.Model;

namespace Ucl.Core.Results;

/// <summary>The outcome of one assembly in one cell.</summary>
public sealed record AssemblyResult
{
    /// <summary>Assembly name.</summary>
    public required string Name { get; init; }

    /// <summary>Predefined or asmdef.</summary>
    public AssemblyKind Kind { get; init; }

    /// <summary>asmdef path, or null.</summary>
    public string? DefinitionPath { get; init; }

    /// <summary>Outcome.</summary>
    public AssemblyStatus Status { get; init; }

    /// <summary>Why it was skipped, or null.</summary>
    public string? SkipReason { get; init; }

    /// <summary>For a skipped assembly: the failed assemblies (root failures) that it depends on, directly or through other skipped ones; sorted.</summary>
    public IReadOnlyList<string> BlockedBy { get; init; } = [];

    /// <summary>SHA-256 (hex) of every input: sources, references, defines, options.</summary>
    public string InputsHash { get; init; } = string.Empty;

    /// <summary>Defines, sorted.</summary>
    public IReadOnlyList<string> Defines { get; init; } = [];

    /// <summary>References as stable display strings (<c>assembly:</c>, <c>plugin:</c>, <c>editor:</c>, <c>profile:</c>), sorted.</summary>
    public IReadOnlyList<string> References { get; init; } = [];

    /// <summary>Analyzer DLLs (project paths), sorted.</summary>
    public IReadOnlyList<string> Analyzers { get; init; } = [];

    /// <summary>Number of source files.</summary>
    public int SourceCount { get; init; }

    /// <summary>Diagnostics of this assembly.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    /// <summary>Wall-clock milliseconds; reported only with <c>--timings</c>.</summary>
    public long ElapsedMs { get; init; }

    /// <summary>True when the result came from the incremental cache.</summary>
    public bool Cached { get; init; }
}
