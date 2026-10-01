namespace Ucl.Core.Model;

/// <summary>Which component produced a diagnostic. Reported separately so analyzer findings never hide compiler errors.</summary>
public enum DiagnosticOrigin
{
    /// <summary>The C# compiler (CSxxxx).</summary>
    Compiler,

    /// <summary>A Roslyn analyzer or source generator.</summary>
    Analyzer,

    /// <summary><c>ucl</c> itself (UCLxxxx): Unity rules such as missing references.</summary>
    Ucl,
}
