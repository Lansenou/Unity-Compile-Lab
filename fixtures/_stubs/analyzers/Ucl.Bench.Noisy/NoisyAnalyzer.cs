// Benchmark analyzer for ucl (scripts/gen-bench.sh). Original code, Apache-2.0.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ucl.Bench.Noisy
{
    /// <summary>
    /// Stands in for a large third-party analyzer set: a warning on every method, an info on every invocation and a hidden
    /// diagnostic on every local, so a project gets tens of thousands of analyzer diagnostics.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NoisyAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Method = new DiagnosticDescriptor(
            "UBN001", "Method", "Method '{0}'", "Bench", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor Call = new DiagnosticDescriptor(
            "UBN002", "Invocation", "Call to '{0}'", "Bench", DiagnosticSeverity.Info, isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor Local = new DiagnosticDescriptor(
            "UBN003", "Local", "Local '{0}'", "Bench", DiagnosticSeverity.Hidden, isEnabledByDefault: true);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Method, Call, Local);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(
                c => c.ReportDiagnostic(Diagnostic.Create(Method, c.Symbol.Locations[0], c.Symbol.Name)), SymbolKind.Method);
            context.RegisterOperationAction(
                c => c.ReportDiagnostic(Diagnostic.Create(Call, c.Operation.Syntax.GetLocation(), ((Microsoft.CodeAnalysis.Operations.IInvocationOperation)c.Operation).TargetMethod.Name)),
                OperationKind.Invocation);
            context.RegisterOperationAction(
                c => c.ReportDiagnostic(Diagnostic.Create(Local, c.Operation.Syntax.GetLocation(), "local")),
                OperationKind.LocalReference);
        }
    }
}
