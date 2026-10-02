using Ucl.Core.Model;

namespace Ucl.Cli;

/// <summary>Parsed command line.</summary>
public sealed record CliOptions
{
    /// <summary>Command name.</summary>
    public string Command { get; init; } = "check";

    /// <summary>Positional arguments after the command.</summary>
    public IReadOnlyList<string> Positionals { get; init; } = [];

    /// <summary>Targets (default editor).</summary>
    public IReadOnlyList<TargetKind> Targets { get; init; } = [];

    /// <summary>Platforms (default StandaloneWindows64).</summary>
    public IReadOnlyList<BuildPlatform> Platforms { get; init; } = [];

    /// <summary>Unity versions (default: the project's).</summary>
    public IReadOnlyList<UnityVersion> UnityVersions { get; init; } = [];

    /// <summary><c>--editor</c>.</summary>
    public string? EditorPath { get; init; }

    /// <summary><c>--editor-os</c>, or null for the host.</summary>
    public HostOs? EditorOs { get; init; }

    /// <summary><c>--backend</c>.</summary>
    public ScriptingBackend? Backend { get; init; }

    /// <summary><c>--development</c>.</summary>
    public bool Development { get; init; }

    /// <summary><c>--include-tests</c>.</summary>
    public bool IncludeTests { get; init; }

    /// <summary><c>--analyzers on|off</c>.</summary>
    public bool Analyzers { get; init; } = true;

    /// <summary><c>--warnaserror</c>.</summary>
    public bool WarnAsError { get; init; }

    /// <summary><c>--format</c>.</summary>
    public string Format { get; init; } = "text";

    /// <summary><c>--output</c>.</summary>
    public string? Output { get; init; }

    /// <summary><c>--cache-dir</c>.</summary>
    public string? CacheDir { get; init; }

    /// <summary><c>--no-cache</c>.</summary>
    public bool NoCache { get; init; }

    /// <summary><c>--changed</c> git ref.</summary>
    public string? Changed { get; init; }

    /// <summary><c>--filter</c> (test): a regular expression over test full names.</summary>
    public string? Filter { get; init; }

    /// <summary><c>--emit-unity-filter</c> (test): file to write the Unity <c>-testFilter</c> exclusion list to.</summary>
    public string? EmitUnityFilter { get; init; }

    /// <summary><c>--summary</c>: text output keeps only each cell's root failures and result line.</summary>
    public bool Summary { get; init; }

    /// <summary><c>--timings</c>.</summary>
    public bool Timings { get; init; }

    /// <summary><c>--jobs</c>.</summary>
    public int? Jobs { get; init; }

    /// <summary><c>--project</c> (for commands whose positional is not the project).</summary>
    public string? Project { get; init; }

    /// <summary><c>--out</c> folder (export-csproj).</summary>
    public string? OutDir { get; init; }
}
