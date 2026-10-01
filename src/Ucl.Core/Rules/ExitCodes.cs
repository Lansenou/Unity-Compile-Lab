using Ucl.Core.Model;

namespace Ucl.Core.Rules;

/// <summary>The exit-code contract (README, <c>ucl --help</c>).</summary>
public static class ExitCodes
{
    /// <summary>No errors (warnings allowed).</summary>
    public const int Clean = 0;

    /// <summary>Compile, analyzer or <c>ucl</c> rule errors.</summary>
    public const int Errors = 1;

    /// <summary>Only warnings promoted to errors.</summary>
    public const int WarningsAsErrors = 2;

    /// <summary>Configuration problem.</summary>
    public const int Configuration = 3;

    /// <summary>Internal error (a bug in <c>ucl</c>).</summary>
    public const int Internal = 4;

    /// <summary>Computes the exit code: configuration problems win, then real errors, then promoted warnings.</summary>
    public static int Compute(IEnumerable<Problem> problems, IEnumerable<Diagnostic> diagnostics)
    {
        if (problems.Any())
        {
            return Configuration;
        }

        var errors = diagnostics.Where(d => d.Severity == Severity.Error).ToList();
        if (errors.Any(e => !e.WarningAsError))
        {
            return Errors;
        }

        return errors.Count > 0 ? WarningsAsErrors : Clean;
    }
}
