using Ucl.Core.Model;
using Ucl.Core.Rules;

namespace Ucl.Core.Graph;

/// <summary>The assemblies of one cell in dependency order, with the reasons for anything left out.</summary>
public sealed record AssemblyGraph
{
    /// <summary>The cell.</summary>
    public required CompileCell Cell { get; init; }

    /// <summary>Assemblies in a deterministic topological order (dependencies first, ties by name).</summary>
    public IReadOnlyList<AssemblyPlan> Assemblies { get; init; } = [];

    /// <summary>Assemblies defined in the project but not compiled in this cell, with the reason.</summary>
    public IReadOnlyDictionary<string, string> Excluded { get; init; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Script path to owning assembly name (also for scripts whose assembly is excluded).</summary>
    public IReadOnlyDictionary<string, string> ScriptOwners { get; init; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Cell-wide defines (before per-assembly additions).</summary>
    public required DefineSet BaseDefines { get; init; }

    /// <summary>Enabled built-in module names (<c>physics</c> for <c>com.unity.modules.physics</c>), sorted.</summary>
    public IReadOnlyList<string> EnabledModules { get; init; } = [];

    /// <summary>API compatibility of the cell: true for .NET Framework, false for .NET Standard 2.1.</summary>
    public bool NetFramework { get; init; }

    /// <summary><c>ucl</c> diagnostics (UCL1xxx) found while planning.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    /// <summary>Configuration problems (exit 3).</summary>
    public IReadOnlyList<Problem> Problems { get; init; } = [];

    /// <summary>Looks up a planned assembly.</summary>
    public AssemblyPlan? Find(string name) => Assemblies.FirstOrDefault(a => a.Name == name);
}
