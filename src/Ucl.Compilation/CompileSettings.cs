namespace Ucl.Compilation;

/// <summary>Run-wide compile switches from the command line.</summary>
public sealed record CompileSettings
{
    /// <summary>Run Roslyn analyzers and source generators (<c>--analyzers on</c>, the default).</summary>
    public bool Analyzers { get; init; } = true;

    /// <summary>Treat every warning as an error (<c>--warnaserror</c>), on top of response files.</summary>
    public bool WarnAsError { get; init; }

    /// <summary>Maximum assemblies compiled at once.</summary>
    public int MaxParallelism { get; init; } = Environment.ProcessorCount;

    /// <summary>Incremental cache folder, or null to disable the cache.</summary>
    public string? CacheDirectory { get; init; }

    /// <summary>
    /// Emit full images (IL included) instead of metadata-only ones, so the assemblies can be loaded and run
    /// (<c>ucl test</c>). Cached separately from metadata-only results.
    /// </summary>
    public bool FullImages { get; init; }

    /// <summary>The tool version, part of every inputs hash so an upgrade invalidates the cache.</summary>
    public string ToolVersion { get; init; } = "0.0.0";
}
