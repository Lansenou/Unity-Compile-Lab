namespace Ucl.Core.Model;

/// <summary>One diagnostic, located by project-relative path with <c>/</c> separators.</summary>
/// <param name="Id">Diagnostic id, such as <c>CS0103</c> or <c>UCL1001</c>.</param>
/// <param name="Severity">Effective severity.</param>
/// <param name="Origin">Producer.</param>
/// <param name="Assembly">Assembly being compiled, if any.</param>
/// <param name="File">Project-relative file, or null.</param>
/// <param name="Line">1-based line, or 0 when there is no position.</param>
/// <param name="Column">1-based column, or 0 when there is no position.</param>
/// <param name="Message">Message text.</param>
/// <param name="WarningAsError">True when the error is a warning promoted by <c>-warnaserror</c>.</param>
public sealed record Diagnostic(
    string Id,
    Severity Severity,
    DiagnosticOrigin Origin,
    string? Assembly,
    string? File,
    int Line,
    int Column,
    string Message,
    bool WarningAsError = false);
