using Xunit;

namespace Ucl.Integration.Tests;

/// <summary>Coverage gates preserve thresholds and reject missing/incomplete reports.</summary>
public sealed class CoverageGateTests
{
    /// <summary>Check exact counts, including a percentage that rounds up to the threshold but remains below it.</summary>
    [Theory]
    [InlineData(3, 4, 75, 0)]
    [InlineData(2, 3, 75, 1)]
    [InlineData(74999, 100000, 75, 1)]
    [InlineData(9, 10, 90, 0)]
    [InlineData(0, 0, 75, 2)]
    [InlineData(5, 4, 75, 2)]
    public void Exact_counts_enforce_the_threshold(long covered, long total, int minimum, int exit)
    {
        using var temp = new TempDir();
        var report = Path.Combine(temp.Path, "coverage.xml");
        File.WriteAllText(report, FormattableString.Invariant($"<coverage lines-covered=\"{covered}\" lines-valid=\"{total}\"/>"));
        Assert.Equal(exit, CoverageGate.Check([report, minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)]));
    }

    /// <summary>A missing report cannot make the gate succeed.</summary>
    [Fact]
    public void Missing_report_fails_closed()
    {
        using var temp = new TempDir();
        Assert.Equal(2, CoverageGate.Check([Path.Combine(temp.Path, "missing.xml"), "75"]));
    }
}
