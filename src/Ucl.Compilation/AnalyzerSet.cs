using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ucl.Compilation;

/// <summary>
/// The analyzers and generators of each analyzer DLL, loaded and instantiated once per run and shared by every assembly
/// that runs them (analyzers are stateless by contract), instead of once per assembly.
/// </summary>
internal sealed class AnalyzerSet
{
    private readonly AnalyzerLoader _loader = new();
    private readonly ConcurrentDictionary<string, Lazy<Loaded>> _byPath = new(StringComparer.Ordinal);

    public Loaded Get(string path) => _byPath.GetOrAdd(path, p => new Lazy<Loaded>(() => Load(p))).Value;

    private Loaded Load(string path)
    {
        var failures = new List<string>();
        var reference = new AnalyzerFileReference(path, _loader);
        reference.AnalyzerLoadFailed += (_, e) => failures.Add(e.Message);
        var analyzers = reference.GetAnalyzers(LanguageNames.CSharp);
        var generators = reference.GetGenerators(LanguageNames.CSharp);
        return new Loaded(analyzers, generators, failures);
    }

    /// <summary>One DLL's analyzers, generators and load failure messages.</summary>
    public sealed record Loaded(ImmutableArray<DiagnosticAnalyzer> Analyzers, ImmutableArray<ISourceGenerator> Generators, IReadOnlyList<string> LoadFailures);
}
