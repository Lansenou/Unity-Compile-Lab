using Ucl.Core.Bee;
using Ucl.Discovery;

namespace Ucl.Cli;

/// <summary>
/// Brings both sides of a Bee comparison into <see cref="CommandLine"/> form: project paths become logical (package
/// files under <c>Library/PackageCache</c> become <c>Packages/&lt;name&gt;/...</c>), editor files <c>editor:&lt;path&gt;</c>,
/// project assemblies (<c>Library/Bee/artifacts/**/X.ref.dll</c>, <c>Library/ScriptAssemblies/X.dll</c>) their names.
/// </summary>
internal sealed class BeeNormalizer(string projectRoot, ProjectContext project, EditorInstall editor, AssemblyIdentityReader identities)
{
    private readonly string _bee = Path.Combine(projectRoot, "Library", "Bee");
    private readonly string _scriptAssemblies = Path.Combine(projectRoot, "Library", "ScriptAssemblies");

    public string Full(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path));

    public string Location(string path)
    {
        var full = Full(path);
        if (Under(editor.DataPath, full) is { } inEditor)
        {
            return "editor:" + inEditor;
        }

        return project.ToLogical(full) ?? full.Replace('\\', '/');
    }

    public ReferenceEntry Reference(string path)
    {
        var full = Full(path);
        var file = Path.GetFileName(full);
        if (Under(_bee, full) is not null || Under(_scriptAssemblies, full) is not null)
        {
            var name = file.EndsWith(".ref.dll", StringComparison.OrdinalIgnoreCase) ? file[..^".ref.dll".Length]
                : file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? file[..^".dll".Length] : file;
            return ReferenceEntry.ForAssembly(name);
        }

        return new ReferenceEntry(null, file, identities.Version(full), Location(full));
    }

    /// <summary>The Editor's command line. Additional files Bee generates itself (under <c>Library/Bee</c>) are left out.</summary>
    public CommandLine FromBee(BeeResponseFile rsp) => new()
    {
        Sources = [.. rsp.Sources.Select(Location)],
        References = [.. rsp.References.Select(Reference)],
        Defines = rsp.Defines,
        NoWarn = rsp.NoWarn,
        Analyzers = [.. rsp.Analyzers.Select(Location)],
        AdditionalFiles = [.. rsp.AdditionalFiles.Where(f => Under(_bee, Full(f)) is null).Select(Location)],
        LangVersion = rsp.LangVersion,
        Unsafe = rsp.Unsafe,
    };

    // The path of 'full' relative to 'root' with '/', or null when it is outside.
    private static string? Under(string root, string full)
    {
        var rel = Path.GetRelativePath(root, full);
        return rel == "." || rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? null : rel.Replace('\\', '/');
    }
}
