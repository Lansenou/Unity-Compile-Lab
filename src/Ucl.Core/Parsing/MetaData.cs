namespace Ucl.Core.Parsing;

/// <summary>What <c>ucl</c> reads from a <c>.meta</c> file.</summary>
/// <param name="Guid">Asset GUID (32 lowercase hex), or null when absent.</param>
/// <param name="Labels">Asset labels, such as <c>RoslynAnalyzer</c>.</param>
/// <param name="Plugin">Plugin import settings, or null when the importer is not a <c>PluginImporter</c>.</param>
public sealed record MetaData(string? Guid, IReadOnlyList<string> Labels, PluginSettings? Plugin)
{
    /// <summary>The label that turns a DLL into a Roslyn analyzer.</summary>
    public const string RoslynAnalyzerLabel = "RoslynAnalyzer";

    /// <summary>True when the asset carries the <c>RoslynAnalyzer</c> label (case-sensitive, as in Unity).</summary>
    public bool IsRoslynAnalyzer => Labels.Contains(RoslynAnalyzerLabel, StringComparer.Ordinal);
}
