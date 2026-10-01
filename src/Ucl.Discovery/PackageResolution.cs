using Ucl.Core.Graph;
using Ucl.Core.Model;

namespace Ucl.Discovery;

/// <summary>The outcome of package resolution.</summary>
/// <param name="Packages">Resolved packages, sorted by name.</param>
/// <param name="Roots">Logical root (<c>Packages/&lt;name&gt;</c>) to absolute folder, for packages that have files.</param>
/// <param name="Problems">Unresolved packages and malformed <c>package.json</c> files.</param>
internal sealed record PackageResolution(
    IReadOnlyList<ResolvedPackage> Packages,
    IReadOnlyDictionary<string, string> Roots,
    IReadOnlyList<Problem> Problems);
