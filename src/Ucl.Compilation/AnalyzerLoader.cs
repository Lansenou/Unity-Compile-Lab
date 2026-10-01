using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Ucl.Compilation;

/// <summary>
/// Loads analyzer DLLs, each into its own load context so two analyzers with clashing dependencies coexist;
/// Roslyn itself resolves from the default context so analyzers bind to the host compiler. DLLs are loaded
/// from bytes, never mapped from their path, so ucl does not lock files in the user's project on Windows.
/// </summary>
internal sealed class AnalyzerLoader : IAnalyzerAssemblyLoader
{
    private readonly ConcurrentDictionary<string, Assembly> _loaded = new(StringComparer.Ordinal);

    public void AddDependencyLocation(string fullPath)
    {
    }

    public Assembly LoadFromPath(string fullPath) =>
        _loaded.GetOrAdd(fullPath, p => FromBytes(new IsolatedContext(p), p));

    private static Assembly FromBytes(AssemblyLoadContext context, string path)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        return context.LoadFromStream(stream);
    }

    private sealed class IsolatedContext(string mainPath) : AssemblyLoadContext($"ucl-analyzer:{Path.GetFileName(mainPath)}")
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (AssemblyLoadContext.Default.Assemblies.Any(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), assemblyName)))
            {
                return null;
            }

            var candidate = Path.Combine(Path.GetDirectoryName(mainPath)!, assemblyName.Name + ".dll");
            return File.Exists(candidate) ? FromBytes(this, candidate) : null;
        }
    }
}
