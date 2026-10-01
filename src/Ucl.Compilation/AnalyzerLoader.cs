using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Ucl.Compilation;

/// <summary>
/// Loads analyzer DLLs, each into its own load context so two analyzers with clashing dependencies coexist;
/// Roslyn itself resolves from the default context so analyzers bind to the host compiler.
/// </summary>
internal sealed class AnalyzerLoader : IAnalyzerAssemblyLoader
{
    private readonly ConcurrentDictionary<string, Assembly> _loaded = new(StringComparer.Ordinal);

    public void AddDependencyLocation(string fullPath)
    {
    }

    public Assembly LoadFromPath(string fullPath) =>
        _loaded.GetOrAdd(fullPath, p => new IsolatedContext(p).LoadFromAssemblyPath(p));

    private sealed class IsolatedContext(string mainPath) : AssemblyLoadContext($"ucl-analyzer:{Path.GetFileName(mainPath)}")
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (AssemblyLoadContext.Default.Assemblies.Any(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), assemblyName)))
            {
                return null;
            }

            var candidate = Path.Combine(Path.GetDirectoryName(mainPath)!, assemblyName.Name + ".dll");
            return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
        }
    }
}
