namespace Ucl.Core.Bee;

/// <summary>
/// A compiler command line in comparable form: paths are project-logical (<c>Assets/...</c>, <c>Packages/&lt;name&gt;/...</c>),
/// <c>editor:&lt;path under the editor's data folder&gt;</c>, or absolute with <c>/</c>; diagnostic ids are <c>CSxxxx</c>.
/// Both sides of a Bee comparison are normalised to this before <see cref="BeeDiff.Compare"/> runs.
/// </summary>
public sealed record CommandLine
{
    /// <summary>Source files, logical.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>References: other assemblies of the project by name, DLLs by identity and location.</summary>
    public IReadOnlyList<ReferenceEntry> References { get; init; } = [];

    /// <summary>Preprocessor symbols.</summary>
    public IReadOnlyList<string> Defines { get; init; } = [];

    /// <summary>Suppressed diagnostics, <c>CSxxxx</c>.</summary>
    public IReadOnlyList<string> NoWarn { get; init; } = [];

    /// <summary>Analyzer and source generator DLL locations.</summary>
    public IReadOnlyList<string> Analyzers { get; init; } = [];

    /// <summary>Analyzer additional files, logical.</summary>
    public IReadOnlyList<string> AdditionalFiles { get; init; } = [];

    /// <summary>Language version.</summary>
    public string? LangVersion { get; init; }

    /// <summary>Unsafe code allowed.</summary>
    public bool Unsafe { get; init; }

    /// <summary>Normalises a warning id: <c>169</c>, <c>0169</c> and <c>cs0169</c> are all <c>CS0169</c>.</summary>
    public static string WarningId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var t = id.Trim();
        if (t.StartsWith("CS", StringComparison.OrdinalIgnoreCase))
        {
            t = t[2..];
        }

        return int.TryParse(t, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? $"CS{n:D4}"
            : id.Trim();
    }
}

