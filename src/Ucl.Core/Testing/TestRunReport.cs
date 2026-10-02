using Ucl.Core.Model;
using Ucl.Core.Results;

namespace Ucl.Core.Testing;

/// <summary>Everything <c>ucl test</c> reports: the compile of the test assemblies, then every test case.</summary>
public sealed record TestRunReport
{
    /// <summary>Tool version.</summary>
    public required string ToolVersion { get; init; }

    /// <summary>The cell the tests were compiled for (an editor cell), or null when the project did not load.</summary>
    public CompileCell? Cell { get; init; }

    /// <summary>The compile of the test assemblies and their dependencies, or null.</summary>
    public CellResult? Compile { get; init; }

    /// <summary>Every discovered case, sorted by assembly then full name.</summary>
    public IReadOnlyList<TestCaseResult> Cases { get; init; } = [];

    /// <summary>Test assemblies run (names, sorted).</summary>
    public IReadOnlyList<string> Assemblies { get; init; } = [];

    /// <summary>Every time the test host process died; the run continued in a new host after each.</summary>
    public IReadOnlyList<TestHostCrash> HostCrashes { get; init; } = [];

    /// <summary>Configuration problems.</summary>
    public IReadOnlyList<Problem> Problems { get; init; } = [];

    /// <summary>Report per-case durations.</summary>
    public bool Timings { get; init; }

    /// <summary>Number of cases in <paramref name="category"/>.</summary>
    public int Count(TestCategory category) => Cases.Count(c => c.Category == category);

    /// <summary>3 on a configuration problem, 1 when a test assembly does not compile or a case failed for a real reason, else 0.</summary>
    public int ExitCode =>
        Problems.Count > 0 ? Rules.ExitCodes.Configuration
        : Compile is { ExitCode: not 0 } ? Compile.ExitCode
        : Count(TestCategory.Failed) > 0 ? Rules.ExitCodes.Errors
        : Rules.ExitCodes.Clean;
}
