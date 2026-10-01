using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ucl.Compilation;

/// <summary>Exposes <c>.editorconfig</c> key/value options to analyzers and generators (the public-API equivalent of csc's provider).</summary>
internal sealed class ConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigSet? _set;

    public ConfigOptionsProvider(AnalyzerConfigSet? set, IEnumerable<SyntaxTree> trees, ImmutableArray<AdditionalText> additional)
    {
        _set = set;
        GlobalOptions = new Options(set?.GlobalConfigOptions.AnalyzerOptions ?? ImmutableDictionary<string, string>.Empty);
    }

    public override AnalyzerConfigOptions GlobalOptions { get; }

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => ForPath(tree.FilePath);

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => ForPath(textFile.Path);

    private Options ForPath(string path) =>
        new(_set?.GetOptionsForSourcePath(path).AnalyzerOptions ?? ImmutableDictionary<string, string>.Empty);

    private sealed class Options(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);

        public override IEnumerable<string> Keys => values.Keys;
    }
}
