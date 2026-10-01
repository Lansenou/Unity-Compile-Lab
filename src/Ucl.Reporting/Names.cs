using Ucl.Core.Graph;
using Ucl.Core.Model;
using Ucl.Core.Results;

namespace Ucl.Reporting;

/// <summary>The lowercase names enums have in every output format. Part of the JSON contract.</summary>
public static class Names
{
    /// <summary>Severity name.</summary>
    public static string Of(Severity s) => s switch { Severity.Error => "error", Severity.Warning => "warning", _ => "info" };

    /// <summary>Origin name.</summary>
    public static string Of(DiagnosticOrigin o) => o switch { DiagnosticOrigin.Compiler => "compiler", DiagnosticOrigin.Analyzer => "analyzer", _ => "ucl" };

    /// <summary>Target name.</summary>
    public static string Of(TargetKind t) => t == TargetKind.Editor ? "editor" : "player";

    /// <summary>Status name.</summary>
    public static string Of(AssemblyStatus s) => s switch { AssemblyStatus.Compiled => "compiled", AssemblyStatus.Failed => "failed", _ => "skipped" };

    /// <summary>Kind name.</summary>
    public static string Of(AssemblyKind k) => k == AssemblyKind.Asmdef ? "asmdef" : "predefined";

    /// <summary>Backend name.</summary>
    public static string Of(ScriptingBackend b) => b == ScriptingBackend.IL2CPP ? "il2cpp" : "mono";

    /// <summary>Host OS name.</summary>
    public static string Of(HostOs o) => o switch { HostOs.Windows => "windows", HostOs.MacOS => "macos", _ => "linux" };
}
