using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Ucl.Core.Graph;
using Ucl.Core.Rules;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>
/// The DLL references of one editor install and project, created once per run: Roslyn caches metadata per
/// reference object, so sharing them across assemblies is what keeps a 30-assembly compile fast.
/// </summary>
internal sealed class ReferenceCatalog
{
    private readonly IFileSystem _fs;
    private readonly EditorInstall _editor;
    private readonly ConcurrentDictionary<string, PortableExecutableReference> _byPath = new(StringComparer.Ordinal);
    private readonly IReadOnlyCollection<string> _modulePackages;

    public ReferenceCatalog(IFileSystem fs, EditorInstall editor)
    {
        _fs = fs;
        _editor = editor;

        // The editor's own list of built-in packages is authoritative; the static list covers stub editors.
        var builtIn = Path.Combine(editor.DataPath, "Resources", "PackageManager", "BuiltInPackages");
        var listed = fs.ListDirectories(builtIn)
            .Where(d => d.StartsWith("com.unity.modules.", StringComparison.Ordinal))
            .Select(d => d["com.unity.modules.".Length..])
            .ToHashSet(StringComparer.Ordinal);
        _modulePackages = listed.Count > 0 ? listed : BuiltInModules.KnownPackages.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Editor and profile DLLs for an assembly: (display string, absolute path), sorted by display.</summary>
    public IReadOnlyList<(string Display, string Path)> EditorReferences(AssemblyGraph graph, AssemblyPlan plan)
    {
        var engine = plan.Engine;
        var result = new List<(string, string)>();
        var profile = plan.NetFramework
            ? _editor.NetFrameworkReferences
            : plan.IsEditorOnly ? [.. _editor.NetStandardReferences, .. _editor.NetStandardEditorExtensions] : _editor.NetStandardReferences;
        foreach (var p in profile)
        {
            result.Add(($"profile:{Relative(p)}", p));
        }

        if (engine != EngineReferences.None)
        {
            var modules = EngineModules(graph.Cell.IsEditor, _editor.PlatformModules.GetValueOrDefault(graph.Cell.Platform) ?? [])
                .Where(p => BuiltInModules.IsReferenced(Path.GetFileName(p), graph.EnabledModules, _modulePackages));
            foreach (var p in modules)
            {
                result.Add(($"editor:{Relative(p)}", p));
            }
        }

        if (engine == EngineReferences.RuntimeAndEditor)
        {
            foreach (var p in _editor.EditorAssemblies)
            {
                result.Add(($"editor:{Relative(p)}", p));
            }
        }

        // Editor-cell references every assembly gets, noEngineReferences or not (UnityCsReference EditorAssemblyReferences).
        if (graph.Cell.IsEditor)
        {
            foreach (var p in _editor.EditorExtensions)
            {
                result.Add(($"editor:{Relative(p)}", p));
            }
        }

        if (_editor.CompilationPipeline is { } pipeline && CodeGenAssemblies.UsesCompilationPipeline(plan.Name))
        {
            result.Add(($"editor:{Relative(pipeline)}", pipeline));
        }

        return result.OrderBy(r => r.Item1, StringComparer.Ordinal).ToList();
    }

    // One DLL per file name (docs/architecture.md, "Editor references"). Editor cells use Managed/UnityEngine/ whatever the
    // active platform is, plus the platform modules it lacks (UnityEngine.WebGLModule); player cells use the platform's copy.
    private IEnumerable<string> EngineModules(bool editorCell, IReadOnlyList<string> platform)
    {
        IEnumerable<string> editor = [.. _editor.EngineModules, .. _editor.EngineFacade is { } facade ? [facade] : Array.Empty<string>()];
        var first = editorCell ? editor : platform;
        var second = editorCell ? platform : editor;
        var names = first.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return first.Concat(second.Where(p => !names.Contains(Path.GetFileName(p))));
    }

    /// <summary>The editor's own source generators, which run on every assembly: (display string, absolute path).</summary>
    public IReadOnlyList<(string Display, string Path)> EditorAnalyzers() =>
        [.. _editor.SourceGenerators.Select(p => ($"editor:{Relative(p)}", p))];

    /// <summary>A shared reference to a DLL on disk.</summary>
    public PortableExecutableReference Get(string path) =>
        _byPath.GetOrAdd(path, p => MetadataReference.CreateFromImage(_fs.ReadAllBytes(p), filePath: p));

    // Platform support lives beside Unity.app on macOS, outside the data folder.
    private string Relative(string path) =>
        Path.GetRelativePath(path.StartsWith(_editor.DataPath, StringComparison.Ordinal) || _editor.PlaybackEnginesParent.Length == 0 ? _editor.DataPath : _editor.PlaybackEnginesParent, path)
            .Replace('\\', '/');
}
