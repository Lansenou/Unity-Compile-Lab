using Ucl.Core.Model;

namespace Ucl.Core.Results;

/// <summary>The outcome of a whole <c>ucl check</c> run.</summary>
public sealed record RunResult
{
    /// <summary><c>ucl</c> version.</summary>
    public required string ToolVersion { get; init; }

    /// <summary>Cells in matrix order.</summary>
    public IReadOnlyList<CellResult> Cells { get; init; } = [];

    /// <summary>Problems that stopped the run before any cell (no project, bad arguments).</summary>
    public IReadOnlyList<Problem> Problems { get; init; } = [];

    /// <summary>Whether elapsed times are reported.</summary>
    public bool Timings { get; init; }

    /// <summary>Overall exit code: the most severe of the cells' and the run's.</summary>
    public int ExitCode { get; init; }
}
