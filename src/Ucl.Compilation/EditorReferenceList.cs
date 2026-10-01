using Ucl.Core.Graph;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>
/// The editor and .NET profile DLLs an assembly compiles against, exactly as <c>ucl check</c> resolves them; exposed
/// for tools that describe a compile without running it (<c>ucl export-csproj</c>).
/// </summary>
public sealed class EditorReferenceList
{
    private readonly ReferenceCatalog catalog;

    /// <summary>Creates the list for one editor install.</summary>
    /// <param name="fs">Disk access (reads the editor's built-in package list).</param>
    /// <param name="editor">The editor install.</param>
    public EditorReferenceList(IFileSystem fs, EditorInstall editor)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(editor);
        catalog = new ReferenceCatalog(fs, editor);
    }

    /// <summary>Absolute paths of the profile and editor DLLs for <paramref name="plan"/> in <paramref name="graph"/>, in the compiler's order.</summary>
    public IReadOnlyList<string> For(AssemblyGraph graph, AssemblyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(plan);
        return catalog.EditorReferences(graph, plan).Select(r => r.Path).ToList();
    }
}
