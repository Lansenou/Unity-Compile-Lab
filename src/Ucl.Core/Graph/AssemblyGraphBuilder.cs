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

        var baseDefines = DefineTable.Compute(cell, settings, packages.ContainsKey("com.unity.test-framework"));
        var testables = inventory.Testables.ToHashSet(StringComparer.Ordinal);
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
            if (!sources.ContainsKey(data.Name))
            {
                // Unity compiles no assembly for an asmdef without scripts (its own or an asmref's), and never resolves its references.
                diagnostics.Add(new Diagnostic(ProblemIds.ScriptlessAssembly, Severity.Info, DiagnosticOrigin.Ucl, data.Name, entry.Path, 0, 0,
                    $"Assembly '{data.Name}' has no scripts; Unity compiles no assembly for it"));
                excluded[data.Name] = "no scripts";
                continue;
            }

            var rsp = rspByFolder.TryGetValue(entry.Folder, out var local) ? local : rspByFolder.GetValueOrDefault(GlobalRspFolder);
            var editorOnly = data.IncludePlatforms.Count == 1 && data.IncludePlatforms[0].Equals(PlatformInfo.EditorAsmdefName, StringComparison.OrdinalIgnoreCase);
            var netFramework = settings.IsNetFrameworkFor(info.TargetGroup, editorOnly);
            var defines = DefineTable.ForProfile(baseDefines, netFramework);

            // D61: a test framework assembly (defineConstraints has UNITY_TESTS_FRAMEWORK) gets the symbol wherever
            // UNITY_INCLUDE_TESTS holds; no other assembly does.
            if (entry.IsTestFrameworkAssembly && defines.Contains("UNITY_INCLUDE_TESTS"))
            {
                defines.Add("UNITY_TESTS_FRAMEWORK", "D61: test framework assembly (defineConstraints)");
            }

            foreach (var d in rsp.Options?.Defines ?? [])
            {
                defines.Add(d, rsp.Path);
            }

            foreach (var vd in data.VersionDefines)
            {
                // Resource Unity: versions are written the way Unity prints them (2022.2.14f1); the suffix is ignored (D53).
                var range = VersionRange.Parse(vd.Name == "Unity" ? UnityVersion.WithoutSuffixes(vd.Expression) : vd.Expression);
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
            if (!platformOk)
            {
                excluded[data.Name] = $"platform {platformKey} is not compatible with {entry.Path}";
                continue;
            }

            // Test assemblies and the test framework's own (UNITY_TESTS_FRAMEWORK) stay out of a player unless tests are
            // included (UnityCsReference CustomScriptAssembly.IsCompatibleWith).
            if (!cell.IsEditor && !cell.IncludeTests && (entry.IsTestAssembly || entry.IsTestFrameworkAssembly))
            {
                excluded[data.Name] = "test assemblies are not part of a player build unless --include-tests";
                continue;
            }

            if (!DefineConstraints.AreSatisfied(data.DefineConstraints, defines.Contains))
            {
                excluded[data.Name] = $"defineConstraints [{string.Join(", ", data.DefineConstraints)}] not satisfied";
                continue;
            }

            // A package's test assemblies compile only when the package is embedded or listed in "testables".
            if (IsUntestable(entry, packages, testables))
            {
                excluded[data.Name] = $"package {entry.PackageName} is not testable: its tests compile only when it is embedded in Packages/ or listed in Packages/manifest.json \"testables\"";
                continue;
            }

            if (editorOnly)
            {
                defines.Add(BuiltInDefines.EditorOnlyCompilation, "E02");
            }

            drafts[data.Name] = new Draft(data.Name, AssemblyKind.Asmdef, entry, defines, rsp, data.AllowUnsafeCode, editorOnly, netFramework);
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

            var editorOnly = SpecialFolders.IsEditorPredefined(name);
            var netFramework = settings.IsNetFrameworkFor(info.TargetGroup, editorOnly);
            var defines = DefineTable.ForProfile(baseDefines, netFramework);
            foreach (var d in globalRsp.Options?.Defines ?? [])
            {
                defines.Add(d, globalRsp.Path);
            }

            if (editorOnly)
            {
                defines.Add(BuiltInDefines.EditorOnlyCompilation, "E02");
            }

            drafts[name] = new Draft(name, AssemblyKind.Predefined, null, defines, globalRsp, settings.AllowUnsafeCode, editorOnly, netFramework);
        }

        ReferenceResolver.Resolve(index, drafts, diagnostics, cell, settings.PlayModeTestRunnerEnabled);
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

        var untestable = index.Asmdefs.Where(e => IsUntestable(e, packages, testables)).Select(e => e.Data.Name).ToHashSet(StringComparer.Ordinal);
        var plugins = PluginResolver.Resolve(inventory, index, cell, baseDefines, drafts, untestable, diagnostics);
        var plans = new Dictionary<string, AssemblyPlan>(StringComparer.Ordinal);
        foreach (var draft in drafts.Values)
        {
            plans[draft.Name] = ToPlan(draft, sources.GetValueOrDefault(draft.Name) ?? [], plugins, playerArgs, settings, inventory, cell) with
            {
                SuppressWarnings = draft.Entry?.PackageName is { } owner && packages.TryGetValue(owner, out var package) && IsImmutable(package),
            };
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

    // A package's test assemblies compile only when the package is embedded or listed in "testables".
    private static bool IsUntestable(AsmdefEntry entry, Dictionary<string, ResolvedPackage> packages, HashSet<string> testables) =>
        entry.IsTestAssembly && entry.PackageName is { } package && packages.TryGetValue(package, out var owner)
            && owner.Source != "embedded" && !testables.Contains(package);

    // Unity's immutable package folders (AssetDatabase.TryGetAssetFolderInfo): everything but embedded and local "file:" packages.
    private static bool IsImmutable(ResolvedPackage package) => package.Source is not ("embedded" or "local");

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
        var noWarn = new SortedSet<string>(StringComparer.Ordinal) { "CS0282", "CS1701", "CS1702" };
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
            NetFramework = draft.NetFramework,
        };
    }
}
