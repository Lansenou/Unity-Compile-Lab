using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length != 4)
{
    Console.Error.WriteLine("usage: compiler <exported-csproj-directory> <player-managed> <project-list> <output>");
    return 2;
}
var export = args[0];
var managed = args[1];
var output = args[3];
Directory.CreateDirectory(output);
var projectPaths = File.ReadAllLines(args[2]).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
var assemblyPaths = Directory.GetFiles(managed, "*.dll").ToList();
var reports = new List<object>();
var built = new List<string>();
var pending = new HashSet<string>(projectPaths, StringComparer.Ordinal);
while (pending.Count > 0)
{
    var selected = pending.FirstOrDefault(p =>
    {
        var doc = XDocument.Load(Path.Combine(export, p));
        return !doc.Descendants("ProjectReference").Any(r => pending.Contains((string)r.Attribute("Include")!));
    });
    if (selected is null) throw new InvalidOperationException("Selected project dependency cycle.");
    pending.Remove(selected);
    var document = XDocument.Load(Path.Combine(export, selected));
    var name = document.Descendants("AssemblyName").Single().Value;
    // Keep EditMode conditionals so unsupported assertions are not compiled away.
    // The reference set remains player-only; Editor-dependent files are excluded.
    var defines = document.Descendants("DefineConstants").Single().Value.Split(';');
    var parse = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: defines);
    var trees = document.Descendants("Compile").Select(c => (string)c.Attribute("Include")!)
        .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), parse, p)).ToList();
    var removed = new List<object>();
    var references = assemblyPaths.DistinctBy(Path.GetFileNameWithoutExtension)
        .Select(p => MetadataReference.CreateFromFile(p)).ToArray();
    var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
        allowUnsafe: document.Descendants("AllowUnsafeBlocks").FirstOrDefault()?.Value == "true",
        optimizationLevel: OptimizationLevel.Release);
    int rounds = 0;
    while (true)
    {
        rounds++;
        var compilation = CSharpCompilation.Create(name, trees, references, options);
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length == 0)
        {
            var path = Path.Combine(output, name + ".dll");
            using var stream = File.Create(path);
            var result = compilation.Emit(stream);
            if (!result.Success) throw new InvalidOperationException(string.Join("\n", result.Diagnostics));
            assemblyPaths.Add(path);
            built.Add(path);
            reports.Add(new { assembly = name, sourceFiles = trees.Count, rounds, excluded = removed });
            break;
        }
        var failed = errors.Select(d => d.Location.SourceTree).OfType<SyntaxTree>().Distinct().ToArray();
        if (failed.Length == 0)
            throw new InvalidOperationException("Assembly-level errors: " + string.Join("\n", errors.Select(e => e.ToString())));
        foreach (var tree in failed)
        {
            var diagnostics = errors.Where(d => d.Location.SourceTree == tree).Select(d => d.ToString()).ToArray();
            var types = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
                .Select(t => t.Identifier.Text).ToArray();
            removed.Add(new { path = tree.FilePath, types, diagnostics });
            trees.Remove(tree);
        }
        if (trees.Count == 0)
        {
            reports.Add(new { assembly = name, sourceFiles = 0, rounds, excluded = removed });
            break;
        }
    }
}
File.WriteAllText(Path.Combine(output, "compile-report.json"), JsonSerializer.Serialize(reports,
    new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllLines(Path.Combine(output, "built-assemblies.txt"), built);
Console.WriteLine($"Built {built.Count} selected assemblies without the Editor.");
return 0;
