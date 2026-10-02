namespace Ucl.Core.Results;

/// <summary>Roslyn's cumulative callback execution time for one analyzer in one assembly.</summary>
/// <param name="Path">Stable analyzer DLL display path (project-relative or editor-relative).</param>
/// <param name="Analyzer">Fully qualified analyzer type name.</param>
/// <param name="TimeMs">Callback execution milliseconds; concurrent callbacks are accumulated, not wall-clock time.</param>
public sealed record AnalyzerTiming(string Path, string Analyzer, double TimeMs)
{
    /// <summary>Supported diagnostic IDs (suppressed diagnostic IDs for a suppressor); time is shared, not measured per rule.</summary>
    public IReadOnlyList<string> RuleIds { get; init; } = [];
}
