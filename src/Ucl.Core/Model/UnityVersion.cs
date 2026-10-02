using System.Globalization;
using System.Text.RegularExpressions;

namespace Ucl.Core.Model;

/// <summary>A Unity editor version such as <c>6000.0.30f1</c>.</summary>
/// <param name="Major">Major version (6000 for Unity 6).</param>
/// <param name="Minor">Minor version.</param>
/// <param name="Patch">Patch number.</param>
/// <param name="Suffix">Release type and build, such as <c>f1</c>; may be empty.</param>
public sealed partial record UnityVersion(int Major, int Minor, int Patch, string Suffix) : IComparable<UnityVersion>
{
    /// <summary>True for Unity 6 (6000.x), the only supported generation.</summary>
    public bool IsUnity6 => Major == 6000;

    /// <summary>Parses <c>6000.0.30f1</c>, <c>6000.0.30</c> or <c>6000.0</c>.</summary>
    public static Result<UnityVersion> Parse(string? text)
    {
        var m = Pattern().Match(text?.Trim() ?? string.Empty);
        if (!m.Success)
        {
            return Result<UnityVersion>.Failure($"'{text}' is not a Unity version (expected e.g. 6000.0.30f1)");
        }

        int Num(Group g) => g.Success ? int.Parse(g.Value, CultureInfo.InvariantCulture) : 0;
        return Result<UnityVersion>.Success(new UnityVersion(Num(m.Groups[1]), Num(m.Groups[2]), Num(m.Groups[3]), m.Groups[4].Value));
    }

    /// <summary>Drops the release suffix (<c>a1</c>, <c>b2</c>, <c>f1</c>, <c>p3</c>) of every version in a versionDefines expression.</summary>
    public static string WithoutSuffixes(string expression) => SuffixPattern().Replace(expression, "$1");

    /// <summary>The version as a semantic version (suffix dropped), used by <c>versionDefines</c> with resource <c>Unity</c>.</summary>
    public SemanticVersion ToSemantic() => new(Major, Minor, Patch, string.Empty);

    /// <inheritdoc/>
    public int CompareTo(UnityVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var c = Major.CompareTo(other.Major);
        if (c == 0) c = Minor.CompareTo(other.Minor);
        if (c == 0) c = Patch.CompareTo(other.Patch);
        return c != 0 ? c : string.CompareOrdinal(Suffix, other.Suffix);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Major}.{Minor}.{Patch}{Suffix}";

    [GeneratedRegex(@"(\d+\.\d+\.\d+)[abfpx]\d+")]
    private static partial Regex SuffixPattern();

    [GeneratedRegex(@"^(\d+)\.(\d+)(?:\.(\d+)([abfpx]\d+)?)?$")]
    private static partial Regex Pattern();
}
