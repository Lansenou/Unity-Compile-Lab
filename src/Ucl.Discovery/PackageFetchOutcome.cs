namespace Ucl.Discovery;

/// <summary>The result of <c>ucl fetch</c> for one package.</summary>
/// <param name="Name">Package name.</param>
/// <param name="Version">Version (or the git/file URL for non-registry packages).</param>
/// <param name="Status">What happened.</param>
/// <param name="Detail">Registry URL for <see cref="PackageFetchStatus.Fetched"/>, resolution source for
/// <see cref="PackageFetchStatus.Cached"/>, reason for <see cref="PackageFetchStatus.Failed"/>.</param>
public sealed record PackageFetchOutcome(string Name, string Version, PackageFetchStatus Status, string Detail);
