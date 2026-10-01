using Ucl.Core.Model;
using Ucl.Core.Rules;

namespace Ucl.Core.Tests;

public class ExitCodesTests
{
    private static Diagnostic D(Severity severity, bool warnAsError = false) =>
        new("CS0001", severity, DiagnosticOrigin.Compiler, "A", "Assets/A.cs", 1, 1, "m", warnAsError);

    [Fact]
    public void Constants_are_the_documented_contract()
    {
        Assert.Equal(0, ExitCodes.Clean);
        Assert.Equal(1, ExitCodes.Errors);
        Assert.Equal(2, ExitCodes.WarningsAsErrors);
        Assert.Equal(3, ExitCodes.Configuration);
        Assert.Equal(4, ExitCodes.Internal);
    }

    [Fact]
    public void Nothing_is_clean() => Assert.Equal(ExitCodes.Clean, ExitCodes.Compute([], []));

    [Fact]
    public void Warnings_and_info_are_clean() =>
        Assert.Equal(ExitCodes.Clean, ExitCodes.Compute([], [D(Severity.Warning), D(Severity.Info)]));

    [Fact]
    public void Problems_win_over_errors() =>
        Assert.Equal(ExitCodes.Configuration, ExitCodes.Compute([new Problem(ProblemIds.BadAsmdef, "bad")], [D(Severity.Error)]));

    [Fact]
    public void Problem_alone_is_configuration() =>
        Assert.Equal(ExitCodes.Configuration, ExitCodes.Compute([new Problem(ProblemIds.EditorNotFound, "no editor", null)], []));

    [Fact]
    public void Real_error_is_errors() =>
        Assert.Equal(ExitCodes.Errors, ExitCodes.Compute([], [D(Severity.Warning), D(Severity.Error)]));

    [Fact]
    public void Real_error_wins_over_promoted_warning() =>
        Assert.Equal(ExitCodes.Errors, ExitCodes.Compute([], [D(Severity.Error, warnAsError: true), D(Severity.Error)]));

    [Fact]
    public void Only_promoted_warnings_is_warnings_as_errors() =>
        Assert.Equal(ExitCodes.WarningsAsErrors, ExitCodes.Compute([], [D(Severity.Error, warnAsError: true), D(Severity.Warning)]));
}
