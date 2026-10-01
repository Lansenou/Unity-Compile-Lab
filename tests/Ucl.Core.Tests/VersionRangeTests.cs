using Ucl.Core.Model;

namespace Ucl.Core.Tests;

public class VersionRangeTests
{
    private static bool Matches(string expression, string version) =>
        VersionRange.Parse(expression).Value!.Contains(SemanticVersion.Parse(version).Value!);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_expression_matches_any_version(string? expression)
    {
        var r = VersionRange.Parse(expression);
        Assert.True(r.Ok);
        Assert.Same(VersionRange.Any, r.Value);
        Assert.True(r.Value!.Contains(new SemanticVersion(0, 0, 0, "a")));
        Assert.True(r.Value!.Contains(new SemanticVersion(99, 0, 0, "")));
    }

    [Theory]
    [InlineData("1.2", "1.2.0", true)]
    [InlineData("1.2", "1.1.9", false)]
    [InlineData("1.2", "9.0", true)]
    [InlineData("1.2", "1.2.0-preview.1", false)]
    public void Bare_version_is_a_minimum(string expression, string version, bool expected)
    {
        Assert.Equal(expected, Matches(expression, version));
    }

    [Theory]
    [InlineData("[1.2]", "1.2.0", true)]
    [InlineData("[1.2]", "1.2.1", false)]
    [InlineData("[1.2]", "1.1.0", false)]
    public void Brackets_with_one_value_are_exact(string expression, string version, bool expected)
    {
        Assert.Equal(expected, Matches(expression, version));
    }

    [Theory]
    [InlineData("[1.2,2.0)", "1.2", true)]
    [InlineData("[1.2,2.0)", "2.0", false)]
    [InlineData("[1.2,2.0)", "1.9.9", true)]
    [InlineData("(1.2,2.0]", "1.2", false)]
    [InlineData("(1.2,2.0]", "2.0", true)]
    [InlineData("[1.2,2.0]", "2.0", true)]
    [InlineData("[1.2,2.0]", "1.2", true)]
    [InlineData("(1.2,2.0)", "1.2", false)]
    [InlineData("(1.2,2.0)", "2.0", false)]
    [InlineData("(1.2,2.0)", "1.5", true)]
    [InlineData("(,2.0)", "0.0.1", true)]
    [InlineData("(,2.0)", "2.0", false)]
    [InlineData("[1.0,)", "1.0", true)]
    [InlineData("[1.0,)", "0.9", false)]
    [InlineData("[1.0,)", "100.0", true)]
    [InlineData("[1.0, 2.0)", "1.5", true)]
    [InlineData("[2.1.0-preview.1,2.1.0)", "2.1.0-preview.7", true)]
    [InlineData("[2.1.0-preview.1,2.1.0)", "2.1.0", false)]
    [InlineData("[1.2,1.2]", "1.2", true)]
    public void Intervals(string expression, string version, bool expected)
    {
        Assert.Equal(expected, Matches(expression, version));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("[1.2")]
    [InlineData("[")]
    [InlineData("[]")]
    [InlineData("(1.2)")]
    [InlineData("[1.2)")]
    [InlineData("[x]")]
    [InlineData("[,]")]
    [InlineData("[1,2,3]")]
    [InlineData("[a,2.0)")]
    [InlineData("[1.0,b)")]
    [InlineData("[2.0,1.0]")]
    [InlineData("[1.0,2.0}")]
    public void Rejects_malformed_expressions(string expression)
    {
        var r = VersionRange.Parse(expression);
        Assert.False(r.Ok);
        Assert.Contains("is not a version expression", r.Error);
    }

    [Fact]
    public void Parsed_interval_records_bounds()
    {
        var r = VersionRange.Parse("(1.2,2.0]").Value!;
        Assert.Equal(new SemanticVersion(1, 2, 0, ""), r.Min);
        Assert.False(r.MinInclusive);
        Assert.Equal(new SemanticVersion(2, 0, 0, ""), r.Max);
        Assert.True(r.MaxInclusive);
    }
}
