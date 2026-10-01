using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Ucl.Core.Graph;
using Ucl.Discovery;
using DiagnosticOrigin = Ucl.Core.Model.DiagnosticOrigin;

namespace Ucl.Compilation;

/// <summary>Runs the source generators and analyzers of an assembly, the way Unity runs DLLs labelled <c>RoslynAnalyzer</c>.</summary>
internal static class AnalyzerHost
{
    /// <summary>Runs generators (returning the compilation with generated sources) and collects analyzer diagnostics into <paramref name="sink"/>.</summary>
    public static Microsoft.CodeAnalysis.Compilation Run(
        Microsoft.CodeAnalysis.Compilation compilation,
        IReadOnlyList<string> analyzerPaths,
        AssemblyPlan plan,
        ProjectContext project,
        IFileSystem fs,
        IAnalyzerAssemblyLoader loader,
        CSharpParseOptions parseOptions,
        AnalyzerConfigSet? configSet,
        List<(Microsoft.CodeAnalysis.Diagnostic, DiagnosticOrigin)> sink)
    {
        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        var generators = ImmutableArray.CreateBuilder<ISourceGenerator>();
        foreach (var path in analyzerPaths)
        {
            var reference = new AnalyzerFileReference(path, loader);
            reference.AnalyzerLoadFailed += (_, e) => sink.Add((Microsoft.CodeAnalysis.Diagnostic.Create(LoadFailed, Location.None, path, e.Message), DiagnosticOrigin.Analyzer));
            analyzers.AddRange(reference.GetAnalyzers(LanguageNames.CSharp));
            generators.AddRange(reference.GetGenerators(LanguageNames.CSharp));
        }

        var additional = plan.AdditionalFiles
            .Select(f => project.ToPhysical(f))
            .Where(fs.FileExists)
            .Select(p => (AdditionalText)new FileText(p, fs.ReadAllText(p)))
            .ToImmutableArray();
        var optionsProvider = new ConfigOptionsProvider(configSet, compilation.SyntaxTrees, additional);

        if (generators.Count > 0)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, additional, parseOptions, optionsProvider);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var generatorDiagnostics);
            compilation = updated;
            sink.AddRange(generatorDiagnostics.Select(d => (d, DiagnosticOrigin.Analyzer)));
        }

        if (analyzers.Count > 0)
        {
            var withAnalyzers = compilation.WithAnalyzers(analyzers.ToImmutable(), new AnalyzerOptions(additional, optionsProvider));
            var found = withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
            sink.AddRange(found.Select(d => (d, DiagnosticOrigin.Analyzer)));
        }

        return compilation;
    }

    private static readonly DiagnosticDescriptor LoadFailed = new(
        "UCL1030", "Analyzer could not be loaded", "Analyzer '{0}' could not be loaded: {1}", "ucl", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private sealed class FileText(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
