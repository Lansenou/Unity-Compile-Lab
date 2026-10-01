namespace Ucl.Core.Model;

/// <summary>Stable ids of <c>ucl</c>'s own diagnostics and configuration problems. Public contract: never renumber.</summary>
public static class ProblemIds
{
    /// <summary>asmdef reference that resolves to nothing (warning).</summary>
    public const string MissingReference = "UCL1001";

    /// <summary>Cyclic assembly references (error).</summary>
    public const string CyclicReference = "UCL1002";

    /// <summary>asmref whose target assembly does not exist (warning).</summary>
    public const string MissingAsmrefTarget = "UCL1003";

    /// <summary>precompiledReferences entry with no such DLL (error).</summary>
    public const string MissingPrecompiledReference = "UCL1004";

    /// <summary>Script in a package outside any asmdef; Unity does not compile it (warning).</summary>
    public const string PackageScriptWithoutAsmdef = "UCL1010";

    /// <summary>Unsupported response-file option (warning).</summary>
    public const string UnsupportedRspOption = "UCL1020";

    /// <summary>Invalid versionDefines entry (warning).</summary>
    public const string BadVersionDefine = "UCL1021";

    /// <summary>Not a Unity project.</summary>
    public const string NotAProject = "UCL3001";

    /// <summary>Unsupported or unreadable editor version.</summary>
    public const string UnsupportedVersion = "UCL3002";

    /// <summary>No matching editor install.</summary>
    public const string EditorNotFound = "UCL3003";

    /// <summary>Malformed asmdef or asmref.</summary>
    public const string BadAsmdef = "UCL3004";

    /// <summary>Two assemblies with one name.</summary>
    public const string DuplicateAssembly = "UCL3005";

    /// <summary>Package that cannot be resolved.</summary>
    public const string UnresolvedPackage = "UCL3006";

    /// <summary>Folder with more than one asmdef or asmref.</summary>
    public const string MultipleDefinitionsInFolder = "UCL3007";

    /// <summary>Malformed project file (manifest, lock file, ProjectSettings).</summary>
    public const string BadProjectFile = "UCL3008";

    /// <summary>Invalid command-line usage.</summary>
    public const string BadArguments = "UCL3009";
}
