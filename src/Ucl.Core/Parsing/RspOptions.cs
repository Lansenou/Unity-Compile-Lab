namespace Ucl.Core.Parsing;

/// <summary>Compiler options from a response file or <c>additionalCompilerArguments</c>. Lists keep file order.</summary>
public sealed record RspOptions
{
    /// <summary>No options.</summary>
    public static RspOptions Empty { get; } = new();

    /// <summary><c>-define:</c> symbols.</summary>
    public IReadOnlyList<string> Defines { get; init; } = [];

    /// <summary><c>-nowarn:</c> ids, normalised to <c>CS0000</c> form when numeric.</summary>
    public IReadOnlyList<string> NoWarn { get; init; } = [];

    /// <summary><c>-warnaserror</c>: true (all), false (<c>-warnaserror-</c>), or null when absent.</summary>
    public bool? WarnAsErrorAll { get; init; }

    /// <summary><c>-warnaserror:ids</c>.</summary>
    public IReadOnlyList<string> WarnAsErrorIds { get; init; } = [];

    /// <summary><c>-warnaserror-:ids</c>.</summary>
    public IReadOnlyList<string> WarnNotAsErrorIds { get; init; } = [];

    /// <summary><c>-langversion:</c>, or null.</summary>
    public string? LangVersion { get; init; }

    /// <summary><c>-nullable:</c>, or null.</summary>
    public string? Nullable { get; init; }

    /// <summary><c>-unsafe</c> (true), <c>-unsafe-</c> (false), or null.</summary>
    public bool? Unsafe { get; init; }

    /// <summary><c>-additionalfile:</c> project-relative paths.</summary>
    public IReadOnlyList<string> AdditionalFiles { get; init; } = [];

    /// <summary><c>-analyzerconfig:</c> project-relative paths.</summary>
    public IReadOnlyList<string> AnalyzerConfigs { get; init; } = [];

    /// <summary><c>-ruleset:</c> project-relative path, or null.</summary>
    public string? RuleSet { get; init; }

    /// <summary><c>-r:</c>/<c>-reference:</c> project-relative DLL paths.</summary>
    public IReadOnlyList<string> References { get; init; } = [];

    /// <summary>Options <c>ucl</c> does not support; reported as UCL1020.</summary>
    public IReadOnlyList<string> Unsupported { get; init; } = [];

    /// <summary>Applies <paramref name="later"/> on top of this: lists accumulate, single values are replaced when set.</summary>
    public RspOptions Then(RspOptions later) => new()
    {
        Defines = [.. Defines, .. later.Defines],
        NoWarn = [.. NoWarn, .. later.NoWarn],
        WarnAsErrorAll = later.WarnAsErrorAll ?? WarnAsErrorAll,
        WarnAsErrorIds = [.. WarnAsErrorIds, .. later.WarnAsErrorIds],
        WarnNotAsErrorIds = [.. WarnNotAsErrorIds, .. later.WarnNotAsErrorIds],
        LangVersion = later.LangVersion ?? LangVersion,
        Nullable = later.Nullable ?? Nullable,
        Unsafe = later.Unsafe ?? Unsafe,
        AdditionalFiles = [.. AdditionalFiles, .. later.AdditionalFiles],
        AnalyzerConfigs = [.. AnalyzerConfigs, .. later.AnalyzerConfigs],
        RuleSet = later.RuleSet ?? RuleSet,
        References = [.. References, .. later.References],
        Unsupported = [.. Unsupported, .. later.Unsupported],
    };
}
