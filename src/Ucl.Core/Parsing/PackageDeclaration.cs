namespace Ucl.Core.Parsing;

/// <summary>A package as declared by <c>manifest.json</c> and resolved by <c>packages-lock.json</c>.</summary>
/// <param name="Name">Package name.</param>
/// <param name="Requested">Version or URL from <c>manifest.json</c> (empty for indirect dependencies).</param>
/// <param name="Version">Resolved version or URL from the lock file, else <paramref name="Requested"/>.</param>
/// <param name="Source">Lock-file source: <c>registry</c>, <c>builtin</c>, <c>embedded</c>, <c>local</c>, <c>git</c>, or empty.</param>
/// <param name="Url">Registry URL from the lock file, or empty.</param>
/// <param name="Dependencies">Dependency names from the lock file.</param>
public sealed record PackageDeclaration(
    string Name,
    string Requested,
    string Version,
    string Source,
    string Url,
    IReadOnlyList<string> Dependencies)
{
    /// <summary>True for <c>com.unity.modules.*</c> built-in packages.</summary>
    public bool IsBuiltInModule => Name.StartsWith("com.unity.modules.", StringComparison.Ordinal);
}
