namespace Ucl.Core.Model;

/// <summary>A <c>versionDefines</c> expression: a bare minimum version or an interval such as <c>[1.2,2.0)</c>.</summary>
/// <param name="Min">Lower bound, or null for none.</param>
/// <param name="MinInclusive">Whether <paramref name="Min"/> itself matches.</param>
/// <param name="Max">Upper bound, or null for none.</param>
/// <param name="MaxInclusive">Whether <paramref name="Max"/> itself matches.</param>
public sealed record VersionRange(SemanticVersion? Min, bool MinInclusive, SemanticVersion? Max, bool MaxInclusive)
{
    /// <summary>The range that matches every version (an empty expression).</summary>
    public static VersionRange Any { get; } = new(null, true, null, true);

    /// <summary>Parses an expression. Empty means any version; <c>1.2</c> means at least 1.2; <c>[1.2]</c> means exactly 1.2.</summary>
    public static Result<VersionRange> Parse(string? expression)
    {
        var text = expression?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return Result<VersionRange>.Success(Any);
        }

        var open = text[0];
        if (open != '[' && open != '(')
        {
            var bare = SemanticVersion.Parse(text);
            return bare.Ok ? Result<VersionRange>.Success(new VersionRange(bare.Value, true, null, true)) : Fail(text);
        }

        var close = text[^1];
        if (text.Length < 3 || (close != ']' && close != ')'))
        {
            return Fail(text);
        }

        var inner = text[1..^1];
        var parts = inner.Split(',');
        if (parts.Length == 1)
        {
            // [x] is the only single-value interval; (x) matches nothing and is rejected as Unity does.
            var exact = SemanticVersion.Parse(parts[0]);
            return open == '[' && close == ']' && exact.Ok
                ? Result<VersionRange>.Success(new VersionRange(exact.Value, true, exact.Value, true))
                : Fail(text);
        }

        if (parts.Length != 2 || (parts[0].Length == 0 && parts[1].Length == 0))
        {
            return Fail(text);
        }

        SemanticVersion? min = null, max = null;
        if (parts[0].Length > 0)
        {
            var r = SemanticVersion.Parse(parts[0]);
            if (!r.Ok) return Fail(text);
            min = r.Value;
        }

        if (parts[1].Length > 0)
        {
            var r = SemanticVersion.Parse(parts[1]);
            if (!r.Ok) return Fail(text);
            max = r.Value;
        }

        if (min is not null && max is not null && min.CompareTo(max) > 0)
        {
            return Fail(text);
        }

        return Result<VersionRange>.Success(new VersionRange(min, open == '[', max, close == ']'));
    }

    /// <summary>True when <paramref name="version"/> lies in the range.</summary>
    public bool Contains(SemanticVersion version)
    {
        if (Min is not null)
        {
            var c = version.CompareTo(Min);
            if (c < 0 || (c == 0 && !MinInclusive)) return false;
        }

        if (Max is not null)
        {
            var c = version.CompareTo(Max);
            if (c > 0 || (c == 0 && !MaxInclusive)) return false;
        }

        return true;
    }

    private static Result<VersionRange> Fail(string text) => Result<VersionRange>.Failure($"'{text}' is not a version expression");
}
