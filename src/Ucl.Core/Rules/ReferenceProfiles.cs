namespace Ucl.Core.Rules;

/// <summary>
/// Which .NET reference assemblies of an editor install a compile uses, per API compatibility profile
/// (docs/defines.md, "API compatibility level"). Folders are relative to the editor's data folder
/// (<c>Editor/Data</c>, <c>Unity.app/Contents</c>).
/// </summary>
public static class ReferenceProfiles
{
    /// <summary>The .NET Framework profile folder.</summary>
    public const string NetFrameworkFolder = "UnityReferenceAssemblies/unity-4.8-api";

    /// <summary>Every DLL in this folder is referenced in the .NET Framework profile.</summary>
    public const string NetFrameworkFacadesFolder = "UnityReferenceAssemblies/unity-4.8-api/Facades";

    /// <summary>
    /// The DLLs of <see cref="NetFrameworkFolder"/> itself that are referenced (when present); the other DLLs there are not.
    /// </summary>
    public static IReadOnlyList<string> NetFrameworkLibraries { get; } =
    [
        "Microsoft.CSharp.dll",
        "System.ComponentModel.Composition.dll",
        "System.Core.dll",
        "System.Data.DataSetExtensions.dll",
        "System.Data.dll",
        "System.Drawing.dll",
        "System.IO.Compression.FileSystem.dll",
        "System.IO.Compression.dll",
        "System.Net.Http.dll",
        "System.Numerics.Vectors.dll",
        "System.Numerics.dll",
        "System.Runtime.Serialization.dll",
        "System.Transactions.dll",
        "System.Xml.Linq.dll",
        "System.Xml.dll",
        "System.dll",
        "mscorlib.dll",
    ];

    /// <summary>The .NET Standard 2.1 reference assembly (one file).</summary>
    public const string NetStandardReference = "NetStandard/ref/2.1.0/netstandard.dll";

    /// <summary>Folders whose every DLL is referenced in the .NET Standard profile, after <see cref="NetStandardReference"/>.</summary>
    public static IReadOnlyList<string> NetStandardFolders { get; } =
    [
        "NetStandard/compat/2.1.0/shims/netstandard",
        "NetStandard/Extensions/2.0.0",
        "NetStandard/compat/2.1.0/shims/netfx",
    ];

    /// <summary>Extra folder referenced by Editor-only assemblies compiled with the .NET Standard profile.</summary>
    public const string NetStandardEditorExtensionsFolder = "NetStandard/EditorExtensions";
}
