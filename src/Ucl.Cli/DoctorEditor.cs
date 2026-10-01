namespace Ucl.Cli;

/// <summary>One editor install as <c>ucl doctor</c> reports it.</summary>
internal sealed record DoctorEditor(
    string Version,
    string Root,
    string DataPath,
    bool HasManaged,
    int EngineModules,
    int EditorAssemblies,
    int NetStandardReferences);
