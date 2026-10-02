// Fixture analyzers for the ucl conformance corpus. Original code, Apache-2.0.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Example.Style.Analyzers
{
    /// <summary>Reports STY001 (Info by default, a style suggestion) on every class that is not sealed, static or abstract.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SealClassAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            "STY001",
            "Class can be sealed",
            "Class '{0}' can be sealed",
            "Style",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(
                c =>
                {
                    var type = (INamedTypeSymbol)c.Symbol;
                    if (type.TypeKind == TypeKind.Class && !type.IsSealed && !type.IsStatic && !type.IsAbstract && type.Locations.Length > 0)
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], type.Name));
                    }
                },
                SymbolKind.NamedType);
        }
    }

    /// <summary>Reports STY002 (Hidden by default, an IDE fade-out) on every field.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FieldNamingAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            "STY002",
            "Field naming",
            "Field '{0}' could use the project's naming style",
            "Style",
            DiagnosticSeverity.Hidden,
            isEnabledByDefault: true);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(c => c.ReportDiagnostic(Diagnostic.Create(Rule, c.Symbol.Locations[0], c.Symbol.Name)), SymbolKind.Field);
        }
    }
}
