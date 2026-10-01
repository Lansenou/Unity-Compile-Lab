using Ucl.Core.Model;

namespace Ucl.Core.Results;

/// <summary>The outcome of one matrix cell.</summary>
public sealed record CellResult
{
    /// <summary>The cell.</summary>
    public required CompileCell Cell { get; init; }

    /// <summary>Assemblies in compile order.</summary>
    public IReadOnlyList<AssemblyResult> Assemblies { get; init; } = [];

    /// <summary>Assemblies not compiled in this cell, with reasons.</summary>
    public IReadOnlyDictionary<string, string> Excluded { get; init; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

    /// <summary>All diagnostics of the cell (planning and compilation), in report order.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    /// <summary>Configuration problems of the cell.</summary>
    public IReadOnlyList<Problem> Problems { get; init; } = [];

    /// <summary>Exit code of the cell alone.</summary>
    public int ExitCode { get; init; }
}
