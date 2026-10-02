using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ucl.StubBuilder;

/// <summary>
/// The stub .NET Framework 4.8 profile (<c>UnityReferenceAssemblies/unity-4.8-api</c>) and the contracts plugin stubs
/// compile against. One self-written core library (<c>fixtures/_stubs/profiles/unity-4.8-api/mscorlib</c>) is
/// compiled under three identities: <c>mscorlib</c> 4.0.0.0 (the profile), <c>netstandard</c> 2.0.0.0 and
/// <c>System.Runtime</c> 4.0.0.0 (contracts that plugin stubs reference, as NuGet builds do). Facades forward every
/// public type of the core library: <c>Facades/netstandard.dll</c> and <c>Facades/System.Runtime.dll</c> to
/// <c>mscorlib</c>, and the NetStandard shims <c>shims/netstandard/System.Runtime.dll</c> and
/// <c>shims/netfx/mscorlib.dll</c> to the real <c>netstandard</c> 2.1.
/// Assemblies carry the public keys of the real ones (public signing; no private key is involved), because the
/// compiler matches references by name, version and public key token.
/// </summary>
internal sealed record ProfileStubs(
    byte[] Mscorlib,
    byte[] NetStandardFacade,
    byte[] SystemRuntimeFacade,
    byte[] NetStandardSystemRuntimeShim,
    byte[] NetfxMscorlibShim,
    byte[] NetStandard20Contract,
    byte[] SystemRuntimeContract)
{
    // The ECMA standard public key mscorlib carries (token b77a5c561934e089).
    private static readonly byte[] EcmaKey = [0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0];

    public static ProfileStubs Build(string coreSources, string netstandardPath, CSharpParseOptions parseOptions)
    {
        var sources = Directory.EnumerateFiles(coreSources, "*.cs").Order(StringComparer.Ordinal)
            .Select(f => (Path: $"mscorlib/{Path.GetFileName(f)}", Text: File.ReadAllText(f)))
            .ToList();
        var netstandardKey = PublicKey(netstandardPath);
        var systemRuntimeKey = PublicKey(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll"));
        var netstandard21 = MetadataReference.CreateFromFile(netstandardPath);

        var mscorlib = Compile("mscorlib", "4.0.0.0", EcmaKey, sources, [], parseOptions);
        var mscorlibReference = MetadataReference.CreateFromImage(mscorlib);
        var forwards = PublicTypes(mscorlibReference);
        return new ProfileStubs(
            mscorlib,
            Compile("netstandard", "2.0.0.0", netstandardKey, [Forwarders(forwards)], [mscorlibReference], parseOptions),
            Compile("System.Runtime", "4.0.0.0", systemRuntimeKey, [Forwarders(forwards)], [mscorlibReference], parseOptions),
            Compile("System.Runtime", "4.0.0.0", systemRuntimeKey, [Forwarders(forwards)], [netstandard21], parseOptions),
            Compile("mscorlib", "4.0.0.0", EcmaKey, [Forwarders(forwards)], [netstandard21], parseOptions),
            Compile("netstandard", "2.0.0.0", netstandardKey, sources, [], parseOptions),
            Compile("System.Runtime", "4.0.0.0", systemRuntimeKey, sources, [], parseOptions));
    }

    private static byte[] Compile(
        string name, string version, byte[] publicKey, IEnumerable<(string Path, string Text)> sources, MetadataReference[] references, CSharpParseOptions parseOptions)
    {
        var trees = sources
            .Append((Path: $"{name}/AssemblyVersion.cs", Text: $"[assembly: System.Reflection.AssemblyVersion(\"{version}\")]"))
            .Select(s => CSharpSyntaxTree.ParseText(s.Text, parseOptions, path: s.Path, encoding: Encoding.UTF8))
            .ToList();
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            deterministic: true,
            nullableContextOptions: NullableContextOptions.Disable,
            publicSign: true,
            cryptoPublicKey: [.. publicKey]);
        var compilation = CSharpCompilation.Create(name, trees, references, options);
        using var pe = new MemoryStream();
        var result = compilation.Emit(pe, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(runtimeMetadataVersion: "v4.0.30319"));
        return result.Success
            ? pe.ToArray()
            : throw new InvalidOperationException($"profile stub {name} does not compile:{Environment.NewLine}"
                + string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
    }

    internal static (string Path, string Text) Forwarders(IEnumerable<string> types) =>
        ("Forwarders.cs", string.Concat(types.Select(t => $"[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof({t}))]\n")));

    // Top-level public types as typeof() operands (List<> for generics); nested types forward with their parent.
    internal static List<string> PublicTypes(MetadataReference library)
    {
        var compilation = CSharpCompilation.Create("probe", references: [library]);
        var assembly = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(library)!;
        var result = new List<string>();
        Walk(assembly.GlobalNamespace);
        return result.Order(StringComparer.Ordinal).ToList();

        void Walk(INamespaceSymbol ns)
        {
            // System.Void cannot be named in typeof(); signatures encode it as a primitive, never as a type reference.
            foreach (var type in ns.GetTypeMembers().Where(t => t.DeclaredAccessibility == Accessibility.Public && t.SpecialType != SpecialType.System_Void))
            {
                var name = $"global::{type.ContainingNamespace.ToDisplayString()}.{type.Name}";
                result.Add(type.Arity == 0 ? name : $"{name}<{new string(',', type.Arity - 1)}>");
            }

            foreach (var child in ns.GetNamespaceMembers())
            {
                Walk(child);
            }
        }
    }

    private static byte[] PublicKey(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        return metadata.GetBlobBytes(metadata.GetAssemblyDefinition().PublicKey);
    }
}
