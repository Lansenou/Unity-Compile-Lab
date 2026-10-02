// Original synthetic suppressor, Apache-2.0.
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ucl.Fixture.CompilerSuppressor
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CompilerSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor Rule = new SuppressionDescriptor("USUP001", "CS0168", "Synthetic suppression");
        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(Rule);
        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            foreach (var diagnostic in context.ReportedDiagnostics)
                if (diagnostic.Id == "CS0168") context.ReportSuppression(Suppression.Create(Rule, diagnostic));
        }
    }
}
