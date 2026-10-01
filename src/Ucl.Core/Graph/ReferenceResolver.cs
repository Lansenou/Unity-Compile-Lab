using Ucl.Core.Model;
using Ucl.Core.Rules;

namespace Ucl.Core.Graph;

/// <summary>Fills <see cref="Draft.References"/>: asmdef references by name or GUID, and the predefined assemblies' implicit references.</summary>
internal static class ReferenceResolver
{
    public static void Resolve(DefinitionIndex index, Dictionary<string, Draft> drafts, List<Diagnostic> diagnostics)
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

                var name = target.Data.Name;
                if (name == draft.Name || draft.References.Contains(name) || draft.Dropped.Contains(name))
                {
                    continue;
                }

                (drafts.ContainsKey(name) ? draft.References : draft.Dropped).Add(name);
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
                draft.References.AddRange(autoReferenced);
                draft.References.AddRange(phases.Where(drafts.ContainsKey));
            }
        }
    }
}
