namespace Ucl.Core.Graph;

/// <summary>A package after resolution.</summary>
/// <param name="Name">Package name.</param>
/// <param name="Version">Version from its <c>package.json</c> (or the lock file for built-in modules).</param>
/// <param name="LogicalRoot"><c>Packages/&lt;name&gt;</c>, or null for built-in modules, which have no files.</param>
/// <param name="Source">Where it was found: <c>embedded</c>, <c>local</c>, <c>cache</c>, <c>download</c> or <c>builtin</c>.</param>
public sealed record ResolvedPackage(string Name, string Version, string? LogicalRoot, string Source)
{
    /// <summary>True for <c>com.unity.modules.*</c>.</summary>
    public bool IsBuiltInModule => Name.StartsWith("com.unity.modules.", StringComparison.Ordinal);
}
