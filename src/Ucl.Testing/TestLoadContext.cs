using System.Reflection;
using System.Runtime.Loader;

namespace Ucl.Testing;

/// <summary>
/// An isolated, collectible load context for one test run: project assemblies, plugin and editor DLLs from disk by
/// simple name, everything else (the .NET runtime, its <c>mscorlib</c>/<c>netstandard</c> facades) from the default
/// context. <c>nunit.framework</c> always resolves to the host's own NUnit, whatever version the project compiled
/// against, so the runner and the tests share one NUnit.
/// </summary>
internal sealed class TestLoadContext(IReadOnlyDictionary<string, string> files) : AssemblyLoadContext("ucl-test", isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? string.Empty;
        if (name.Equals("nunit.framework", StringComparison.OrdinalIgnoreCase))
        {
            return typeof(NUnit.Framework.Assert).Assembly;
        }

        return files.TryGetValue(name, out var path) ? LoadFromAssemblyPath(path) : null;
    }
}
