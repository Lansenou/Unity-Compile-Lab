namespace Ucl.Core.Model;

/// <summary>Diagnostic severity, ordered from least to most severe.</summary>
public enum Severity
{
    /// <summary>Informational message.</summary>
    Info,

    /// <summary>Warning; does not fail a run unless treated as an error.</summary>
    Warning,

    /// <summary>Error; fails the run.</summary>
    Error,
}
