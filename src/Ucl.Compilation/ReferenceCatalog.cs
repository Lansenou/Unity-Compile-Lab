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
    public IReadOnlyList<(string Display, string Path)> EditorReferences(AssemblyGraph graph, EngineReferences engine)
    {
        var result = new List<(string, string)>();
        foreach (var p in graph.NetFramework ? _editor.NetFrameworkReferences : _editor.NetStandardReferences)
        {
            result.Add(($"profile:{Relative(p)}", p));
        }

        if (engine != EngineReferences.None)
        {
            foreach (var p in _editor.EngineModules.Where(p => BuiltInModules.IsReferenced(Path.GetFileName(p), graph.EnabledModules, _modulePackages)))
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

        return result.OrderBy(r => r.Item1, StringComparer.Ordinal).ToList();
    }

    /// <summary>A shared reference to a DLL on disk.</summary>
    public PortableExecutableReference Get(string path) =>
        _byPath.GetOrAdd(path, p => MetadataReference.CreateFromImage(_fs.ReadAllBytes(p), filePath: p));

    private string Relative(string path) => Path.GetRelativePath(_editor.DataPath, path).Replace('\\', '/');
}
