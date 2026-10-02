using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Ucl.Core.Graph;
using Ucl.Core.Results;
using Ucl.Discovery;
using DiagnosticOrigin = Ucl.Core.Model.DiagnosticOrigin;

namespace Ucl.Compilation;

/// <summary>Runs the source generators and analyzers of an assembly, the way Unity runs DLLs labelled <c>RoslynAnalyzer</c>.</summary>
internal static class AnalyzerHost
{
    /// <summary>
    /// Runs generators and compiler diagnostics before emission; prepares a separately scheduled tracked analyzer pass.
    /// Generator/load diagnostics go to <paramref name="sink"/>. Compiler and analyzer diagnostics are finalized together.
    /// </summary>
    public static PreparedAnalysis Prepare(
        Microsoft.CodeAnalysis.Compilation compilation,
        IReadOnlyList<(string Display, string Path)> analyzerPaths,
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
        var displays = new Dictionary<DiagnosticAnalyzer, string>();
        foreach (var (display, path) in analyzerPaths)
        {
            var loaded = loader.Get(path);
            sink.AddRange(loaded.LoadFailures.Select(m => (Microsoft.CodeAnalysis.Diagnostic.Create(LoadFailed, Location.None, path, m), DiagnosticOrigin.Analyzer)));
            // In an assembly whose warnings are suppressed only analyzers that can report an error matter; skipping the rest is
            // where most of the analyzer time of a project with many package assemblies goes.
            analyzers.AddRange(plan.SuppressWarnings ? loaded.Analyzers.Where(a => CanReportError(a, compilation.Options)) : loaded.Analyzers);
            foreach (var analyzer in loaded.Analyzers)
            {
                displays[analyzer] = display;
            }
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

        // csc without -errorlog (Unity's Bee passes none) filters Hidden and Info diagnostics, and its driver does not run an
        // analyzer whose every diagnostic would be filtered (Roslyn CommonCompiler, AnalyzerManager.IsDiagnosticAnalyzerSuppressed).
        var categoryConfigured = CategorySeverityConfigured(optionsProvider, compilation.SyntaxTrees);
        var running = analyzers.Where(a => categoryConfigured || !OnlyFiltered(a, compilation)).ToImmutableArray();
        var compilerDiagnostics = compilation.GetDiagnostics();
        if (running.Length == 0)
        {
            return new PreparedAnalysis(compilation, compilerDiagnostics, false,
                () => Task.FromResult(new AnalysisBatch(compilerDiagnostics.Select(d => (d, DiagnosticOrigin.Compiler)).ToList(), [])));
        }

        // GetAllDiagnosticsAsync uses Roslyn's untracked driver and discards its execution times. Use the tracked API once.
        // Route compiler diagnostics through that same driver so DiagnosticSuppressors still see and suppress compiler warnings.
        var withAnalyzers = compilation.WithAnalyzers(
            running.Add(new CompilerDiagnosticsAnalyzer(compilerDiagnostics)),
            new CompilationWithAnalyzersOptions(new AnalyzerOptions(additional, optionsProvider), onAnalyzerException: null, concurrentAnalysis: true,
                logAnalyzerExecutionTime: true, reportSuppressedDiagnostics: false));
        var ids = running.SelectMany(a => a.SupportedDiagnostics).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        return new PreparedAnalysis(compilation, compilerDiagnostics, true, async () =>
        {
            var analysis = await withAnalyzers.GetAnalysisResultAsync(CancellationToken.None).ConfigureAwait(false);
            var timings = running.Select(analyzer => new AnalyzerTiming(displays[analyzer], analyzer.GetType().FullName ?? analyzer.GetType().Name,
                analysis.AnalyzerTelemetryInfo[analyzer].ExecutionTime.TotalMilliseconds)).ToList();
            var diagnostics = analysis.GetAllDiagnostics()
                .Select(d => (d, ids.Contains(d.Id) && !d.Id.StartsWith("CS", StringComparison.Ordinal) ? DiagnosticOrigin.Analyzer : DiagnosticOrigin.Compiler)).ToList();
            return new AnalysisBatch(diagnostics, timings);
        });
    }

    internal sealed record PreparedAnalysis(Microsoft.CodeAnalysis.Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> CompilerDiagnostics, bool HasAnalyzers, Func<Task<AnalysisBatch>> Complete);

    internal sealed record AnalysisBatch(IReadOnlyList<(Microsoft.CodeAnalysis.Diagnostic Diagnostic, DiagnosticOrigin Origin)> Diagnostics,
        IReadOnlyList<AnalyzerTiming> Timings);

    // True when every diagnostic of the analyzer is Hidden, Info or suppressed wherever it can be reported: by default, under
    // the compilation's specific options (rsp, ruleset) and under every global or per-file .editorconfig value. Suppressors
    // always run (they act on other analyzers' diagnostics).
    private static bool OnlyFiltered(DiagnosticAnalyzer analyzer, Microsoft.CodeAnalysis.Compilation compilation)
    {
        if (analyzer is DiagnosticSuppressor)
        {
            return false;
        }

        var options = compilation.Options;
        var trees = options.SyntaxTreeOptionsProvider;
        foreach (var descriptor in analyzer.SupportedDiagnostics)
        {
            var values = new List<ReportDiagnostic>
            {
                options.SpecificDiagnosticOptions.TryGetValue(descriptor.Id, out var specific) ? specific
                    : descriptor.IsEnabledByDefault ? ReportDiagnostic.Default : ReportDiagnostic.Suppress,
            };
            if (trees is not null)
            {
                if (trees.TryGetGlobalDiagnosticValue(descriptor.Id, CancellationToken.None, out var global))
                {
                    values.Add(global);
                }

                foreach (var tree in compilation.SyntaxTrees)
                {
                    if (trees.TryGetDiagnosticValue(tree, descriptor.Id, CancellationToken.None, out var local))
                    {
                        values.Add(local);
                    }
                }
            }

            if (values.Any(v => Effective(v, descriptor) is ReportDiagnostic.Warn or ReportDiagnostic.Error))
            {
                return false;
            }
        }

        return true;
    }

    private static ReportDiagnostic Effective(ReportDiagnostic value, DiagnosticDescriptor descriptor) =>
        value != ReportDiagnostic.Default ? value : descriptor.DefaultSeverity switch
        {
            DiagnosticSeverity.Error => ReportDiagnostic.Error,
            DiagnosticSeverity.Warning => ReportDiagnostic.Warn,
            DiagnosticSeverity.Info => ReportDiagnostic.Info,
            _ => ReportDiagnostic.Hidden,
        };

    // Category-wide or all-analyzer severities (dotnet_analyzer_diagnostic.*) are resolved by the analyzer driver; with
    // any of them every analyzer runs, and only its output is filtered.
    private static bool CategorySeverityConfigured(AnalyzerConfigOptionsProvider provider, IEnumerable<SyntaxTree> trees) =>
        provider.GlobalOptions.Keys.Concat(trees.SelectMany(t => provider.GetOptions(t).Keys))
            .Any(k => k.StartsWith("dotnet_analyzer_diagnostic.", StringComparison.OrdinalIgnoreCase));

    private static bool CanReportError(DiagnosticAnalyzer analyzer, CompilationOptions options) =>
        analyzer.SupportedDiagnostics.Any(d => d.DefaultSeverity == DiagnosticSeverity.Error
            || options.SpecificDiagnosticOptions.GetValueOrDefault(d.Id) == ReportDiagnostic.Error);

    private static readonly DiagnosticDescriptor LoadFailed = new(
        "UCL1030", "Analyzer could not be loaded", "Analyzer '{0}' could not be loaded: {1}", "ucl", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // The adapter is an implementation detail, excluded from reported analyzer timings. Compiler diagnostics retain their
    // original locations, severities and warning-as-error flags, and are classified as compiler diagnostics by their CS ids.
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1001", Justification = "Private in-process adapter instantiated directly, not a loadable compiler plugin.")]
    private sealed class CompilerDiagnosticsAnalyzer(ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics) : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            diagnostics.Select(d => d.Descriptor).DistinctBy(d => d.Id).ToImmutableArray();

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(c =>
            {
                foreach (var diagnostic in diagnostics)
                {
                    c.ReportDiagnostic(diagnostic);
                }
            });
        }
    }

    private sealed class FileText(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
