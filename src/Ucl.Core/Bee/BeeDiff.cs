namespace Ucl.Core.Bee;

/// <summary>Compares a Bee command line with ucl's (both normalised). Pure and deterministic: results are sorted.</summary>
public static class BeeDiff
{
    /// <summary>Every difference, sorted by category, change and value.</summary>
    /// <param name="bee">The Editor's command line.</param>
    /// <param name="ucl">ucl's command line for the same assembly and cell.</param>
    public static IReadOnlyList<BeeDifference> Compare(CommandLine bee, CommandLine ucl)
    {
        ArgumentNullException.ThrowIfNull(bee);
        ArgumentNullException.ThrowIfNull(ucl);
        var result = new List<BeeDifference>();
        Sets(BeeCategory.Sources, bee.Sources, ucl.Sources, StringComparer.Ordinal, result);
        References(bee.References, ucl.References, result);
        Sets(BeeCategory.Defines, bee.Defines, ucl.Defines, StringComparer.Ordinal, result);
        Sets(BeeCategory.NoWarn, bee.NoWarn.Select(CommandLine.WarningId), ucl.NoWarn.Select(CommandLine.WarningId), StringComparer.Ordinal, result);
        Sets(BeeCategory.Analyzers, bee.Analyzers, ucl.Analyzers, StringComparer.OrdinalIgnoreCase, result);
        Sets(BeeCategory.AdditionalFiles, bee.AdditionalFiles, ucl.AdditionalFiles, StringComparer.OrdinalIgnoreCase, result);
        if (!string.Equals(bee.LangVersion ?? "default", ucl.LangVersion ?? "default", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(new BeeDifference(BeeCategory.Options, BeeChange.Changed, "langversion", bee.LangVersion ?? "default", ucl.LangVersion ?? "default"));
        }

        if (bee.Unsafe != ucl.Unsafe)
        {
            result.Add(new BeeDifference(BeeCategory.Options, BeeChange.Changed, "unsafe", bee.Unsafe ? "on" : "off", ucl.Unsafe ? "on" : "off"));
        }

        return Sort(result);
    }

    /// <summary>Stable order: category, change, value, expected, actual (ordinal).</summary>
    public static IReadOnlyList<BeeDifference> Sort(IEnumerable<BeeDifference> differences) =>
        differences
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Change)
            .ThenBy(d => d.Value, StringComparer.Ordinal)
            .ThenBy(d => d.Expected, StringComparer.Ordinal)
            .ThenBy(d => d.Actual, StringComparer.Ordinal)
            .ToList();

    private static void Sets(BeeCategory category, IEnumerable<string> bee, IEnumerable<string> ucl, StringComparer comparer, List<BeeDifference> result)
    {
        var b = bee.ToHashSet(comparer);
        var u = ucl.ToHashSet(comparer);
        result.AddRange(b.Where(x => !u.Contains(x)).Select(x => new BeeDifference(category, BeeChange.Missing, x)));
        result.AddRange(u.Where(x => !b.Contains(x)).Select(x => new BeeDifference(category, BeeChange.Extra, x)));
    }

    // References match by identity: project assemblies by name; DLLs by file name and version (an unknown version
    // matches any), then their locations are compared.
    private static void References(IReadOnlyList<ReferenceEntry> bee, IReadOnlyList<ReferenceEntry> ucl, List<BeeDifference> result)
    {
        Sets(BeeCategory.References, bee.Where(r => r.Assembly is not null).Select(r => r.Display), ucl.Where(r => r.Assembly is not null).Select(r => r.Display), StringComparer.Ordinal, result);
        var remaining = ucl.Where(r => r.Assembly is null).OrderBy(r => r.Location, StringComparer.Ordinal).ToList();
        var unmatched = new List<ReferenceEntry>();
        foreach (var b in bee.Where(r => r.Assembly is null).OrderBy(r => r.Location, StringComparer.Ordinal))
        {
            var sameName = remaining.Where(u => u.FileName.Equals(b.FileName, StringComparison.OrdinalIgnoreCase)).ToList();
            var match = sameName.FirstOrDefault(u => u.Location.Equals(b.Location, StringComparison.OrdinalIgnoreCase) && VersionsMatch(b, u))
                ?? sameName.FirstOrDefault(u => VersionsMatch(b, u));
            if (match is null)
            {
                unmatched.Add(b);
                continue;
            }

            remaining.Remove(match);
            if (!match.Location.Equals(b.Location, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new BeeDifference(BeeCategory.References, BeeChange.Changed, b.FileName, b.Location, match.Location));
            }
        }

        // A file name on both sides with different versions is a version change, not a missing and an extra.
        foreach (var b in unmatched)
        {
            var other = remaining.FirstOrDefault(u => u.FileName.Equals(b.FileName, StringComparison.OrdinalIgnoreCase));
            if (other is not null)
            {
                remaining.Remove(other);
                result.Add(new BeeDifference(BeeCategory.References, BeeChange.Changed, b.FileName, $"{b.Version} ({b.Location})", $"{other.Version} ({other.Location})"));
            }
            else
            {
                result.Add(new BeeDifference(BeeCategory.References, BeeChange.Missing, b.Display));
            }
        }

        result.AddRange(remaining.Select(u => new BeeDifference(BeeCategory.References, BeeChange.Extra, u.Display)));
    }

    private static bool VersionsMatch(ReferenceEntry a, ReferenceEntry b) =>
        a.Version.Length == 0 || b.Version.Length == 0 || a.Version == b.Version;
}
