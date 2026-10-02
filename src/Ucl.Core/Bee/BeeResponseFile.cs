namespace Ucl.Core.Bee;

/// <summary>
/// One compiler response file the Editor's build backend wrote, <c>Library/Bee/artifacts/&lt;dag&gt;/&lt;Name&gt;.rsp</c>
/// (docs/oracle.md, "Bee oracle"): one argument per line, paths quoted. Raw values; paths as written.
/// </summary>
public sealed record BeeResponseFile
{
    /// <summary>Source files (<c>"Assets/Foo/Bar.cs"</c> lines).</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary><c>-r:</c>/<c>-reference:</c> paths.</summary>
    public IReadOnlyList<string> References { get; init; } = [];

    /// <summary><c>-define:</c>/<c>-d:</c> symbols.</summary>
    public IReadOnlyList<string> Defines { get; init; } = [];

    /// <summary><c>-nowarn:</c> ids as written (<c>0169</c>, <c>CS0169</c>).</summary>
    public IReadOnlyList<string> NoWarn { get; init; } = [];

    /// <summary><c>-analyzer:</c>/<c>-a:</c> paths.</summary>
    public IReadOnlyList<string> Analyzers { get; init; } = [];

    /// <summary><c>-additionalfile:</c> paths.</summary>
    public IReadOnlyList<string> AdditionalFiles { get; init; } = [];

    /// <summary><c>-langversion:</c>, or null.</summary>
    public string? LangVersion { get; init; }

    /// <summary><c>-out:</c>, or null.</summary>
    public string? Out { get; init; }

    /// <summary><c>-unsafe</c>/<c>-unsafe+</c>.</summary>
    public bool Unsafe { get; init; }

    /// <summary>Parses a response file. Options take <c>-</c> or <c>/</c>; unknown options are ignored.</summary>
    public static BeeResponseFile Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sources = new List<string>();
        var references = new List<string>();
        var defines = new List<string>();
        var noWarn = new List<string>();
        var analyzers = new List<string>();
        var additional = new List<string>();
        string? langVersion = null, output = null;
        var unsafeCode = false;
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line[0] == '"' || !IsOption(line))
            {
                sources.Add(Unquote(line));
                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            var name = (colon < 0 ? line[1..] : line[1..colon]).ToLowerInvariant();
            var value = colon < 0 ? string.Empty : Unquote(line[(colon + 1)..]);
            switch (name)
            {
                case "r" or "reference":
                    references.Add(value);
                    break;
                case "d" or "define":
                    defines.AddRange(Split(value));
                    break;
                case "nowarn":
                    noWarn.AddRange(Split(value));
                    break;
                case "a" or "analyzer":
                    analyzers.Add(value);
                    break;
                case "additionalfile":
                    additional.Add(value);
                    break;
                case "langversion":
                    langVersion = value;
                    break;
                case "out":
                    output = value;
                    break;
                case "unsafe" or "unsafe+":
                    unsafeCode = true;
                    break;
                case "unsafe-":
                    unsafeCode = false;
                    break;
            }
        }

        return new BeeResponseFile
        {
            Sources = sources,
            References = references,
            Defines = defines,
            NoWarn = noWarn,
            Analyzers = analyzers,
            AdditionalFiles = additional,
            LangVersion = langVersion,
            Out = output,
            Unsafe = unsafeCode,
        };
    }

    /// <summary>The assembly name: the <c>-out:</c> file name without <c>.dll</c>, else <paramref name="fileName"/> without <c>.rsp</c> (and <c>.dll</c>).</summary>
    public string AssemblyName(string fileName)
    {
        var source = Out is { Length: > 0 } o ? o.Replace('\\', '/')[(o.Replace('\\', '/').LastIndexOf('/') + 1)..] : fileName;
        foreach (var suffix in new[] { ".rsp", ".dll" })
        {
            if (source.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                source = source[..^suffix.Length];
            }
        }

        return source;
    }

    // An option starts with '-', or with '/' followed by a known option word (a Unix path also starts with '/').
    private static bool IsOption(string line)
    {
        if (line[0] == '-')
        {
            return true;
        }

        if (line[0] != '/')
        {
            return false;
        }

        var end = line.IndexOfAny([':', '+', '-'], 1);
        var word = (end < 0 ? line[1..] : line[1..end]).ToLowerInvariant();
        return word is "r" or "reference" or "d" or "define" or "nowarn" or "a" or "analyzer" or "additionalfile" or "langversion" or "out"
            or "refout" or "unsafe" or "target" or "deterministic" or "optimize" or "debug" or "nologo" or "runtimemetadataversion"
            or "utf8output" or "preferreduilang" or "warnaserror" or "nullable" or "nostdlib" or "noconfig" or "warn" or "checked"
            or "pathmap" or "embed" or "generatedfilesout" or "analyzerconfig" or "ruleset" or "features";
    }

    private static IEnumerable<string> Split(string value) =>
        value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
}
