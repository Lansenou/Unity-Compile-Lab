namespace Ucl.Discovery;

/// <summary>What <c>ucl fetch</c> did for one package.</summary>
public enum PackageFetchStatus
{
    /// <summary>Already available locally (embedded, local, <c>Library/PackageCache</c> or the download cache).</summary>
    Cached,

    /// <summary>Downloaded from a registry into the download cache.</summary>
    Fetched,

    /// <summary>Could not be made available (network error, bad tarball, git or file package).</summary>
    Failed,
}
