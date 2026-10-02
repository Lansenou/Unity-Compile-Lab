namespace Ucl.Core.Bee;

/// <summary>One assembly of a dag: the response file compared and the differences found (empty when ucl agrees).</summary>
/// <param name="Name">Assembly name.</param>
/// <param name="DefinitionPath">asmdef path when ucl knows the assembly, else null.</param>
/// <param name="ResponseFile">The response file (project-relative), or null for an assembly only ucl compiles.</param>
/// <param name="Differences">Sorted differences.</param>
public sealed record BeeAssemblyResult(string Name, string? DefinitionPath, string? ResponseFile, IReadOnlyList<BeeDifference> Differences)
{
    /// <summary>True when ucl's command line agrees with the Editor's.</summary>
    public bool Agrees => Differences.Count == 0;
}
