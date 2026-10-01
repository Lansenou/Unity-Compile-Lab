using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Ucl.Core.Graph;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>Builds Roslyn compilation options from a plan: Unity 6's defaults (docs/defines.md, C01-C08) plus response-file options.</summary>
internal static class CompilerOptionsFactory
{
    public static CSharpCompilationOptions Create(AssemblyPlan plan, bool globalWarnAsError, IFileSystem fs, ProjectContext project)
    {
        var specific = new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal);
        var general = ReportDiagnostic.Default;
        if (plan.RuleSet is { } ruleSet && fs.FileExists(project.ToPhysical(ruleSet)))
        {
            general = RuleSet.GetDiagnosticOptionsFromRulesetFile(project.ToPhysical(ruleSet), out var fromRuleSet);
            foreach (var (id, value) in fromRuleSet)
            {
                specific[id] = value;
            }
        }

        if (plan.WarnAsErrorAll || globalWarnAsError)
        {
            general = ReportDiagnostic.Error;
        }

        foreach (var id in plan.WarnAsErrorIds)
        {
            specific[id] = ReportDiagnostic.Error;
        }

        foreach (var id in plan.WarnNotAsErrorIds)
        {
            specific[id] = ReportDiagnostic.Warn;
        }

        // -nowarn wins over everything, as in csc.
        foreach (var id in plan.NoWarn)
        {
            specific[id] = ReportDiagnostic.Suppress;
        }

        var nullable = plan.Nullable.ToLowerInvariant() switch
        {
            "enable" => NullableContextOptions.Enable,
            "warnings" => NullableContextOptions.Warnings,
            "annotations" => NullableContextOptions.Annotations,
            _ => NullableContextOptions.Disable,
        };

        return new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Debug,
            allowUnsafe: plan.AllowUnsafe,
            warningLevel: 4,
            generalDiagnosticOption: general,
            specificDiagnosticOptions: specific,
            deterministic: true,
            concurrentBuild: true,
            nullableContextOptions: nullable,
            assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);
    }
}
