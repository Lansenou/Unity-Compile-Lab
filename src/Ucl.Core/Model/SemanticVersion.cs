using System.Globalization;
using System.Text.RegularExpressions;

namespace Ucl.Core.Model;

/// <summary>A package version as Unity compares them: up to three numeric parts and an optional pre-release tag.</summary>
/// <param name="Major">Major part.</param>
/// <param name="Minor">Minor part; 0 when omitted.</param>
/// <param name="Patch">Patch part; 0 when omitted.</param>
/// <param name="PreRelease">Pre-release tag without the dash, or empty.</param>
public sealed partial record SemanticVersion(int Major, int Minor, int Patch, string PreRelease) : IComparable<SemanticVersion>
{
    /// <summary>Parses <c>1</c>, <c>1.2</c>, <c>1.2.3</c> or <c>1.2.3-pre.1</c> (build metadata after <c>+</c> is ignored).</summary>
    public static Result<SemanticVersion> Parse(string? text)
    {
        var m = Pattern().Match(text?.Trim() ?? string.Empty);
        if (!m.Success)
        {
            return Result<SemanticVersion>.Failure($"'{text}' is not a version");
        }

        static int Num(Group g) => g.Success ? int.Parse(g.Value, CultureInfo.InvariantCulture) : 0;
        return Result<SemanticVersion>.Success(new SemanticVersion(Num(m.Groups[1]), Num(m.Groups[2]), Num(m.Groups[3]), m.Groups[4].Value));
    }

    /// <inheritdoc/>
    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var c = Major.CompareTo(other.Major);
        if (c == 0) c = Minor.CompareTo(other.Minor);
        if (c == 0) c = Patch.CompareTo(other.Patch);
        return c != 0 ? c : ComparePreRelease(PreRelease, other.PreRelease);
    }

    /// <inheritdoc/>
    public override string ToString() => PreRelease.Length == 0 ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";

    // SemVer 2.0 section 11: a release sorts above its pre-releases; identifiers compare numerically when both are numbers.
    private static int ComparePreRelease(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return Math.Sign(b.Length.CompareTo(a.Length));
        }

        var pa = a.Split('.');
        var pb = b.Split('.');
        for (var i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            var na = int.TryParse(pa[i], NumberStyles.None, CultureInfo.InvariantCulture, out var ia);
            var nb = int.TryParse(pb[i], NumberStyles.None, CultureInfo.InvariantCulture, out var ib);
            var c = (na, nb) switch
            {
                (true, true) => ia.CompareTo(ib),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(pa[i], pb[i]),
            };
            if (c != 0)
            {
                return Math.Sign(c);
            }
        }

        return pa.Length.CompareTo(pb.Length);
    }

    [GeneratedRegex(@"^(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$")]
    private static partial Regex Pattern();
}
