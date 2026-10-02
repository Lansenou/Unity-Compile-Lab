using Ucl.Core.Model;
using Ucl.Core.Rules;

namespace Ucl.Core.Bee;

/// <summary>A whole <c>ucl bee-diff</c> run.</summary>
public sealed record BeeDiffResult
{
    /// <summary>Tool version.</summary>
    public required string ToolVersion { get; init; }

    /// <summary>Dags sorted by name.</summary>
    public IReadOnlyList<BeeDagResult> Dags { get; init; } = [];

    /// <summary>Configuration problems (no Bee folder, no editor, ...).</summary>
    public IReadOnlyList<Problem> Problems { get; init; } = [];

    /// <summary>Number of differences over every dag.</summary>
    public int DifferenceCount => Dags.Sum(d => d.Assemblies.Sum(a => a.Differences.Count));

    /// <summary>0 when every compared assembly agrees, 1 on any difference, 3 on a configuration problem.</summary>
    public int ExitCode => Problems.Count > 0 ? ExitCodes.Configuration : DifferenceCount > 0 ? ExitCodes.Errors : ExitCodes.Clean;
}
