using Ucl.Core.Model;
using Ucl.Core.Parsing;
using Ucl.Core.Rules;

namespace Ucl.Core.Graph;

/// <summary>Applies plugin import settings: which DLLs each assembly references, and which analyzers run on it.</summary>
internal static class PluginResolver
{
    /// <summary>The NUnit DLL of the <c>com.unity.ext.nunit</c> package.</summary>
    public const string NUnitFramework = "nunit.framework.dll";

    public static PluginResolution Resolve(
        ProjectInventory inventory,
        DefinitionIndex index,
        CompileCell cell,
        DefineSet baseDefines,
        Dictionary<string, Draft> drafts,
        IReadOnlySet<string> untestable,
        List<Diagnostic> diagnostics)
    {
        var key = cell.IsEditor ? PlatformInfo.EditorPluginKey : PlatformInfo.Of(cell.Platform).PluginKey;
        var references = drafts.Keys.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal);
        var analyzers = drafts.Keys.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal);
        var all = inventory.Plugins
            .OrderBy(p => p.Path, StringComparer.Ordinal)
            .Select(p => (p.Path, Meta: p.MetaText is null ? null : MetaParser.Parse(p.MetaText)))
            .ToList();

        var candidates = new List<(string Path, bool Auto)>();
        foreach (var (path, meta) in all)
        {
            if (meta?.IsRoslynAnalyzer == true)
            {
                AddAnalyzer(path, index, drafts, analyzers);
                continue;
            }

            // A DLL inside the folder of a package test assembly that is not testable is no candidate at all, so it cannot shadow a
            // same-name copy elsewhere ([REAL]: the Editor took the org.nuget copy over the one beside com.unity.collections' tests).
            if (index.OwnerOf(path) is { } owner && untestable.Contains(owner))
            {
                continue;
            }

            var settings = meta?.Plugin ?? PluginSettings.Default;
            if (settings.IsCompatibleWith(key) && DefineConstraints.AreSatisfied(settings.DefineConstraints, baseDefines.Contains))
            {
                candidates.Add((path, !settings.IsExplicitlyReferenced));
            }
        }

        var compatible = OnePerFileName(candidates, inventory.PluginVersions, diagnostics);
        foreach (var draft in drafts.Values.OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            var data = draft.Entry?.Data;
            if (data is { OverrideReferences: true })
            {
                foreach (var file in data.PrecompiledReferences)
                {
                    // Unity looks the name up among the precompiled assemblies it knows and skips it silently when absent
                    // (docs/architecture.md, "Precompiled DLLs"), so this is information, never an error.
                    var exists = all.Any(p => ProjectPaths.FileName(p.Path).Equals(file, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        var native = inventory.NativePlugins.Any(p => ProjectPaths.FileName(p).Equals(file, StringComparison.OrdinalIgnoreCase));
                        diagnostics.Add(new Diagnostic(ProblemIds.MissingPrecompiledReference, Severity.Info, DiagnosticOrigin.Ucl, draft.Name, draft.Entry!.Path, 0, 0,
                            native
                                ? $"Assembly '{draft.Name}' lists precompiled reference '{file}', which is a native DLL; Unity does not reference it"
                                : $"Assembly '{draft.Name}' lists precompiled reference '{file}', which is not in the project; Unity skips it without a message"));
                        continue;
                    }

                    references[draft.Name].AddRange(compatible
                        .Where(c => ProjectPaths.FileName(c.Path).Equals(file, StringComparison.OrdinalIgnoreCase))
                        .Select(c => c.Path));
                }
            }
            else
            {
                references[draft.Name].AddRange(compatible.Where(c => c.Auto).Select(c => c.Path));
            }

            // Editor-only assemblies (every assembly with playModeTestRunnerEnabled) also get nunit.framework.dll, Auto Reference
            // or not, unless they list it themselves (UnityCsReference TestRunnerHelpers.ShouldAddNunitReferences).
            if ((inventory.Settings.PlayModeTestRunnerEnabled || draft.IsEditorOnly) && !ListsNUnit(data))
            {
                references[draft.Name].AddRange(compatible
                    .Where(c => ProjectPaths.FileName(c.Path) == NUnitFramework && !references[draft.Name].Contains(c.Path))
                    .Select(c => c.Path));
            }
        }

        return new PluginResolution(references, analyzers);
    }

    private static bool ListsNUnit(AsmdefData? data) =>
        data is { OverrideReferences: true } && data.PrecompiledReferences.Contains(NUnitFramework, StringComparer.Ordinal);

    // Unity keeps one precompiled assembly per file name (UnityCsReference PrecompiledAssemblyProvider); of DLLs that share a
    // name it keeps the highest assembly version, then the first path (docs/architecture.md, "Precompiled DLLs").
    private static List<(string Path, bool Auto)> OnePerFileName(
        List<(string Path, bool Auto)> candidates, IReadOnlyDictionary<string, string> versions, List<Diagnostic> diagnostics)
    {
        var kept = new List<(string Path, bool Auto)>();
        foreach (var group in candidates.GroupBy(c => ProjectPaths.FileName(c.Path), StringComparer.Ordinal))
        {
            var ordered = group
                .OrderByDescending(c => System.Version.TryParse(versions.GetValueOrDefault(c.Path), out var v) ? v : new System.Version(0, 0))
                .ThenBy(c => c.Path, StringComparer.Ordinal)
                .ToList();
            var winner = ordered[0];
            kept.Add(winner);
            foreach (var loser in ordered.Skip(1))
            {
                diagnostics.Add(new Diagnostic(ProblemIds.ShadowedPrecompiledReference, Severity.Info, DiagnosticOrigin.Ucl, null, loser.Path, 0, 0,
                    $"'{loser.Path}' ({Shown(versions, loser.Path)}) has the same file name as '{winner.Path}' ({Shown(versions, winner.Path)}); "
                    + "Unity keeps one precompiled DLL per file name, the highest version, so this copy is not referenced"));
            }
        }

        return kept.OrderBy(c => c.Path, StringComparer.Ordinal).ToList();
    }

    private static string Shown(IReadOnlyDictionary<string, string> versions, string path) =>
        versions.GetValueOrDefault(path) is { Length: > 0 } v ? $"version {v}" : "no version";

    // Unity's analyzer scope (UnityCsReference RoslynAnalyzers.SetAnalyzers): an analyzer owned by an assembly (its folder is under
    // that assembly's asmdef or asmref folder) applies to that assembly and to every assembly that references it, directly or
    // through others; an analyzer outside every such folder applies to every assembly.
    private static void AddAnalyzer(string path, DefinitionIndex index, Dictionary<string, Draft> drafts, Dictionary<string, List<string>> analyzers)
    {
        var owner = index.OwnerOf(path);
        foreach (var draft in drafts.Values.Where(d => string.IsNullOrEmpty(owner) || d.Name == owner || Reaches(d, owner, drafts, [])))
        {
            analyzers[draft.Name].Add(path);
        }
    }

    private static bool Reaches(Draft from, string target, Dictionary<string, Draft> drafts, HashSet<string> seen) =>
        from.References.Any(r => r == target || (seen.Add(r) && drafts.TryGetValue(r, out var next) && Reaches(next, target, drafts, seen)));
}
