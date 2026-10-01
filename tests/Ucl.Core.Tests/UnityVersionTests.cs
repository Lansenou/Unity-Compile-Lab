using Ucl.Core.Model;

namespace Ucl.Core.Tests;

public class UnityVersionTests
{
    [Theory]
    [InlineData("6000.0.30f1", 6000, 0, 30, "f1")]
    [InlineData("6000.1.2b3", 6000, 1, 2, "b3")]
    [InlineData("6000.0.30", 6000, 0, 30, "")]
    [InlineData("6000.2", 6000, 2, 0, "")]
    [InlineData("  6000.3.1p2 ", 6000, 3, 1, "p2")]
    [InlineData("2022.3.10a1", 2022, 3, 10, "a1")]
    public void Parses_valid_versions(string text, int major, int minor, int patch, string suffix)
    {
        var r = UnityVersion.Parse(text);
        Assert.True(r.Ok);
        Assert.Null(r.Error);
        Assert.Equal(new UnityVersion(major, minor, patch, suffix), r.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("6000")]
    [InlineData("6000.0.30f")]
    [InlineData("6000.0.30z1")]
    [InlineData("v6000.0.30f1")]
    [InlineData("6000.0.30f1 (abc)")]
    public void Rejects_invalid_versions(string? text)
    {
        var r = UnityVersion.Parse(text);
        Assert.False(r.Ok);
        Assert.Contains("is not a Unity version", r.Error);
        Assert.Null(r.Value);
    }

    [Fact]
    public void IsUnity6_only_for_6000()
    {
        Assert.True(new UnityVersion(6000, 0, 0, "").IsUnity6);
        Assert.False(new UnityVersion(2022, 3, 0, "f1").IsUnity6);
    }

    [Fact]
    public void ToString_round_trips()
    {
        Assert.Equal("6000.0.30f1", UnityVersion.Parse("6000.0.30f1").Value!.ToString());
        Assert.Equal("6000.2.0", UnityVersion.Parse("6000.2").Value!.ToString());
    }

    [Fact]
    public void ToSemantic_drops_suffix()
    {
        Assert.Equal(new SemanticVersion(6000, 0, 30, ""), new UnityVersion(6000, 0, 30, "f1").ToSemantic());
    }

    [Theory]
    [InlineData("6000.0.30f1", "6000.0.30f1", 0)]
    [InlineData("6000.0.30f1", "6000.0.31f1", -1)]
    [InlineData("6000.1.0f1", "6000.0.99f1", 1)]
    [InlineData("6000.0.30f2", "6000.0.30f1", 1)]
    [InlineData("6000.0.30b1", "6000.0.30f1", -1)]
    [InlineData("2022.3.0f1", "6000.0.0f1", -1)]
    public void Compares_component_wise(string a, string b, int expected)
    {
        var c = UnityVersion.Parse(a).Value!.CompareTo(UnityVersion.Parse(b).Value!);
        Assert.Equal(expected, Math.Sign(c));
    }

    [Fact]
    public void Compares_above_null()
    {
        Assert.Equal(1, new UnityVersion(6000, 0, 0, "").CompareTo(null));
    }
}
