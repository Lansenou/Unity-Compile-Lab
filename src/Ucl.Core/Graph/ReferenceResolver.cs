using Ucl.Core.Model;
using Ucl.Core.Rules;

namespace Ucl.Core.Graph;

/// <summary>
/// Fills <see cref="Draft.References"/>: asmdef references by name or GUID, the predefined assemblies' implicit references,
/// the auto-referenced uGUI assemblies and the test runner assemblies (docs/architecture.md, "Assembly graph rules").
/// </summary>
internal static class ReferenceResolver
{
    /// <summary>The Unity Test Framework's assemblies.</summary>
    public static readonly string[] TestRunnerAssemblies = ["UnityEngine.TestRunner", "UnityEditor.TestRunner"];

    /// <summary>The uGUI assemblies Unity adds to every asmdef assembly (UnityCsReference AutoReferencedPackageAssemblies).</summary>
    public const string RuntimeUi = "UnityEngine.UI";

    /// <summary>The Editor half of <see cref="RuntimeUi"/>, added in editor cells only.</summary>
    public const string EditorUi = "UnityEditor.UI";

    public static void Resolve(DefinitionIndex index, Dictionary<string, Draft> drafts, List<Diagnostic> diagnostics, CompileCell cell, bool playModeTests)
    {
        foreach (var draft in drafts.Values.Where(d => d.Entry is not null).OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            var entry = draft.Entry!;
            foreach (var reference in entry.Data.References)
            {
                var target = index.Resolve(reference);
                if (target is null)
                {
                    diagnostics.Add(new Diagnostic(ProblemIds.MissingReference, Severity.Warning, DiagnosticOrigin.Ucl, draft.Name, entry.Path, 0, 0,
                        $"Assembly '{draft.Name}' references '{reference}', which does not exist; the reference is ignored"));
                    continue;
                }

                Add(draft, target.Data.Name, drafts);
            }

            // Legacy test assemblies (optionalUnityReferences) also reference the test runner assemblies
            // (UnityCsReference CustomScriptAssemblyWithLegacyData.UpdateLegacyData).
            if (entry.IsLegacyTestAssembly)
            {
                AddExisting(draft, TestRunnerAssemblies, index, drafts);
            }

            // uGUI: every asmdef assembly except the UI and test runner assemblies themselves, noEngineReferences ones and
            // code-gen ones (UnityCsReference AutoReferencedPackageAssemblies, EditorBuildRules.ToScriptAssemblies).
            if (!entry.Data.NoEngineReferences && !CodeGenAssemblies.IsCodeGen(draft.Name)
                && draft.Name is not (RuntimeUi or EditorUi) && !TestRunnerAssemblies.Contains(draft.Name))
            {
                AddExisting(draft, cell.IsEditor ? [RuntimeUi, EditorUi] : [RuntimeUi], index, drafts);
            }
        }

        // Predefined assemblies see every auto-referenced asmdef compiled in the cell and earlier phases.
        var autoReferenced = drafts.Values
            .Where(d => d.Entry is { Data.AutoReferenced: true, IsTestAssembly: false })
            .Select(d => d.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        var earlier = new Dictionary<string, string[]>
        {
            [SpecialFolders.FirstPass] = [],
            [SpecialFolders.Main] = [SpecialFolders.FirstPass],
            [SpecialFolders.EditorFirstPass] = [SpecialFolders.FirstPass],
            [SpecialFolders.Editor] = [SpecialFolders.FirstPass, SpecialFolders.Main, SpecialFolders.EditorFirstPass],
        };
        foreach (var (name, phases) in earlier)
        {
            if (drafts.TryGetValue(name, out var draft))
            {
                // Runtime assemblies never see Editor-only assemblies, even in the Editor: that is what keeps a
                // player build from depending on editor code.
                draft.References.AddRange(SpecialFolders.IsEditorPredefined(name) ? autoReferenced : autoReferenced.Where(a => !drafts[a].IsEditorOnly));
                draft.References.AddRange(phases.Where(drafts.ContainsKey));
            }
        }

        // Test runner: Editor-only assemblies (every assembly with playModeTestRunnerEnabled) that do not reference a test runner
        // assembly already get both, except code-gen assemblies and the runners themselves (UnityCsReference
        // TestRunnerHelpers.ShouldAddTestRunnerReferences).
        foreach (var draft in drafts.Values.OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            var listsRunner = TestRunnerAssemblies.Any(r => draft.References.Contains(r) || draft.Dropped.Contains(r));
            if (!listsRunner && (playModeTests || draft.IsEditorOnly) && !CodeGenAssemblies.IsCodeGen(draft.Name) && !TestRunnerAssemblies.Contains(draft.Name))
            {
                AddExisting(draft, TestRunnerAssemblies, index, drafts);
            }
        }
    }

    // Adds the assemblies that exist in the project (compiled in this cell or not) and are not already listed.
    private static void AddExisting(Draft draft, IEnumerable<string> names, DefinitionIndex index, Dictionary<string, Draft> drafts)
    {
        foreach (var name in names.Where(n => index.Resolve(n) is not null))
        {
            Add(draft, name, drafts);
        }
    }

    private static void Add(Draft draft, string name, Dictionary<string, Draft> drafts)
    {
        if (name == draft.Name || draft.References.Contains(name) || draft.Dropped.Contains(name))
        {
            return;
        }

        (drafts.ContainsKey(name) ? draft.References : draft.Dropped).Add(name);
    }
}
