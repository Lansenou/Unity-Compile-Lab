namespace Ucl.Core.Results;

/// <summary>What happened to one assembly.</summary>
public enum AssemblyStatus
{
    /// <summary>Compiled without errors.</summary>
    Compiled,

    /// <summary>Compiled with errors.</summary>
    Failed,

    /// <summary>Not compiled because a dependency failed (Unity does the same).</summary>
    Skipped,
}
