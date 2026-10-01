using Ucl.Core.Model;
using Ucl.Core.Parsing;
using Ucl.Core.Rules;

namespace Ucl.Core.Graph;

/// <summary>
/// Turns a <see cref="ProjectInventory"/> into the <see cref="AssemblyGraph"/> of one compile cell, applying
/// Unity's rules (docs/architecture.md, "Assembly graph rules"). Pure: no I/O.
/// </summary>
public static class AssemblyGraphBuilder
{
    private const string GlobalRspFolder = "Assets";

    /// <summary>Builds the graph of <paramref name="cell"/>.</summary>
    public static AssemblyGraph Build(ProjectInventory inventory, CompileCell cell)
    {
        var index = new DefinitionIndex(inventory);
        var problems = new List<Problem>(index.Problems);
        var diagnostics = new List<Diagnostic>(index.Diagnostics);
        var info = PlatformInfo.Of(cell.Platform);
        var settings = inventory.Settings;
        var packages = inventory.Packages.ToDictionary(p => p.Name, StringComparer.Ordinal);

        var baseDefines = DefineTable.Compute(cell, settings, packages.ContainsKey("com.unity.test-framework"), out var constraintOnly);
        var playerArgs = RspParser.Parse(settings.AdditionalCompilerArguments.GetValueOrDefault(info.TargetGroup) ?? []);
        var rspByFolder = new Dictionary<string, (string Path, RspOptions Options)>(StringComparer.Ordinal);
        foreach (var rsp in inventory.ResponseFiles.OrderBy(r => r.Path, StringComparer.Ordinal))
        {
            var options = RspParser.Parse(rsp.Text);
            rspByFolder[ProjectPaths.Folder(rsp.Path)] = (rsp.Path, options);
            foreach (var unsupported in options.Unsupported)
            {
                diagnostics.Add(new Diagnostic(ProblemIds.UnsupportedRspOption, Severity.Warning, DiagnosticOrigin.Ucl, null, rsp.Path, 0, 0,
                    $"response file option '{unsupported}' is not supported by ucl and was ignored"));
            }
        }

        // Owners of every script.
        var sources = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var owners = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var script in inventory.Scripts.Order(StringComparer.Ordinal))
        {
            var owner = index.OwnerOf(script);
            if (owner is null)
            {
                if (script.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    diagnostics.Add(new Diagnostic(ProblemIds.PackageScriptWithoutAsmdef, Severity.Warning, DiagnosticOrigin.Ucl, null, script, 0, 0,
                        "script is in a package but not under an assembly definition; Unity does not compile it"));
                    continue;
                }

                owner = SpecialFolders.PredefinedAssemblyFor(script);
            }

            if (owner.Length == 0)
            {
                continue;
            }

            owners[script] = owner;
            if (!sources.TryGetValue(owner, out var list))
            {
                sources[owner] = list = [];
            }

            list.Add(script);
        }

        var excluded = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var drafts = new Dictionary<string, Draft>(StringComparer.Ordinal);

        // asmdef assemblies: membership, defines, options.
        foreach (var entry in index.Asmdefs)
        {
            var data = entry.Data;
            var rsp = rspByFolder.TryGetValue(entry.Folder, out var local) ? local : rspByFolder.GetValueOrDefault(GlobalRspFolder);
            var defines = baseDefines.Copy();
            foreach (var d in rsp.Options?.Defines ?? [])
            {
                defines.Add(d, rsp.Path);
            }

            foreach (var vd in data.VersionDefines)
            {
                var range = VersionRange.Parse(vd.Expression);
                if (!range.Ok || !DefineSet.IsValidSymbol(vd.Define) || vd.Name.Length == 0)
                {
                    diagnostics.Add(new Diagnostic(ProblemIds.BadVersionDefine, Severity.Warning, DiagnosticOrigin.Ucl, data.Name, entry.Path, 0, 0,
                        $"versionDefines entry '{vd.Name}' / '{vd.Expression}' / '{vd.Define}' is invalid and was ignored"));
                    continue;
                }

                var version = vd.Name == "Unity"
                    ? cell.UnityVersion.ToSemantic()
                    : packages.TryGetValue(vd.Name, out var pkg) ? SemanticVersion.Parse(pkg.Version).Value : null;
                if (version is not null && range.Value!.Contains(version))
                {
                    defines.Add(vd.Define, $"{entry.Path} versionDefines");
                }
            }

            var platformKey = cell.IsEditor ? PlatformInfo.EditorAsmdefName : info.AsmdefName;
            bool platformOk = data.IncludePlatforms.Count > 0
                ? data.IncludePlatforms.Contains(platformKey, StringComparer.OrdinalIgnoreCase)
                : !data.ExcludePlatforms.Contains(platformKey, StringComparer.OrdinalIgnoreCase);
            var constraints = entry.IsTestAssembly ? data.DefineConstraints.Append("UNITY_INCLUDE_TESTS") : data.DefineConstraints;
            if (!platformOk)
            {
                excluded[data.Name] = $"platform {platformKey} is not compatible with {entry.Path}";
                continue;
            }

            if (!DefineConstraints.AreSatisfied(constraints, s => defines.Contains(s) || constraintOnly.Contains(s)))
            {
                excluded[data.Name] = $"defineConstraints [{string.Join(", ", constraints)}] not satisfied";
                continue;
            }

            var editorOnly = data.IncludePlatforms.Count == 1 && data.IncludePlatforms[0].Equals(PlatformInfo.EditorAsmdefName, StringComparison.OrdinalIgnoreCase);
            drafts[data.Name] = new Draft(data.Name, AssemblyKind.Asmdef, entry, defines, rsp, data.AllowUnsafeCode, editorOnly);
        }

        // Predefined assemblies exist only when they have scripts; player builds never contain the Editor ones.
        var globalRsp = rspByFolder.GetValueOrDefault(GlobalRspFolder);
        foreach (var name in SpecialFolders.Predefined.Where(sources.ContainsKey))
        {
            if (!cell.IsEditor && SpecialFolders.IsEditorPredefined(name))
            {
                excluded[name] = "Editor scripts are not part of a player build";
                continue;
            }

            var defines = baseDefines.Copy();
            foreach (var d in globalRsp.Options?.Defines ?? [])
            {
                defines.Add(d, globalRsp.Path);
            }

            drafts[name] = new Draft(name, AssemblyKind.Predefined, null, defines, globalRsp, settings.AllowUnsafeCode, SpecialFolders.IsEditorPredefined(name));
        }

        ReferenceResolver.Resolve(index, drafts, diagnostics);
        var edges = drafts.ToDictionary(d => d.Key, d => (IReadOnlyList<string>)d.Value.References, StringComparer.Ordinal);
        foreach (var name in GraphOrdering.FindCycles(edges).Order(StringComparer.Ordinal))
        {
            var path = drafts[name].Entry?.Path;
            diagnostics.Add(new Diagnostic(ProblemIds.CyclicReference, Severity.Error, DiagnosticOrigin.Ucl, name, path, 0, 0,
                $"Assembly '{name}' is part of a cyclic reference chain: {string.Join(" -> ", drafts[name].References.Where(r => edges.ContainsKey(r)))}"));
            excluded[name] = "cyclic references";
            drafts.Remove(name);
        }

        foreach (var draft in drafts.Values)
        {
            var gone = draft.References.Where(r => !drafts.ContainsKey(r)).ToList();
            draft.References.RemoveAll(gone.Contains);
            draft.Dropped.AddRange(gone);
        }

        var plugins = PluginResolver.Resolve(inventory, index, cell, baseDefines, drafts, diagnostics);
        var plans = new Dictionary<string, AssemblyPlan>(StringComparer.Ordinal);
        foreach (var draft in drafts.Values)
        {
            plans[draft.Name] = ToPlan(draft, sources.GetValueOrDefault(draft.Name) ?? [], plugins, playerArgs, settings, inventory, cell);
        }

        var order = GraphOrdering.TopologicalOrder(drafts.ToDictionary(d => d.Key, d => (IReadOnlyList<string>)d.Value.References, StringComparer.Ordinal));
        return new AssemblyGraph
        {
            Cell = cell,
            Assemblies = order.Select(n => plans[n]).ToList(),
            Excluded = excluded,
            ScriptOwners = owners,
            BaseDefines = baseDefines,
            EnabledModules = inventory.Packages.Where(p => p.IsBuiltInModule).Select(p => p.Name["com.unity.modules.".Length..]).Order(StringComparer.Ordinal).ToList(),
            NetFramework = settings.IsNetFramework(info.TargetGroup),
            Diagnostics = diagnostics.OrderBy(d => d.File ?? string.Empty, StringComparer.Ordinal).ThenBy(d => d.Id, StringComparer.Ordinal).ThenBy(d => d.Message, StringComparer.Ordinal).ToList(),
            Problems = problems,
        };
    }

    private static AssemblyPlan ToPlan(
        Draft draft,
        IReadOnlyList<string> sources,
        PluginResolution plugins,
        RspOptions playerArgs,
        ProjectSettingsData settings,
        ProjectInventory inventory,
        CompileCell cell)
    {
        var options = playerArgs.Then(draft.Rsp.Options ?? RspOptions.Empty);
        var noWarn = new SortedSet<string>(StringComparer.Ordinal) { "CS1701", "CS1702" };
        if (settings.SuppressCommonWarnings)
        {
            noWarn.Add("CS0169");
            noWarn.Add("CS0649");
        }

        noWarn.UnionWith(options.NoWarn);
        var ruleSet = options.RuleSet
            ?? (draft.Entry is { } e ? inventory.RuleSets.FirstOrDefault(r => r == $"{e.Folder}/{e.Data.Name}.ruleset") : null)
            ?? inventory.RuleSets.FirstOrDefault(r => r == "Assets/Default.ruleset");
        var configs = new SortedSet<string>(inventory.AnalyzerConfigs, StringComparer.Ordinal);
        configs.UnionWith(options.AnalyzerConfigs);
        var engine = draft.Entry?.Data.NoEngineReferences == true
            ? EngineReferences.None
            : cell.IsEditor ? EngineReferences.RuntimeAndEditor : EngineReferences.Runtime;

        return new AssemblyPlan
        {
            Name = draft.Name,
            Kind = draft.Kind,
            DefinitionPath = draft.Entry?.Path,
            Sources = sources,
            References = draft.References.Order(StringComparer.Ordinal).ToList(),
            DroppedReferences = draft.Dropped.Distinct().Order(StringComparer.Ordinal).ToList(),
            PrecompiledReferences = [.. (plugins.References.GetValueOrDefault(draft.Name) ?? []).Concat(options.References).Distinct().Order(StringComparer.Ordinal)],
            Analyzers = [.. (plugins.Analyzers.GetValueOrDefault(draft.Name) ?? []).Order(StringComparer.Ordinal)],
            Engine = engine,
            Defines = draft.Defines,
            AllowUnsafe = options.Unsafe ?? draft.AllowUnsafe,
            LangVersion = options.LangVersion ?? "9.0",
            Nullable = options.Nullable ?? "disable",
            NoWarn = noWarn.ToList(),
            WarnAsErrorAll = options.WarnAsErrorAll ?? false,
            WarnAsErrorIds = options.WarnAsErrorIds.Distinct().Order(StringComparer.Ordinal).ToList(),
            WarnNotAsErrorIds = options.WarnNotAsErrorIds.Distinct().Order(StringComparer.Ordinal).ToList(),
            AdditionalFiles = options.AdditionalFiles.Distinct().Order(StringComparer.Ordinal).ToList(),
            AnalyzerConfigs = configs.ToList(),
            RuleSet = ruleSet,
            ResponseFile = draft.Rsp.Path,
            IsEditorOnly = draft.IsEditorOnly,
        };
    }
}
