namespace Ucl.Core.Parsing;

/// <summary>The parsed package manifest and lock file.</summary>
/// <param name="Packages">Every direct and (from the lock file) indirect package, sorted by name.</param>
/// <param name="ScopedRegistries">Scoped registries: URL and scopes.</param>
/// <param name="Testables">Packages listed under <c>testables</c>.</param>
public sealed record PackageManifest(
    IReadOnlyList<PackageDeclaration> Packages,
    IReadOnlyList<ScopedRegistry> ScopedRegistries,
    IReadOnlyList<string> Testables);
