using Ucl.Core.Model;

namespace Ucl.Core.Results;

/// <summary>The one deterministic diagnostic order used by every output format.</summary>
public static class DiagnosticOrder
{
    /// <summary>Sorts by file, line, column, id, assembly, message (ordinal); diagnostics without a file come first.</summary>
    public static IReadOnlyList<Diagnostic> Sort(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics
            .OrderBy(d => d.File ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(d => d.Line)
            .ThenBy(d => d.Column)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ThenBy(d => d.Assembly ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(d => d.Message, StringComparer.Ordinal)
            .ToList();
}
