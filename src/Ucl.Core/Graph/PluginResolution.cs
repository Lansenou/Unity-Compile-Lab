namespace Ucl.Core.Graph;

/// <summary>Per-assembly precompiled references and analyzers.</summary>
/// <param name="References">Assembly name to DLL paths.</param>
/// <param name="Analyzers">Assembly name to analyzer DLL paths.</param>
internal sealed record PluginResolution(
    IReadOnlyDictionary<string, List<string>> References,
    IReadOnlyDictionary<string, List<string>> Analyzers);
