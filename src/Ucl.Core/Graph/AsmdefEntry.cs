using Ucl.Core.Parsing;
using Ucl.Core.Rules;

namespace Ucl.Core.Graph;

/// <summary>A parsed asmdef with its location and GUID.</summary>
/// <param name="Path">asmdef path.</param>
/// <param name="Data">Parsed fields.</param>
/// <param name="Guid">GUID from its meta, or null.</param>
public sealed record AsmdefEntry(string Path, AsmdefData Data, string? Guid)
{
    /// <summary>Folder that holds the asmdef.</summary>
    public string Folder => ProjectPaths.Folder(Path);

    /// <summary>
    /// True for test assemblies: <c>defineConstraints</c> has the entry <c>UNITY_INCLUDE_TESTS</c> (legacy
    /// <c>optionalUnityReferences</c> assemblies get it on load). UnityCsReference <c>CustomScriptAssembly.IsCompatibleWith</c>.
    /// </summary>
    public bool IsTestAssembly => Data.DefineConstraints.Contains("UNITY_INCLUDE_TESTS", StringComparer.Ordinal);

    /// <summary>True for an asmdef with <c>optionalUnityReferences</c>, which also references the test runner assemblies.</summary>
    public bool IsLegacyTestAssembly => Data.OptionalUnityReferences.Count > 0;

    /// <summary>True when <c>defineConstraints</c> has the entry <c>UNITY_TESTS_FRAMEWORK</c>, as the test framework's own assemblies do.</summary>
    public bool IsTestFrameworkAssembly => Data.DefineConstraints.Contains("UNITY_TESTS_FRAMEWORK", StringComparer.Ordinal);

    /// <summary>The package that holds this asmdef (<c>Packages/&lt;name&gt;/...</c>), or null for <c>Assets/</c>.</summary>
    public string? PackageName => Path.StartsWith("Packages/", StringComparison.Ordinal) ? Path.Split('/')[1] : null;
}
