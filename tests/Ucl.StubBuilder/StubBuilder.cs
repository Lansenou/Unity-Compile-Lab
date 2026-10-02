using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ucl.StubBuilder;

/// <summary>
/// Compiles <c>fixtures/_stubs</c> into fake Unity editor installs and plugin/analyzer DLLs
/// (docs/fixtures.md). Deterministic; a no-op when the stamp over the stub sources matches.
/// </summary>
public static class StubBuilder
{
    /// <summary>Editor versions laid out under <c>editors/</c>; all get identical bytes.</summary>
    public static IReadOnlyList<string> EditorVersions { get; } = ["6000.0.30f1", "6000.3.2f1", "6000.3.19f1"];

    /// <summary>Bump when the layout or compile settings change, so existing stamps are invalidated.</summary>
    private const string BuilderVersion = "ucl-stubs/7";

    private const string CoreModule = "UnityEngine.CoreModule";

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp9, DocumentationMode.None);

    /// <summary>Builds every stub into <paramref name="outDir"/> unless its <c>stamp.txt</c> is current. Returns <paramref name="outDir"/>.</summary>
    public static string Build(string repoRoot, string outDir)
    {
        var stubs = Path.Combine(Path.GetFullPath(repoRoot), "fixtures", "_stubs");
        if (!Directory.Exists(stubs))
        {
            throw new DirectoryNotFoundException($"stub sources not found: {stubs}");
        }

        outDir = Path.GetFullPath(outDir);
        var stamp = ComputeStamp(stubs);
        var stampPath = Path.Combine(outDir, "stamp.txt");
        if (ReadStamp(stampPath) == stamp)
        {
            return outDir;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outDir)!);
        using (AcquireLock(outDir + ".lock"))
        {
            if (ReadStamp(stampPath) == stamp)
            {
                return outDir;
            }

            var staging = outDir + ".staging";
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            BuildInto(stubs, staging);
            File.WriteAllText(Path.Combine(staging, "stamp.txt"), stamp + "\n");
            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, recursive: true);
            }

            Directory.Move(staging, outDir);
        }

        return outDir;
    }

    private static void BuildInto(string stubs, string outDir)
    {
        var netstandardPath = Path.Combine(AppContext.BaseDirectory, "netstandard.dll");
        if (!File.Exists(netstandardPath))
        {
            throw new FileNotFoundException("netstandard.dll was not copied next to Ucl.StubBuilder", netstandardPath);
        }

        var netstandard = MetadataReference.CreateFromFile(netstandardPath);
        var profile = ProfileStubs.Build(Path.Combine(stubs, "profiles", "unity-4.8-api", "mscorlib"), netstandardPath, ParseOptions);

        // Editor modules: CoreModule first, every other module references it.
        var editorDir = Path.Combine(stubs, "editor");
        var modules = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var core = Compile(CoreModule, Path.Combine(editorDir, CoreModule), [netstandard]);
        modules[CoreModule] = core;
        var coreReference = MetadataReference.CreateFromImage(core);
        foreach (var dir in SubDirectories(editorDir).Where(d => Path.GetFileName(d) != CoreModule))
        {
            var name = Path.GetFileName(dir);
            modules[name] = Compile(name, dir, [netstandard, coreReference]);
        }

        var moduleReferences = modules.ToDictionary(m => m.Key, m => (MetadataReference)MetadataReference.CreateFromImage(m.Value), StringComparer.Ordinal);

        // Managed/UnityEngine/UnityEngine.dll: the facade that forwards every public engine type to its module.
        var engineModules = moduleReferences.Where(m => m.Key.StartsWith("UnityEngine.", StringComparison.Ordinal)).Select(m => m.Value).ToList();
        var facade = CompileSources(
            "UnityEngine",
            [ProfileStubs.Forwarders(engineModules.SelectMany(ProfileStubs.PublicTypes).Distinct().Order(StringComparer.Ordinal))],
            [netstandard, .. engineModules]);

        // Editor files outside Managed/UnityEngine (editor-extra/<Name>/, stub.ini path=).
        var extras = new List<(string DataPath, string Name, byte[] Image)>();
        foreach (var dir in SubDirectories(Path.Combine(stubs, "editor-extra")))
        {
            var ini = StubIni.Read(dir);
            var name = ini.Name ?? Path.GetFileName(dir);
            var sources = ini.Sources is { } s ? Path.Combine(stubs, s.Replace('/', Path.DirectorySeparatorChar)) : dir;
            var image = ini.Kind == "analyzer"
                ? CompileAnalyzer(name, sources, netstandard)
                : Compile(name, sources, [netstandard, .. ini.References.Select(r => moduleReferences[r])], ini.Version);
            extras.Add((ini.DataPath ?? throw new InvalidOperationException($"{dir}/stub.ini: path= is required"), name, image));
        }

        foreach (var version in EditorVersions)
        {
            var data = Path.Combine(outDir, "editors", version, "Editor", "Data");
            var managed = Path.Combine(data, "Managed", "UnityEngine");
            Directory.CreateDirectory(managed);
            foreach (var (name, image) in modules)
            {
                File.WriteAllBytes(Path.Combine(managed, name + ".dll"), image);
            }

            File.WriteAllBytes(Path.Combine(managed, "UnityEngine.dll"), facade);
            foreach (var (dataPath, name, image) in extras)
            {
                var folder = Path.Combine(data, dataPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".dll"), image);
            }

            var reference = Path.Combine(data, "NetStandard", "ref", "2.1.0");
            Directory.CreateDirectory(reference);
            File.Copy(netstandardPath, Path.Combine(reference, "netstandard.dll"), overwrite: true);
            var netfxShims = Path.Combine(data, "NetStandard", "compat", "2.1.0", "shims", "netfx");
            Directory.CreateDirectory(netfxShims);
            File.WriteAllBytes(Path.Combine(netfxShims, "mscorlib.dll"), profile.NetfxMscorlibShim);
            var shims = Path.Combine(data, "NetStandard", "compat", "2.1.0", "shims", "netstandard");
            Directory.CreateDirectory(shims);
            File.WriteAllBytes(Path.Combine(shims, "System.Runtime.dll"), profile.NetStandardSystemRuntimeShim);

            var netfx = Path.Combine(data, "UnityReferenceAssemblies", "unity-4.8-api");
            Directory.CreateDirectory(Path.Combine(netfx, "Facades"));
            File.WriteAllBytes(Path.Combine(netfx, "mscorlib.dll"), profile.Mscorlib);
            File.WriteAllBytes(Path.Combine(netfx, "Facades", "netstandard.dll"), profile.NetStandardFacade);
            File.WriteAllBytes(Path.Combine(netfx, "Facades", "System.Runtime.dll"), profile.SystemRuntimeFacade);
        }

        var dlls = Path.Combine(outDir, "dlls");
        Directory.CreateDirectory(dlls);

        // A monolithic "UnityEngine" (CoreModule's sources under the old single-assembly name), for engine=UnityEngine stubs.
        var monolithic = MetadataReference.CreateFromImage(Compile("UnityEngine", Path.Combine(editorDir, CoreModule), [netstandard]));
        foreach (var dir in SubDirectories(Path.Combine(stubs, "dlls")))
        {
            var file = Path.GetFileName(dir);
            var ini = StubIni.Read(dir);
            var usesEngine = SourceFiles(dir).Any(f => File.ReadAllText(f).Contains("UnityEngine", StringComparison.Ordinal));
            MetadataReference baseReference = ini.Profile switch
            {
                "netstandard2.0" => MetadataReference.CreateFromImage(profile.NetStandard20Contract),
                "System.Runtime" => MetadataReference.CreateFromImage(profile.SystemRuntimeContract),
                _ => netstandard,
            };
            var engine = ini.Engine == "UnityEngine" ? monolithic : coreReference;
            MetadataReference[] references = usesEngine ? [baseReference, engine] : [baseReference];
            File.WriteAllBytes(Path.Combine(dlls, file + ".dll"), Compile(ini.Name ?? file, dir, references, ini.Version));
        }

        File.Copy(Path.Combine(AppContext.BaseDirectory, "nunit", "nunit.framework.dll"), Path.Combine(dlls, "nunit.framework.dll"), overwrite: true);

        foreach (var dir in SubDirectories(Path.Combine(stubs, "native")))
        {
            File.WriteAllBytes(Path.Combine(dlls, Path.GetFileName(dir) + ".dll"), NativeImage.Build());
        }

        foreach (var dir in SubDirectories(Path.Combine(stubs, "analyzers")))
        {
            var name = Path.GetFileName(dir);
            File.WriteAllBytes(Path.Combine(dlls, name + ".dll"), CompileAnalyzer(name, dir, netstandard));
        }
    }

    // Analyzers compile against netstandard.dll and the Roslyn DLLs this process loaded. On a .NET 10 host those are
    // the net10.0 Roslyn builds, which reference System.Runtime rather than netstandard (CS0012), so that attempt
    // fails and the runtime's own managed assemblies are referenced instead. The resulting analyzer targets the
    // running .NET, which is what ucl loads it into.
    private static byte[] CompileAnalyzer(string name, string dir, MetadataReference netstandard)
    {
        var roslyn = new[]
        {
            typeof(Compilation).Assembly.Location,
            typeof(CSharpCompilation).Assembly.Location,
            typeof(ImmutableArray).Assembly.Location,
        }.Distinct(StringComparer.Ordinal).Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();

        if (TryCompile(name, dir, [netstandard, .. roslyn], out var image, out _))
        {
            return image!;
        }

        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        var runtimeReferences = Directory.EnumerateFiles(runtime, "*.dll")
            .Order(StringComparer.Ordinal)
            .Where(IsManaged)
            .Where(p => !roslyn.OfType<PortableExecutableReference>().Any(r => Path.GetFileName(r.FilePath) == Path.GetFileName(p)))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));
        if (TryCompile(name, dir, [.. runtimeReferences, .. roslyn], out image, out var errors))
        {
            return image!;
        }

        throw new InvalidOperationException($"stub {name} does not compile:{Environment.NewLine}{errors}");
    }

    private static byte[] CompileSources(string name, IEnumerable<(string Path, string Text)> sources, IEnumerable<MetadataReference> references)
    {
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s.Text, ParseOptions, path: $"{name}/{s.Path}", encoding: Encoding.UTF8));
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, deterministic: true);
        using var pe = new MemoryStream();
        var result = CSharpCompilation.Create(name, trees, references, options).Emit(pe);
        return result.Success
            ? pe.ToArray()
            : throw new InvalidOperationException($"stub {name} does not compile:{Environment.NewLine}"
                + string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
    }

    private static byte[] Compile(string name, string dir, IEnumerable<MetadataReference> references, string? version = null) =>
        TryCompile(name, dir, references, out var image, out var errors, version)
            ? image!
            : throw new InvalidOperationException($"stub {name} does not compile:{Environment.NewLine}{errors}");

    private static bool TryCompile(string name, string dir, IEnumerable<MetadataReference> references, out byte[]? image, out string errors, string? version = null)
    {
        var trees = SourceFiles(dir)
            .Select(f => CSharpSyntaxTree.ParseText(
                File.ReadAllText(f),
                ParseOptions,
                path: $"{name}/{Path.GetRelativePath(dir, f).Replace('\\', '/')}",
                encoding: Encoding.UTF8))
            .ToList();
        if (trees.Count == 0)
        {
            throw new InvalidOperationException($"stub {name} has no sources in {dir}");
        }

        if (version is not null)
        {
            trees.Add(CSharpSyntaxTree.ParseText($"[assembly: System.Reflection.AssemblyVersion(\"{version}\")]", ParseOptions, path: $"{name}/AssemblyVersion.cs", encoding: Encoding.UTF8));
        }

        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            deterministic: true,
            nullableContextOptions: NullableContextOptions.Disable,
            generalDiagnosticOption: ReportDiagnostic.Default,
            warningLevel: 4);
        var compilation = CSharpCompilation.Create(name, trees, references, options);
        using var pe = new MemoryStream();
        var result = compilation.Emit(pe); // no PDB stream: no PDB
        if (!result.Success)
        {
            image = null;
            errors = string.Join(Environment.NewLine, result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            return false;
        }

        image = pe.ToArray();
        errors = string.Empty;
        return true;
    }

    private static string ComputeStamp(string stubs)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(Encoding.UTF8.GetBytes(BuilderVersion + "\n"));
        var files = Directory.EnumerateFiles(stubs, "*", SearchOption.AllDirectories)
            .Select(f => (Full: f, Relative: Path.GetRelativePath(stubs, f).Replace('\\', '/')))
            .Where(f => !IsBuildOutput(f.Relative))
            .OrderBy(f => f.Relative, StringComparer.Ordinal);
        foreach (var (full, relative) in files)
        {
            sha.AppendData(Encoding.UTF8.GetBytes(relative + "\n"));
            var content = File.ReadAllBytes(full);
            sha.AppendData(Encoding.UTF8.GetBytes(content.Length + "\n"));
            sha.AppendData(content);
        }

        // The Roslyn and runtime versions change the analyzer output too.
        sha.AppendData(Encoding.UTF8.GetBytes($"{typeof(Compilation).Assembly.GetName().Version} {Environment.Version}\n"));
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private static bool IsBuildOutput(string relative) =>
        relative.Split('/').Any(p => p is "bin" or "obj");

    private static IEnumerable<string> SubDirectories(string dir) =>
        Directory.Exists(dir) ? Directory.EnumerateDirectories(dir).Order(StringComparer.Ordinal) : [];

    private static IEnumerable<string> SourceFiles(string dir) =>
        Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(Path.GetRelativePath(dir, f).Replace('\\', '/')))
            .Order(StringComparer.Ordinal);

    private static string? ReadStamp(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsManaged(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new PEReader(stream);
            return reader.HasMetadata;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    // Several test processes may build the same folder at once; a lock file serialises them.
    private static FileStream AcquireLock(string path)
    {
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(200);
            }
        }
    }
}
