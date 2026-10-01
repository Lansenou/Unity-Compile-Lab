using Ucl.Core.Model;

namespace Ucl.Core.Tests;

public class SemanticVersionTests
{
    [Theory]
    [InlineData("1", 1, 0, 0, "")]
    [InlineData("1.2", 1, 2, 0, "")]
    [InlineData("1.2.3", 1, 2, 3, "")]
    [InlineData("2.1.0-preview.7", 2, 1, 0, "preview.7")]
    [InlineData("1.0.0-exp.1+build.5", 1, 0, 0, "exp.1")]
    [InlineData("3.0.0+meta", 3, 0, 0, "")]
    [InlineData(" 1.2.3 ", 1, 2, 3, "")]
    public void Parses(string text, int major, int minor, int patch, string pre)
    {
        var r = SemanticVersion.Parse(text);
        Assert.True(r.Ok);
        Assert.Equal(new SemanticVersion(major, minor, patch, pre), r.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("1.")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.3-")]
    [InlineData("https://github.com/x/y.git")]
    public void Rejects(string? text)
    {
        var r = SemanticVersion.Parse(text);
        Assert.False(r.Ok);
        Assert.Contains("is not a version", r.Error);
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("2.1.0-preview.7", "2.1.0-preview.7")]
    public void ToString_is_canonical(string text, string expected)
    {
        Assert.Equal(expected, SemanticVersion.Parse(text).Value!.ToString());
    }

    [Theory]
    [InlineData("1.2", "1.2.0", 0)]
    [InlineData("1.2.0", "1.10.0", -1)]
    [InlineData("2.0.0", "1.99.99", 1)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("2.1.0-preview.7", "2.1.0", -1)]
    [InlineData("2.1.0", "2.1.0-preview.7", 1)]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1", -1)]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta", -1)]
    [InlineData("1.0.0-alpha.beta", "1.0.0-beta", -1)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11", -1)]
    [InlineData("1.0.0-beta.11", "1.0.0-rc.1", -1)]
    [InlineData("1.0.0-pre.10", "1.0.0-pre.9", 1)]
    [InlineData("1.0.0-x", "1.0.0-x", 0)]
    public void Compares_like_semver(string a, string b, int expected)
    {
        var c = SemanticVersion.Parse(a).Value!.CompareTo(SemanticVersion.Parse(b).Value!);
        Assert.Equal(expected, Math.Sign(c));
    }

    [Fact]
    public void Compares_above_null()
    {
        Assert.Equal(1, new SemanticVersion(0, 0, 0, "").CompareTo(null));
    }
}
