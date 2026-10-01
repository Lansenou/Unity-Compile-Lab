// Fixture analyzer for the ucl conformance corpus. Original code, Apache-2.0.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ucl.Fixture.Analyzer
{
    /// <summary>Reports UFX001 on every named type whose name contains "Bad".</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BadNameAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>The diagnostic id.</summary>
        public const string DiagnosticId = "UFX001";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Type name contains 'Bad'",
            "Type name '{0}' contains 'Bad'",
            "Fixture",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        }

        private static void AnalyzeType(SymbolAnalysisContext context)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (type.Name.Contains("Bad") && type.Locations.Length > 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], type.Name));
            }
        }
    }
}
