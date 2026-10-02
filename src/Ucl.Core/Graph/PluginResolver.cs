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
        List<Diagnostic> diagnostics)
    {
        var key = cell.IsEditor ? PlatformInfo.EditorPluginKey : PlatformInfo.Of(cell.Platform).PluginKey;
        var references = drafts.Keys.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal);
        var analyzers = drafts.Keys.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal);
        var all = inventory.Plugins
            .OrderBy(p => p.Path, StringComparer.Ordinal)
            .Select(p => (p.Path, Meta: p.MetaText is null ? null : MetaParser.Parse(p.MetaText)))
            .ToList();

        var compatible = new List<(string Path, bool Auto)>();
        foreach (var (path, meta) in all)
        {
            if (meta?.IsRoslynAnalyzer == true)
            {
                AddAnalyzer(path, index, drafts, analyzers);
                continue;
            }

            var settings = meta?.Plugin ?? PluginSettings.Default;
            if (settings.IsCompatibleWith(key) && DefineConstraints.AreSatisfied(settings.DefineConstraints, baseDefines.Contains))
            {
                compatible.Add((path, !settings.IsExplicitlyReferenced));
            }
        }

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

            // Legacy test assemblies also get nunit.framework.dll, Auto Reference or not (UnityCsReference
            // EditorBuildRules.AddTestRunnerPrecompiledReferences).
            if (draft.Entry?.IsTestAssembly == true)
            {
                references[draft.Name].AddRange(compatible
                    .Where(c => ProjectPaths.FileName(c.Path).Equals(NUnitFramework, StringComparison.OrdinalIgnoreCase) && !references[draft.Name].Contains(c.Path))
                    .Select(c => c.Path));
            }
        }

        return new PluginResolution(references, analyzers);
    }

    // Unity analyzer scope: an analyzer under an asmdef folder applies to that assembly and its direct referrers;
    // anywhere else it applies to the predefined assemblies.
    private static void AddAnalyzer(string path, DefinitionIndex index, Dictionary<string, Draft> drafts, Dictionary<string, List<string>> analyzers)
    {
        var owner = index.AsmdefFolderOwner(path);
        if (owner is null)
        {
            foreach (var name in SpecialFolders.Predefined.Where(drafts.ContainsKey))
            {
                analyzers[name].Add(path);
            }

            return;
        }

        var ownerName = owner.Data.Name;
        foreach (var draft in drafts.Values.Where(d => d.Name == ownerName || d.References.Contains(ownerName)))
        {
            analyzers[draft.Name].Add(path);
        }
    }
}
