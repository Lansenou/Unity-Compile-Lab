using Ucl.Core.Model;

namespace Ucl.Core.Tests;

/// <summary>Property tests with a fixed-seed generator: version ranges and the semantic version order.</summary>
public class VersionRangePropertyTests
{
    private const int Cases = 600;

    private static readonly string[] PreReleases = ["", "", "", "alpha", "alpha.1", "beta", "beta.2", "beta.11", "preview.7", "pre.10", "rc.1", "1", "exp.2.a"];

    private static SemanticVersion RandomVersion(Random rng) =>
        new(rng.Next(0, 4), rng.Next(0, 4), rng.Next(0, 4), PreReleases[rng.Next(PreReleases.Length)]);

    // Independent comparison: numeric parts, then a release above any pre-release, then SemVer identifier rules.
    private static int Reference(SemanticVersion a, SemanticVersion b)
    {
        int[] na = [a.Major, a.Minor, a.Patch], nb = [b.Major, b.Minor, b.Patch];
        for (var i = 0; i < 3; i++)
        {
            if (na[i] != nb[i]) return na[i] < nb[i] ? -1 : 1;
        }

        if (a.PreRelease == b.PreRelease) return 0;
        if (a.PreRelease.Length == 0) return 1;
        if (b.PreRelease.Length == 0) return -1;
        var pa = a.PreRelease.Split('.');
        var pb = b.PreRelease.Split('.');
        for (var i = 0; i < pa.Length && i < pb.Length; i++)
        {
            var aNum = pa[i].All(char.IsAsciiDigit);
            var bNum = pb[i].All(char.IsAsciiDigit);
            int c;
            if (aNum && bNum) c = long.Parse(pa[i]).CompareTo(long.Parse(pb[i]));
            else if (aNum) c = -1;
            else if (bNum) c = 1;
            else c = string.CompareOrdinal(pa[i], pb[i]);
            if (c != 0) return Math.Sign(c);
        }

        return Math.Sign(pa.Length - pb.Length);
    }

    private static (SemanticVersion Lo, SemanticVersion Hi) RandomBounds(Random rng)
    {
        var a = RandomVersion(rng);
        var b = RandomVersion(rng);
        return Reference(a, b) <= 0 ? (a, b) : (b, a);
    }

    private static VersionRange Parse(string text)
    {
        var r = VersionRange.Parse(text);
        Assert.True(r.Ok, text);
        return r.Value!;
    }

    [Fact]
    public void Closed_open_interval_contains_v_iff_lo_le_v_lt_hi()
    {
        var rng = new Random(1001);
        for (var n = 0; n < Cases; n++)
        {
            var (lo, hi) = RandomBounds(rng);
            var v = RandomVersion(rng);
            var range = Parse($"[{lo},{hi})");
            Assert.Equal(Reference(lo, v) <= 0 && Reference(v, hi) < 0, range.Contains(v));
        }
    }

    [Fact]
    public void Open_closed_interval_contains_v_iff_lo_lt_v_le_hi()
    {
        var rng = new Random(1002);
        for (var n = 0; n < Cases; n++)
        {
            var (lo, hi) = RandomBounds(rng);
            var v = RandomVersion(rng);
            var range = Parse($"({lo},{hi}]");
            Assert.Equal(Reference(lo, v) < 0 && Reference(v, hi) <= 0, range.Contains(v));
        }
    }

    [Fact]
    public void Bare_bound_contains_v_iff_v_ge_lo()
    {
        var rng = new Random(1003);
        for (var n = 0; n < Cases; n++)
        {
            var lo = RandomVersion(rng);
            var v = RandomVersion(rng);
            Assert.Equal(Reference(v, lo) >= 0, Parse(lo.ToString()).Contains(v));
        }
    }

    [Fact]
    public void Exact_contains_v_iff_v_eq_lo()
    {
        var rng = new Random(1004);
        for (var n = 0; n < Cases; n++)
        {
            var lo = RandomVersion(rng);
            var v = rng.Next(4) == 0 ? lo with { } : RandomVersion(rng);
            Assert.Equal(Reference(v, lo) == 0, Parse($"[{lo}]").Contains(v));
        }
    }

    [Fact]
    public void Open_ended_intervals()
    {
        var rng = new Random(1005);
        for (var n = 0; n < Cases; n++)
        {
            var b = RandomVersion(rng);
            var v = RandomVersion(rng);
            Assert.Equal(Reference(v, b) < 0, Parse($"(,{b})").Contains(v));
            Assert.Equal(Reference(v, b) >= 0, Parse($"[{b},)").Contains(v));
        }
    }

    [Fact]
    public void Comparison_matches_reference_and_is_antisymmetric()
    {
        var rng = new Random(2001);
        for (var n = 0; n < Cases; n++)
        {
            var a = RandomVersion(rng);
            var b = RandomVersion(rng);
            var ab = Math.Sign(a.CompareTo(b));
            Assert.Equal(Reference(a, b), ab);
            Assert.Equal(-ab, Math.Sign(b.CompareTo(a)));
            Assert.Equal(0, a.CompareTo(a));
        }
    }

    [Fact]
    public void Comparison_is_transitive()
    {
        var rng = new Random(2002);
        for (var n = 0; n < Cases; n++)
        {
            var xs = new[] { RandomVersion(rng), RandomVersion(rng), RandomVersion(rng) };
            Array.Sort(xs, (x, y) => x.CompareTo(y));
            Assert.True(xs[0].CompareTo(xs[1]) <= 0);
            Assert.True(xs[1].CompareTo(xs[2]) <= 0);
            Assert.True(xs[0].CompareTo(xs[2]) <= 0);
        }
    }

    [Fact]
    public void Pre_release_sorts_below_its_release()
    {
        var rng = new Random(2003);
        for (var n = 0; n < Cases; n++)
        {
            var v = RandomVersion(rng);
            var release = v with { PreRelease = "" };
            var pre = v with { PreRelease = PreReleases[3 + rng.Next(PreReleases.Length - 3)] };
            Assert.True(pre.CompareTo(release) < 0);
            Assert.True(release.CompareTo(pre) > 0);
        }
    }

    [Fact]
    public void ToString_then_Parse_round_trips()
    {
        var rng = new Random(2004);
        for (var n = 0; n < Cases; n++)
        {
            var v = RandomVersion(rng);
            Assert.Equal(v, SemanticVersion.Parse(v.ToString()).Value);
        }
    }
}
