using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ucl.Compilation;

/// <summary>
/// Feeds <c>.editorconfig</c>/<c>.globalconfig</c> severities to the compiler per syntax tree, as csc does with
/// <c>-analyzerconfig</c>. Roslyn's own provider is internal, so this is the public-API equivalent.
/// </summary>
internal sealed class TreeOptionsProvider : SyntaxTreeOptionsProvider
{
    private readonly ImmutableDictionary<SyntaxTree, AnalyzerConfigOptionsResult> _perTree;
    private readonly AnalyzerConfigOptionsResult _global;

    public TreeOptionsProvider(AnalyzerConfigSet set, IEnumerable<SyntaxTree> trees)
    {
        _global = set.GlobalConfigOptions;
        _perTree = trees.ToImmutableDictionary(t => t, t => set.GetOptionsForSourcePath(t.FilePath));
    }

    public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) => GeneratedKind.Unknown;

    public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
    {
        if (_perTree.TryGetValue(tree, out var result) && result.TreeOptions.TryGetValue(diagnosticId, out severity))
        {
            return true;
        }

        severity = ReportDiagnostic.Default;
        return false;
    }

    public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity) =>
        _global.TreeOptions.TryGetValue(diagnosticId, out severity);
}
