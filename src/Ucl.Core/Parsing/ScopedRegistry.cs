namespace Ucl.Core.Parsing;

/// <summary>A scoped registry from <c>manifest.json</c>.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Url">Registry URL.</param>
/// <param name="Scopes">Package name prefixes served by this registry.</param>
public sealed record ScopedRegistry(string Name, string Url, IReadOnlyList<string> Scopes)
{
    /// <summary>True when the registry serves <paramref name="package"/> (Unity matches whole dot-separated prefixes).</summary>
    public bool Serves(string package) =>
        Scopes.Any(s => package == s || package.StartsWith(s + ".", StringComparison.Ordinal));
}
