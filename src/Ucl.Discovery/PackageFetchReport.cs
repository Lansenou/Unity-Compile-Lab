using Ucl.Core.Model;

namespace Ucl.Discovery;

/// <summary>The result of <c>ucl fetch</c>.</summary>
/// <param name="Outcomes">One entry per non-built-in package, sorted by name.</param>
/// <param name="Problems">What still does not resolve afterwards (<c>UCL3006</c>) and unreadable project files (<c>UCL3008</c>).</param>
public sealed record PackageFetchReport(IReadOnlyList<PackageFetchOutcome> Outcomes, IReadOnlyList<Problem> Problems);
