namespace Ucl.Discovery;

/// <summary>Where <c>ucl fetch</c> stores registry packages: <c>&lt;root&gt;/&lt;name&gt;@&lt;version&gt;/</c> holds the package contents.</summary>
public static class DownloadCache
{
    /// <summary>Environment variable that overrides the cache root.</summary>
    public const string Variable = "UCL_PACKAGE_CACHE";

    /// <summary>The cache root: <c>UCL_PACKAGE_CACHE</c>, else <c>&lt;home&gt;/.cache/ucl/packages</c>. Same rule as package resolution.</summary>
    public static string Root(IEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        return env.GetVariable(Variable) is { Length: > 0 } custom
            ? Path.GetFullPath(custom)
            : Path.Combine(env.HomeDirectory, ".cache", "ucl", "packages");
    }

    /// <summary>The folder of one package version.</summary>
    public static string Folder(IEnvironment env, string name, string version) => Path.Combine(Root(env), $"{name}@{version}");
}
