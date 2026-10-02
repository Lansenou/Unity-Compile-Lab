// Original synthetic analyzer, Apache-2.0.
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
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
        private static readonly DiagnosticDescriptor Timeout = new DiagnosticDescriptor(
            "USLOW002", "Dependent did not start", "Dependent compilation did not start during root analysis", "Fixture",
            DiagnosticSeverity.Error, isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor LateFailure = new DiagnosticDescriptor(
            "USLOW003", "Synthetic late failure", "Synthetic root analyzer failure", "Fixture",
            DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, Timeout, LateFailure);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(c =>
            {
                var options = c.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
                if (c.Compilation.AssemblyName == "Timing.Root" && options.TryGetValue("ucl_fixture.signal_dir", out var signal))
                {
                    var clock = Stopwatch.StartNew();
                    while (!File.Exists(Path.Combine(signal, "leaf.started")) && clock.ElapsedMilliseconds < 3000) Thread.Sleep(10);
                    if (!File.Exists(Path.Combine(signal, "leaf.started")))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Timeout, Location.None));
                        return;
                    }
                }

                var delay = 200;
                if (options.TryGetValue("ucl_fixture.delay_ms", out var value)
                    && int.TryParse(value, out var configured)) delay = configured;
                Thread.Sleep(delay);
                c.ReportDiagnostic(Diagnostic.Create(Rule, Location.None));
                if (c.Compilation.AssemblyName == "Timing.Root" && options.TryGetValue("ucl_fixture.fail_root", out var fail) && fail == "true")
                    c.ReportDiagnostic(Diagnostic.Create(LateFailure, Location.None));
            });
        }
    }

    // Test-only signal: the generator must run before the leaf's reference image can be emitted. Without a configured
    // temporary signal directory it has no side effects and emits no source. No project paths are embedded in the fixture.
    [Generator]
    public sealed class ProgressGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context) { }
        public void Execute(GeneratorExecutionContext context)
        {
            if (context.Compilation.AssemblyName == "Timing.Leaf"
                && context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("ucl_fixture.signal_dir", out var signal))
                File.WriteAllText(Path.Combine(signal, "leaf.started"), "started");
        }
    }
}
