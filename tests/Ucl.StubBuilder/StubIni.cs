namespace Ucl.StubBuilder;

/// <summary>
/// Optional <c>stub.ini</c> next to a stub's sources. <c>#</c> starts a comment line. Keys:
/// <list type="bullet">
/// <item><c>version=</c>: assembly version (default 0.0.0.0).</item>
/// <item><c>profile=</c>: <c>netstandard2.1</c> (default), <c>netstandard2.0</c> or <c>System.Runtime</c>, the contract it is compiled against.</item>
/// <item><c>name=</c>: assembly name when it differs from the folder name (two versions of one assembly need two folders).</item>
/// <item><c>engine=</c>: <c>modules</c> (default) compiles against the stub module DLLs; <c>UnityEngine</c> against a monolithic
/// <c>UnityEngine</c> assembly, as DLLs built for old Unity versions are, so that only the <c>UnityEngine.dll</c> facade resolves them.</item>
/// <item><c>path=</c> (<c>editor-extra/</c> only): folder under the editor's data folder that receives the DLL.</item>
/// <item><c>kind=</c> (<c>editor-extra/</c> only): <c>library</c> (default) or <c>analyzer</c> (compiled against Roslyn).</item>
/// <item><c>references=</c> (<c>editor-extra/</c> only): comma-separated stub module names it references.</item>
/// <item><c>sources=</c> (<c>editor-extra/</c> only): folder under <c>fixtures/_stubs</c> to compile instead of this one (a
/// platform's variant of an engine module shares the module's sources).</item>
/// </list>
/// </summary>
internal sealed record StubIni(string? Version, string Profile, string? Name, string Engine, string? DataPath, string Kind, IReadOnlyList<string> References, string? Sources = null)
{
    public static StubIni Read(string dir)
    {
        var path = Path.Combine(dir, "stub.ini");
        var values = File.Exists(path)
            ? File.ReadAllLines(path)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .Select(l => l.Split('=', 2, StringSplitOptions.TrimEntries))
                .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : string.Empty, StringComparer.Ordinal)
            : [];
        var profile = values.GetValueOrDefault("profile", "netstandard2.1");
        if (profile is not ("netstandard2.1" or "netstandard2.0" or "System.Runtime"))
        {
            throw new InvalidOperationException($"{path}: unknown profile '{profile}'");
        }

        var engine = values.GetValueOrDefault("engine", "modules");
        if (engine is not ("modules" or "UnityEngine"))
        {
            throw new InvalidOperationException($"{path}: unknown engine '{engine}'");
        }

        var kind = values.GetValueOrDefault("kind", "library");
        if (kind is not ("library" or "analyzer"))
        {
            throw new InvalidOperationException($"{path}: unknown kind '{kind}'");
        }

        var references = values.GetValueOrDefault("references", string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new StubIni(values.GetValueOrDefault("version"), profile, values.GetValueOrDefault("name"), engine, values.GetValueOrDefault("path"), kind, references, values.GetValueOrDefault("sources"));
    }
}
