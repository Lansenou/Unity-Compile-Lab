using Ucl.Core.Graph;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>
/// The compiler inputs <c>ucl check</c> uses for one assembly, as a command line would list them; for comparison with
/// the Editor's own command lines (<c>ucl bee-diff</c>). Nothing is compiled.
/// </summary>
public sealed class UclCommandLine
{
    private readonly ReferenceCatalog _catalog;
    private readonly ProjectContext _project;

    /// <summary>Creates the builder for one editor install and project.</summary>
    public UclCommandLine(IFileSystem fs, EditorInstall editor, ProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(project);
        _catalog = new ReferenceCatalog(fs, editor);
        _project = project;
    }

    /// <summary>Absolute paths of every DLL <paramref name="plan"/> references (profile, editor, plugins), in the compiler's order.</summary>
    public IReadOnlyList<string> ReferencePaths(AssemblyGraph graph, AssemblyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(plan);
        return [.. _catalog.EditorReferences(graph, plan).Select(r => r.Path), .. plan.PrecompiledReferences.Select(_project.ToPhysical)];
    }

    /// <summary>Absolute paths of the analyzers that run on <paramref name="plan"/> when analyzers are on.</summary>
    public IReadOnlyList<string> AnalyzerPaths(AssemblyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return [.. plan.Analyzers.Select(_project.ToPhysical), .. _catalog.EditorAnalyzers().Select(a => a.Path)];
    }
}
