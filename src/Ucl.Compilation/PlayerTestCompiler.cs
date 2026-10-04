using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Ucl.Core.Graph;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>Compiles Editor test sources against a player, excluding whole unsupported source files.</summary>
public static class PlayerTestCompiler
{
    /// <summary>Content key for all cell/compiler inputs, including external files and the adapter binary.</summary>
    public static string Key(AssemblyGraph graph, ProjectContext project, IReadOnlySet<string> tests, bool analyzers, IReadOnlyList<string>? editorGenerators = null)
    {
        var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["protocol"] = "player-test-compiler/2",
            ["graph"] = JsonSerializer.Serialize(graph),
            ["tests"] = string.Join("\n", tests.Order(StringComparer.Ordinal)),
            ["analyzers"] = analyzers.ToString(),
            ["editorGenerators"] = string.Join("\n", (editorGenerators ?? []).Select(p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))),
            ["adapter"] = BinaryIdentity(typeof(PlayerTestCompiler).Assembly),
            ["roslyn"] = BinaryIdentity(typeof(CSharpCompilation).Assembly),
        };
        foreach (var plan in graph.Assemblies)
            foreach (var logical in plan.Sources.Concat(plan.PrecompiledReferences).Concat(plan.Analyzers)
                .Concat(plan.AnalyzerConfigs).Concat(plan.AdditionalFiles).Concat(plan.RuleSet is null ? [] : new[] { plan.RuleSet }))
            {
                var path = project.ToPhysical(logical);
                inputs["file:" + logical] = File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "missing";
            }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(inputs))));
    }

    private static string BinaryIdentity(Assembly assembly)
    {
        var path = Path.Combine(AppContext.BaseDirectory, assembly.GetName().Name + ".dll");
        if (!File.Exists(path)) path = Environment.ProcessPath;
        if (path is null || !File.Exists(path)) throw new InvalidOperationException("Cannot identify the tool binary for the player-test cache.");
        using var stream = File.OpenRead(path);
        return assembly.ManifestModule.ModuleVersionId.ToString("N") + ":" + Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>Emits selected tests and dependencies. Exclusions retain original compiler diagnostics.</summary>
    public static IReadOnlyDictionary<string, string> Compile(AssemblyGraph graph, ProjectContext project,
        string managed, string output, IReadOnlySet<string> tests, bool analyzers = true, IReadOnlyList<string>? editorGenerators = null)
    {
        Directory.CreateDirectory(output);
        var paths = Directory.GetFiles(managed, "*.dll").ToDictionary(p => Path.GetFileNameWithoutExtension(p)!, p => p, StringComparer.OrdinalIgnoreCase);
        var needed = new HashSet<string>(tests, StringComparer.Ordinal);
        foreach (var plan in graph.Assemblies.Reverse().Where(p => needed.Contains(p.Name)))
            needed.UnionWith(plan.References);
        var exclusions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var plan in graph.Assemblies.Where(p => needed.Contains(p.Name)))
        {
            if (paths.ContainsKey(plan.Name) && !tests.Contains(plan.Name)) continue;
            var unsupported = analyzers && (plan.Analyzers.Count > 0 || editorGenerators is { Count: > 0 }) ? "Source generators/analyzers require the Editor adapter."
                : plan.References.Any(n => !paths.ContainsKey(n)) ? "A dependency requires the Editor adapter." : null;
            if (unsupported is not null)
            {
                foreach (var source in plan.Sources) exclusions[project.ToPhysical(source)] = unsupported;
                continue;
            }
            var parse = new CSharpParseOptions(LanguageVersionFacts.TryParse(plan.LangVersion, out var language) ? language : LanguageVersion.CSharp9,
                DocumentationMode.None, SourceCodeKind.Regular, plan.Defines.Symbols);
            var trees = plan.Sources.Select(project.ToPhysical)
                .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), parse, p)).ToList();
            var referencePaths = new Dictionary<string, string>(paths, StringComparer.OrdinalIgnoreCase);
            foreach (var reference in plan.PrecompiledReferences)
            {
                var path = project.ToPhysical(reference);
                if (File.Exists(path)) referencePaths.TryAdd(Path.GetFileNameWithoutExtension(path), path);
            }
            var references = referencePaths.Values.Select(p => MetadataReference.CreateFromFile(p)).ToArray();
            var fs = new PhysicalFileSystem(project.Root);
            var options = CompilerOptionsFactory.Create(plan, false, fs, project);
            var configs = plan.AnalyzerConfigs.Select(project.ToPhysical).Where(File.Exists).ToList();
            if (configs.Count > 0)
            {
                var configSet = AnalyzerConfigSet.Create(configs.Select(p => AnalyzerConfig.Parse(File.ReadAllText(p), p)).ToList());
                options = options.WithSyntaxTreeOptionsProvider(new TreeOptionsProvider(configSet, trees));
            }
            // Player dataPath is not the input Assets tree. Remove whole context-dependent files;
            // normal compiler diagnostics then exclude inherited fixtures and helper callers.
            var ownership = CSharpCompilation.Create(plan.Name, trees, references, options);
            foreach (var tree in trees.ToArray())
            {
                var names = tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>()
                    .Where(n => n.Identifier.ValueText == "dataPath").ToArray();
                if (names.Length == 0) continue;
                var model = ownership.GetSemanticModel(tree);
                if (!names.Any(n => model.GetSymbolInfo(n).Symbol is IPropertySymbol property
                    && property.ContainingType.ToDisplayString() == "UnityEngine.Application")) continue;
                exclusions[tree.FilePath] = "Application.dataPath source context requires the Editor.";
                trees.Remove(tree);
            }
            while (trees.Count > 0)
            {
                var compilation = CSharpCompilation.Create(plan.Name, trees, references, options);
                var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                if (errors.Length == 0)
                {
                    var path = Path.Combine(output, plan.Name + ".dll");
                    using var stream = File.Create(path);
                    var result = compilation.Emit(stream);
                    if (!result.Success) throw new InvalidOperationException(string.Join("\n", result.Diagnostics));
                    paths[plan.Name] = path;
                    break;
                }
                var failed = errors.Select(d => d.Location.SourceTree).OfType<SyntaxTree>().Distinct().ToList();
                if (failed.Count == 0) throw new InvalidOperationException(string.Join("\n", errors.Select(d => d.ToString())));
                foreach (var tree in failed)
                {
                    exclusions[tree.FilePath] = string.Join("\n", errors.Where(d => d.Location.SourceTree == tree).Select(d => d.ToString()));
                    trees.Remove(tree);
                }
            }
        }
        return exclusions;
    }
}
