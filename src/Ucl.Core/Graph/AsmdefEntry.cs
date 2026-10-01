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

    /// <summary>True for test assemblies, which predefined assemblies never reference.</summary>
    public bool IsTestAssembly => Data.OptionalUnityReferences.Contains("TestAssemblies", StringComparer.Ordinal);
}
