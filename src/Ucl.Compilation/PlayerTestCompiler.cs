using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Ucl.Core.Graph;
using Ucl.Discovery;

namespace Ucl.Compilation;

/// <summary>Compiles Editor test sources against a player, excluding whole unsupported source files.</summary>
public static class PlayerTestCompiler
{
    /// <summary>Emits selected tests and dependencies. Exclusions retain original compiler diagnostics.</summary>
    public static IReadOnlyDictionary<string, string> Compile(AssemblyGraph graph, ProjectContext project,
        string managed, string output, IReadOnlySet<string> tests)
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
            var parse = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: plan.Defines.Symbols);
            var trees = plan.Sources.Select(project.ToPhysical)
                .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), parse, p)).ToList();
            var references = paths.Values.Select(p => MetadataReference.CreateFromFile(p)).ToArray();
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: plan.AllowUnsafe,
                optimizationLevel: OptimizationLevel.Release);
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
