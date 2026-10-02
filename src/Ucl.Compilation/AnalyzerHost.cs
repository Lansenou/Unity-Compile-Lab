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
    /// <summary>
    /// Runs generators, then the compiler and the analyzers in one concurrent pass, so method bodies are bound once for both.
    /// Returns the compilation with generated sources; compiler and analyzer diagnostics go to <paramref name="sink"/>.
    /// </summary>
    public static Microsoft.CodeAnalysis.Compilation Run(
        Microsoft.CodeAnalysis.Compilation compilation,
        IReadOnlyList<string> analyzerPaths,
        AssemblyPlan plan,
        ProjectContext project,
        IFileSystem fs,
        AnalyzerSet loader,
        CSharpParseOptions parseOptions,
        AnalyzerConfigSet? configSet,
        List<(Microsoft.CodeAnalysis.Diagnostic, DiagnosticOrigin)> sink)
    {
        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        var generators = ImmutableArray.CreateBuilder<ISourceGenerator>();
        foreach (var path in analyzerPaths)
        {
            var loaded = loader.Get(path);
            sink.AddRange(loaded.LoadFailures.Select(m => (Microsoft.CodeAnalysis.Diagnostic.Create(LoadFailed, Location.None, path, m), DiagnosticOrigin.Analyzer)));
            // In an assembly whose warnings are suppressed only analyzers that can report an error matter; skipping the rest is
            // where most of the analyzer time of a project with many package assemblies goes.
            analyzers.AddRange(plan.SuppressWarnings ? loaded.Analyzers.Where(a => CanReportError(a, compilation.Options)) : loaded.Analyzers);
            generators.AddRange(loaded.Generators);
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

        if (analyzers.Count == 0)
        {
            sink.AddRange(compilation.GetDiagnostics().Select(d => (d, DiagnosticOrigin.Compiler)));
            return compilation;
        }

        var withAnalyzers = compilation.WithAnalyzers(
            analyzers.ToImmutable(),
            new CompilationWithAnalyzersOptions(new AnalyzerOptions(additional, optionsProvider), onAnalyzerException: null, concurrentAnalysis: true,
                logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: false));
        var ids = analyzers.SelectMany(a => a.SupportedDiagnostics).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var all = withAnalyzers.GetAllDiagnosticsAsync().GetAwaiter().GetResult();
        sink.AddRange(all.Select(d => (d, ids.Contains(d.Id) && !d.Id.StartsWith("CS", StringComparison.Ordinal) ? DiagnosticOrigin.Analyzer : DiagnosticOrigin.Compiler)));
        return compilation;
    }

    private static bool CanReportError(DiagnosticAnalyzer analyzer, CompilationOptions options) =>
        analyzer.SupportedDiagnostics.Any(d => d.DefaultSeverity == DiagnosticSeverity.Error
            || options.SpecificDiagnosticOptions.GetValueOrDefault(d.Id) == ReportDiagnostic.Error);

    private static readonly DiagnosticDescriptor LoadFailed = new(
        "UCL1030", "Analyzer could not be loaded", "Analyzer '{0}' could not be loaded: {1}", "ucl", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private sealed class FileText(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
