// Original synthetic analyzer, Apache-2.0.
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ucl.Fixture.Slow
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SlowAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            "USLOW001", "Synthetic slow analysis", "Synthetic slow analysis completed", "Fixture",
            DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(c =>
            {
                var delay = 200;
                if (c.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("ucl_fixture.delay_ms", out var value)
                    && int.TryParse(value, out var configured)) delay = configured;
                Thread.Sleep(delay);
                c.ReportDiagnostic(Diagnostic.Create(Rule, Location.None));
            });
        }
    }
}
