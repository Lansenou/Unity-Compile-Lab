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
    private readonly AnalyzerSet _loader = new();
    private readonly BuildCache? _cache;

    public AssemblyCompiler(IFileSystem fs, ProjectContext project, ReferenceCatalog catalog, ContentHasher hasher, CompileSettings settings, BuildCache? cache)
    {
        _cache = cache;
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
        var sources = plan.Sources.Select(l => (Logical: l, Physical: _project.ToPhysical(l))).ToList();
        foreach (var (logical, physical) in sources)
        {
            hash.Add("source", logical, _hasher.HashFile(physical));
        }

        var referencePaths = new List<(string Display, string Path)>(_catalog.EditorReferences(graph, plan));
        foreach (var dll in plan.PrecompiledReferences)
        {
            var path = _project.ToPhysical(dll);
            if (_fs.FileExists(path))
            {
                referencePaths.Add(($"plugin:{dll}", path));
            }
        }

        foreach (var (display, path) in referencePaths)
        {
            hash.Add("reference", display, _hasher.HashFile(path));
        }

        foreach (var name in plan.References)
        {
            hash.Add("reference", $"assembly:{name}", dependencies[name].ImageHash);
        }

        var analyzerPaths = _settings.Analyzers ? plan.Analyzers : [];
        var editorAnalyzers = _settings.Analyzers ? _catalog.EditorAnalyzers() : [];
        foreach (var (display, path) in editorAnalyzers)
        {
            hash.Add("analyzer", display, _hasher.HashFile(path));
        }

        var analyzerDisplays = analyzerPaths.Concat(editorAnalyzers.Select(a => a.Display)).ToList();
        var configs = plan.AnalyzerConfigs.Select(c => (Logical: c, Physical: _project.ToPhysical(c))).Where(c => _fs.FileExists(c.Physical)).ToList();
        var extraInputs = analyzerPaths.Select(a => ("analyzer", a))
            .Concat(configs.Select(c => ("analyzerconfig", c.Logical)))
            .Concat(plan.AdditionalFiles.Select(f => ("additionalfile", f)))
            .Concat(plan.RuleSet is null ? [] : new[] { ("ruleset", plan.RuleSet) });
        foreach (var (kind, logical) in extraInputs)
        {
            var physical = _project.ToPhysical(logical);
            hash.Add(kind, logical, _fs.FileExists(physical) ? _hasher.HashFile(physical) : "missing");
        }

        if (_settings.FullImages)
        {
            hash.Add("image", "full", "1");
        }

        var inputsHash = hash.Finish(_settings.WarnAsError);
        var displays = referencePaths.Select(r => r.Display).Concat(plan.References.Select(n => $"assembly:{n}")).Order(StringComparer.Ordinal).ToList();
        if (_cache?.TryLoad(inputsHash) is { } hit)
        {
            return new AssemblyOutcome(
                Result(plan, hit.Failed, inputsHash, displays, analyzerDisplays, [], clock.ElapsedMilliseconds, cached: true),
                hit.Image is null ? null : MetadataReference.CreateFromImage(hit.Image),
                hit.ImageHash,
                hit.Image,
                Task.Run(hit.LoadDiagnostics));
        }

        var parseOptions = new CSharpParseOptions(
            LanguageVersionFacts.TryParse(plan.LangVersion, out var lang) ? lang : LanguageVersion.CSharp9,
            DocumentationMode.None,
            SourceCodeKind.Regular,
            plan.Defines.Symbols);
        var trees = new List<SyntaxTree>(sources.Count);
        foreach (var (_, physical) in sources)
        {
            var bytes = _fs.ReadAllBytes(physical);
            var text = SourceText.From(bytes, bytes.Length, Encoding.UTF8, SourceHashAlgorithm.Sha256, throwIfBinaryDetected: false, canBeEmbedded: false);
            trees.Add(CSharpSyntaxTree.ParseText(text, parseOptions, physical));
        }

        var references = referencePaths.Select(r => _catalog.Get(r.Path)).Cast<MetadataReference>()
            .Concat(plan.References.Select(n => dependencies[n].Reference!))
            .ToList();
        var options = CompilerOptionsFactory.Create(plan, _settings.WarnAsError, _fs, _project);
        AnalyzerConfigSet? configSet = null;
        if (configs.Count > 0)
        {
            configSet = AnalyzerConfigSet.Create(configs.Select(c => AnalyzerConfig.Parse(_fs.ReadAllText(c.Physical), c.Physical)).ToList());
            options = options.WithSyntaxTreeOptionsProvider(new TreeOptionsProvider(configSet, trees));
        }

        Microsoft.CodeAnalysis.Compilation compilation = CSharpCompilation.Create(plan.Name, trees, references, options);
        var raw = new List<(Microsoft.CodeAnalysis.Diagnostic Diagnostic, DiagnosticOrigin Origin)>();
        if (analyzerDisplays.Count > 0)
        {
            var physical = analyzerPaths.Select(_project.ToPhysical).Concat(editorAnalyzers.Select(a => a.Path)).ToList();
            compilation = AnalyzerHost.Run(compilation, physical, plan, _project, _fs, _loader, parseOptions, configSet, raw);
        }
        else
        {
            raw.AddRange(compilation.GetDiagnostics().Select(d => (d, DiagnosticOrigin.Compiler)));
        }

        var diagnostics = raw
            .Where(r => r.Diagnostic.Severity != DiagnosticSeverity.Hidden && !r.Diagnostic.IsSuppressed)
            .Where(r => !plan.SuppressWarnings || (r.Diagnostic.Severity == DiagnosticSeverity.Error && !r.Diagnostic.IsWarningAsError))
            .Select(r => Map(r.Diagnostic, r.Origin, plan))
            .Distinct()
            .ToList();
        var failed = diagnostics.Any(d => d.Severity == Severity.Error);

        byte[]? imageBytes = null;
        var imageHash = string.Empty;
        if (!failed)
        {
            using var stream = new MemoryStream();
            var emit = compilation.Emit(stream, options: new EmitOptions(metadataOnly: !_settings.FullImages));
            if (emit.Success)
            {
                imageBytes = stream.ToArray();
                imageHash = ContentHasher.HashBytes(imageBytes);
            }
            else
            {
                failed = true;
                diagnostics.AddRange(emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => Map(d, DiagnosticOrigin.Compiler, plan)));
            }
        }

        var sorted = DiagnosticOrder.Sort(diagnostics);
        _cache?.Store(inputsHash, failed, sorted, imageBytes, imageHash);
        return new AssemblyOutcome(
            Result(plan, failed, inputsHash, displays, analyzerDisplays, sorted, clock.ElapsedMilliseconds, cached: false),
            imageBytes is null ? null : MetadataReference.CreateFromImage(imageBytes),
            imageHash,
            imageBytes);
    }

    private static AssemblyResult Result(
        AssemblyPlan plan, bool failed, string inputsHash, IReadOnlyList<string> references, IReadOnlyList<string> analyzers,
        IReadOnlyList<CoreDiagnostic> diagnostics, long elapsed, bool cached) => new()
        {
            Name = plan.Name,
            Kind = plan.Kind,
            DefinitionPath = plan.DefinitionPath,
            Status = failed ? AssemblyStatus.Failed : AssemblyStatus.Compiled,
            InputsHash = inputsHash,
            Defines = plan.Defines.Symbols.ToList(),
            References = references,
            Analyzers = analyzers,
            SourceCount = plan.Sources.Count,
            Diagnostics = diagnostics,
            ElapsedMs = elapsed,
            Cached = cached,
        };

    private CoreDiagnostic Map(Microsoft.CodeAnalysis.Diagnostic d, DiagnosticOrigin origin, AssemblyPlan plan)
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
        // Roslyn also flags .editorconfig and ruleset escalations as IsWarningAsError; exit code 2 is only for -warnaserror.
        var promoted = d.IsWarningAsError
            && !plan.WarnNotAsErrorIds.Contains(d.Id)
            && (plan.WarnAsErrorAll || _settings.WarnAsError || plan.WarnAsErrorIds.Contains(d.Id));
        return new CoreDiagnostic(d.Id, severity, origin, plan.Name, file, line, column, d.GetMessage(CultureInfo.InvariantCulture), promoted);
    }
}
