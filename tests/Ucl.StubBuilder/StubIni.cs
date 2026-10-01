namespace Ucl.StubBuilder;

/// <summary>
/// Optional <c>stub.ini</c> next to a plugin stub's sources: <c>version=</c> (assembly version, default 0.0.0.0) and
/// <c>profile=</c> (<c>netstandard2.1</c>, the default; <c>netstandard2.0</c> or <c>System.Runtime</c>: the contract it is
/// compiled against). <c>#</c> starts a comment line.
/// </summary>
internal sealed record StubIni(string? Version, string Profile)
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

        return new StubIni(values.GetValueOrDefault("version"), profile);
    }
}
