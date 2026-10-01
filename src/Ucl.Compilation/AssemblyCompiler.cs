using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Ucl.Core.Graph;
using Ucl.Core.Results;
using Ucl.Discovery;
using CoreDiagnostic = Ucl.Core.Model.Diagnostic;
using DiagnosticOrigin = Ucl.Core.Model.DiagnosticOrigin;
using Severity = Ucl.Core.Model.Severity;

namespace Ucl.Compilation;

/// <summary>Compiles one <see cref="AssemblyPlan"/> with Roslyn using Unity's compiler options.</summary>
internal sealed class AssemblyCompiler
{
    private readonly IFileSystem _fs;
    private readonly ProjectContext _project;
    private readonly ReferenceCatalog _catalog;
    private readonly ContentHasher _hasher;
    private readonly CompileSettings _settings;
    private readonly AnalyzerLoader _loader = new();

    public AssemblyCompiler(IFileSystem fs, ProjectContext project, ReferenceCatalog catalog, ContentHasher hasher, CompileSettings settings)
    {
        _fs = fs;
        _project = project;
        _catalog = catalog;
        _hasher = hasher;
        _settings = settings;
    }

    public AssemblyOutcome Compile(AssemblyGraph graph, AssemblyPlan plan, IReadOnlyDictionary<string, AssemblyOutcome> dependencies)
    {
        var clock = Stopwatch.StartNew();
        var hash = new InputsHashBuilder(_settings.ToolVersion, plan);
        var parseOptions = new CSharpParseOptions(
            LanguageVersionFacts.TryParse(plan.LangVersion, out var lang) ? lang : LanguageVersion.CSharp9,
            DocumentationMode.None,
            SourceCodeKind.Regular,
            plan.Defines.Symbols);

        var trees = new List<SyntaxTree>(plan.Sources.Count);
        foreach (var logical in plan.Sources)
        {
            var physical = _project.ToPhysical(logical);
            var bytes = _fs.ReadAllBytes(physical);
            hash.Add("source", logical, ContentHasher.HashBytes(bytes));
            var text = SourceText.From(bytes, bytes.Length, Encoding.UTF8, SourceHashAlgorithm.Sha256, throwIfBinaryDetected: false, canBeEmbedded: false);
            trees.Add(CSharpSyntaxTree.ParseText(text, parseOptions, physical));
        }

        var references = new List<(string Display, MetadataReference Reference)>();
        foreach (var (display, path) in _catalog.EditorReferences(graph, plan.Engine))
        {
            hash.Add("reference", display, _hasher.HashFile(path));
            references.Add((display, _catalog.Get(path)));
        }

        foreach (var dll in plan.PrecompiledReferences)
        {
            var path = _project.ToPhysical(dll);
            if (!_fs.FileExists(path))
            {
                continue;
            }

            hash.Add("reference", $"plugin:{dll}", _hasher.HashFile(path));
            references.Add(($"plugin:{dll}", _catalog.Get(path)));
        }

        foreach (var name in plan.References)
        {
            var dep = dependencies[name];
            hash.Add("reference", $"assembly:{name}", dep.ImageHash);
            references.Add(($"assembly:{name}", dep.Reference!));
        }

        var analyzerPaths = _settings.Analyzers ? plan.Analyzers : [];
        foreach (var a in analyzerPaths)
        {
            hash.Add("analyzer", a, _hasher.HashFile(_project.ToPhysical(a)));
        }

        var configs = plan.AnalyzerConfigs.Select(c => (Logical: c, Physical: _project.ToPhysical(c))).Where(c => _fs.FileExists(c.Physical)).ToList();
        foreach (var c in configs)
        {
            hash.Add("analyzerconfig", c.Logical, _hasher.HashFile(c.Physical));
        }

        var inputsHash = hash.Finish(_settings.WarnAsError);
        var options = CompilerOptionsFactory.Create(plan, _settings.WarnAsError, _fs, _project);
        AnalyzerConfigSet? configSet = null;
        if (configs.Count > 0)
        {
            configSet = AnalyzerConfigSet.Create(configs.Select(c => AnalyzerConfig.Parse(_fs.ReadAllText(c.Physical), c.Physical)).ToList());
            options = options.WithSyntaxTreeOptionsProvider(new TreeOptionsProvider(configSet, trees));
        }

        Microsoft.CodeAnalysis.Compilation compilation = CSharpCompilation.Create(plan.Name, trees, references.Select(r => r.Reference), options);
        var raw = new List<(Microsoft.CodeAnalysis.Diagnostic Diagnostic, DiagnosticOrigin Origin)>();
        if (analyzerPaths.Count > 0)
        {
            compilation = AnalyzerHost.Run(compilation, analyzerPaths.Select(_project.ToPhysical).ToList(), plan, _project, _fs, _loader, parseOptions, configSet, raw);
        }

        raw.InsertRange(0, compilation.GetDiagnostics().Select(d => (d, DiagnosticOrigin.Compiler)));
        var diagnostics = raw
            .Where(r => r.Diagnostic.Severity != DiagnosticSeverity.Hidden && !r.Diagnostic.IsSuppressed)
            .Select(r => Map(r.Diagnostic, r.Origin, plan.Name))
            .Distinct()
            .ToList();
        var failed = diagnostics.Any(d => d.Severity == Severity.Error);

        MetadataReference? image = null;
        var imageHash = string.Empty;
        if (!failed)
        {
            using var stream = new MemoryStream();
            var emit = compilation.Emit(stream, options: new EmitOptions(metadataOnly: true));
            if (emit.Success)
            {
                var bytes = stream.ToArray();
                imageHash = ContentHasher.HashBytes(bytes);
                image = MetadataReference.CreateFromImage(bytes);
            }
            else
            {
                failed = true;
                diagnostics.AddRange(emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => Map(d, DiagnosticOrigin.Compiler, plan.Name)));
            }
        }

        var result = new AssemblyResult
        {
            Name = plan.Name,
            Kind = plan.Kind,
            DefinitionPath = plan.DefinitionPath,
            Status = failed ? AssemblyStatus.Failed : AssemblyStatus.Compiled,
            InputsHash = inputsHash,
            Defines = plan.Defines.Symbols.ToList(),
            References = references.Select(r => r.Display).Order(StringComparer.Ordinal).ToList(),
            Analyzers = analyzerPaths,
            SourceCount = plan.Sources.Count,
            Diagnostics = DiagnosticOrder.Sort(diagnostics),
            ElapsedMs = clock.ElapsedMilliseconds,
        };
        return new AssemblyOutcome(result, image, imageHash);
    }

    private CoreDiagnostic Map(Microsoft.CodeAnalysis.Diagnostic d, DiagnosticOrigin origin, string assembly)
    {
        string? file = null;
        int line = 0, column = 0;
        if (d.Location.IsInSource)
        {
            var span = d.Location.GetMappedLineSpan();
            var path = span.HasMappedPath ? span.Path : d.Location.SourceTree!.FilePath;
            file = _project.ToLogical(path) ?? path.Replace('\\', '/');
            line = span.StartLinePosition.Line + 1;
            column = span.StartLinePosition.Character + 1;
        }

        var severity = d.Severity switch
        {
            DiagnosticSeverity.Error => Severity.Error,
            DiagnosticSeverity.Warning => Severity.Warning,
            _ => Severity.Info,
        };
        return new CoreDiagnostic(d.Id, severity, origin, assembly, file, line, column, d.GetMessage(CultureInfo.InvariantCulture), d.IsWarningAsError);
    }
}
