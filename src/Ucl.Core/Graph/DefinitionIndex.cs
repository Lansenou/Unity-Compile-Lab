using Ucl.Core.Model;
using Ucl.Core.Parsing;
using Ucl.Core.Rules;

namespace Ucl.Core.Graph;

/// <summary>
/// Indexes asmdefs and asmrefs: by name, by GUID and by folder, and assigns every script to its owning
/// assembly. Collects the problems found on the way. Independent of the compile cell.
/// </summary>
public sealed class DefinitionIndex
{
    private readonly Dictionary<string, AsmdefEntry> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AsmdefEntry> _byGuid = new(StringComparer.Ordinal);

    // Folder -> owning assembly name; empty string means "owned by an asmref that resolves to nothing".
    private readonly Dictionary<string, string> _folderOwner = new(StringComparer.Ordinal);
    private readonly List<Problem> _problems = [];
    private readonly List<Diagnostic> _diagnostics = [];

    /// <summary>Builds the index.</summary>
    public DefinitionIndex(ProjectInventory inventory)
    {
        foreach (var file in inventory.Asmdefs.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var parsed = AsmdefParser.ParseAsmdef(file.Text);
            if (!parsed.Ok)
            {
                _problems.Add(new Problem(ProblemIds.BadAsmdef, $"{file.Path}: {parsed.Error}", file.Path));
                continue;
            }

            var entry = new AsmdefEntry(file.Path, parsed.Value!, file.MetaText is null ? null : MetaParser.Parse(file.MetaText).Guid);
            if (_byName.TryGetValue(entry.Data.Name, out var existing) || SpecialFolders.Predefined.Contains(entry.Data.Name))
            {
                _problems.Add(new Problem(
                    ProblemIds.DuplicateAssembly,
                    $"{file.Path}: assembly name '{entry.Data.Name}' is already used by {existing?.Path ?? "a predefined assembly"}",
                    file.Path));
                continue;
            }

            if (!ClaimFolder(entry.Folder, entry.Data.Name, file.Path))
            {
                continue;
            }

            _byName[entry.Data.Name] = entry;
            if (entry.Guid is not null)
            {
                _byGuid.TryAdd(entry.Guid, entry);
            }
        }

        foreach (var file in inventory.Asmrefs.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var parsed = AsmdefParser.ParseAsmref(file.Text);
            if (!parsed.Ok)
            {
                _problems.Add(new Problem(ProblemIds.BadAsmdef, $"{file.Path}: {parsed.Error}", file.Path));
                continue;
            }

            var target = Resolve(parsed.Value!);
            if (target is null)
            {
                _diagnostics.Add(new Diagnostic(
                    ProblemIds.MissingAsmrefTarget, Severity.Warning, DiagnosticOrigin.Ucl, null, file.Path, 0, 0,
                    $"Assembly definition reference '{file.Path}' points to '{parsed.Value}', which does not exist; its scripts are not compiled"));
            }

            ClaimFolder(ProjectPaths.Folder(file.Path), target?.Data.Name ?? string.Empty, file.Path);
        }
    }

    /// <summary>All valid asmdefs, sorted by name.</summary>
    public IEnumerable<AsmdefEntry> Asmdefs => _byName.Values.OrderBy(e => e.Data.Name, StringComparer.Ordinal);

    /// <summary>Problems found while indexing.</summary>
    public IReadOnlyList<Problem> Problems => _problems;

    /// <summary>Diagnostics found while indexing.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>Resolves a reference string: <c>GUID:&lt;hex&gt;</c> or an assembly name.</summary>
    public AsmdefEntry? Resolve(string reference)
    {
        if (reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
        {
            return _byGuid.GetValueOrDefault(reference[5..].Trim().ToLowerInvariant());
        }

        return _byName.GetValueOrDefault(reference);
    }

    /// <summary>The asmdef whose folder holds <paramref name="path"/> (nearest ancestor), ignoring asmrefs; used for analyzer scope.</summary>
    public AsmdefEntry? AsmdefFolderOwner(string path) =>
        ProjectPaths.SelfAndAncestors(ProjectPaths.Folder(path))
            .Select(f => _byName.Values.FirstOrDefault(e => e.Folder == f))
            .FirstOrDefault(e => e is not null);

    /// <summary>
    /// The owner of a script: an assembly name, <see cref="string.Empty"/> when the script is owned by an asmref
    /// that resolves to nothing, or null when no asmdef or asmref covers it.
    /// </summary>
    public string? OwnerOf(string scriptPath)
    {
        foreach (var folder in ProjectPaths.SelfAndAncestors(ProjectPaths.Folder(scriptPath)))
        {
            if (_folderOwner.TryGetValue(folder, out var owner))
            {
                return owner;
            }
        }

        return null;
    }

    private bool ClaimFolder(string folder, string owner, string path)
    {
        if (_folderOwner.ContainsKey(folder))
        {
            _problems.Add(new Problem(
                ProblemIds.MultipleDefinitionsInFolder,
                $"{path}: folder '{folder}' already has an assembly definition or reference",
                path));
            return false;
        }

        _folderOwner[folder] = owner;
        return true;
    }
}
